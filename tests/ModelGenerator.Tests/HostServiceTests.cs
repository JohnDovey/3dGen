using System.Text;
using System.Text.Json;
using ModelGenerator.Core.Models;
using ModelGenerator.Host;
using ModelGenerator.Host.Protocol;
using Xunit;

namespace ModelGenerator.Tests;

public class HostServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"host-test-{Guid.NewGuid():N}");
    private readonly HostService _service;

    public HostServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
        _service = HostService.CreateForTesting(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void Ping_ReturnsOkAndProtocolVersion()
    {
        var result = _service.Ping();

        Assert.True(result.Ok);
        Assert.Equal(HostProtocol.Version, result.Protocol);
        Assert.False(string.IsNullOrWhiteSpace(result.Version));
    }

    [Fact]
    public void GenerateParts_Circle_ProducesNonEmptyMeshes()
    {
        var model = new Model
        {
            ShapeType = ShapeType.Circle,
            ShapeSize = 60,
            ShapeThickness = 10,
            BorderThickness = 5,
            BorderHeight = 5
        };

        var parts = _service.GenerateParts(model);

        Assert.True(parts.Floor.Vertices.Length >= 9);
        Assert.True(parts.Border.Vertices.Length >= 9);
        Assert.True(parts.VertexCount > 0);
        Assert.True(parts.TriangleCount > 0);
        Assert.Equal(parts.Floor.ColorArgb, model.BaseColorArgb);
        Assert.Equal(parts.Border.ColorArgb, model.BorderColorArgb);
    }

    [Fact]
    public void ExportStl_WritesFileWithExpectedHeaderSize()
    {
        var model = new Model { ShapeType = ShapeType.Circle, ShapeSize = 40 };
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.stl");

        try
        {
            var result = _service.ExportStl(model, path);

            Assert.True(File.Exists(path));
            Assert.Equal(new FileInfo(path).Length, result.Bytes);
            // Binary STL: 80-byte header + 4-byte count + 50 bytes per triangle
            Assert.Equal(84 + result.TriangleCount * 50L, result.Bytes);
            Assert.True(result.TriangleCount > 0);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task JsonRpcSession_PingRoundTrip()
    {
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        using (var writer = new StreamWriter(input, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" })
        {
            writer.WriteLine("""{"id":"1","method":"ping","params":{}}""");
            writer.Flush();
        }
        input.Position = 0;

        var session = new JsonRpcSession(_service, input, output);
        await session.RunAsync();

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8);
        string? line = reader.ReadLine();
        Assert.NotNull(line);

        var response = JsonSerializer.Deserialize<RpcResponse>(line!, HostProtocol.JsonOptions);
        Assert.NotNull(response);
        Assert.Equal("1", response!.Id);
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);
    }

    [Fact]
    public void JsonRpcSession_UnknownMethod_ReturnsMethodNotFound()
    {
        var session = new JsonRpcSession(_service, Stream.Null, Stream.Null);
        var response = session.HandleLine("""{"id":"x","method":"nope","params":{}}""");

        Assert.Equal("x", response.Id);
        Assert.NotNull(response.Error);
        Assert.Equal(RpcErrorCodes.MethodNotFound, response.Error!.Code);
    }

    [Fact]
    public void JsonRpcSession_GenerateParts_ViaParamsModel()
    {
        var session = new JsonRpcSession(_service, Stream.Null, Stream.Null);
        string request = """
            {"id":"g","method":"generateParts","params":{"model":{"shapeType":0,"shapeSize":50,"shapeThickness":10,"borderThickness":5,"borderHeight":5}}}
            """;

        var response = session.HandleLine(request.Trim());

        Assert.Equal("g", response.Id);
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);

        string resultJson = JsonSerializer.Serialize(response.Result, HostProtocol.JsonOptions);
        using var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.GetProperty("triangleCount").GetInt32() > 0);
    }

    [Fact]
    public void Model_RoundTripsThroughJson_ForHostWireFormat()
    {
        var model = new Model
        {
            Name = "Wire",
            ShapeType = ShapeType.Rectangle,
            ShapeSize = 80,
            ShapeHeight = 40,
            TextLines =
            {
                new TextLine { Content = "HI", FontName = "Arial", FontSize = 12 }
            }
        };

        string json = JsonSerializer.Serialize(model, HostProtocol.JsonOptions);
        var loaded = JsonSerializer.Deserialize<Model>(json, HostProtocol.JsonOptions);

        Assert.NotNull(loaded);
        Assert.Equal(model.Name, loaded!.Name);
        Assert.Equal(model.ShapeType, loaded.ShapeType);
        Assert.Equal(model.ShapeSize, loaded.ShapeSize);
        Assert.Single(loaded.TextLines);
        Assert.Equal("HI", loaded.TextLines[0].Content);
    }

    [Fact]
    public void SaveGetListDeleteModel_RoundTrips()
    {
        var model = new Model
        {
            Name = "Host Save Test",
            ShapeType = ShapeType.Circle,
            ShapeSize = 55,
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "OK", FontName = "Arial", FontSize = 12 }
            }
        };

        var saved = _service.SaveModel(model, saveMesh: true);
        Assert.True(saved.Id > 0);
        Assert.Equal("Host Save Test", saved.Name);

        var listed = _service.ListModels();
        Assert.Contains(listed.Models, m => m.Id == saved.Id && m.Name == "Host Save Test");

        var loaded = _service.GetModel(saved.Id);
        Assert.Equal(saved.Id, loaded.Model.Id);
        Assert.Equal("Host Save Test", loaded.Model.Name);
        Assert.Equal(55, loaded.Model.ShapeSize);
        Assert.Single(loaded.Model.TextLines);
        Assert.Equal("OK", loaded.Model.TextLines[0].Content);

        var deleted = _service.DeleteModel(saved.Id);
        Assert.True(deleted.Deleted);
        Assert.DoesNotContain(_service.ListModels().Models, m => m.Id == saved.Id);
    }

    [Fact]
    public void JsonRpcSession_SaveModel_ViaDispatch()
    {
        var session = new JsonRpcSession(_service, Stream.Null, Stream.Null);
        string request = """
            {"id":"s","method":"saveModel","params":{"model":{"name":"RpcSave","shapeType":0,"shapeSize":40,"shapeThickness":10,"borderThickness":5,"borderHeight":5,"textLines":[],"svgInserts":[],"imageInserts":[]},"saveMesh":false}}
            """;

        var response = session.HandleLine(request.Trim());
        Assert.Null(response.Error);
        string resultJson = JsonSerializer.Serialize(response.Result, HostProtocol.JsonOptions);
        using var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.Equal("RpcSave", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void SvgLibrary_ImportSearchTagDelete_AndThumbnail()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
              <rect x="0" y="0" width="10" height="10" />
            </svg>
            """;
        string source = Path.Combine(_tempDir, "star.svg");
        File.WriteAllText(source, svg);

        var imported = _service.ImportSvgFile(source);
        Assert.Equal("star.svg", imported.FileName);

        _service.SetSvgKeywords(imported.FileName, new[] { "badge", "logo" });
        var found = _service.ListSvgFiles("badge");
        Assert.Contains(found.Files, f => f.FileName == "star.svg" && f.Keywords.Contains("badge"));

        var content = _service.ReadSvgContent(imported.FileName);
        Assert.Contains("<rect", content.Content);

        var thumb = _service.RenderSvgThumbnail(imported.FileName, svgContent: null, width: 32, height: 32);
        Assert.True(thumb.Png.Length > 8);
        Assert.Equal(0x89, thumb.Png[0]); // PNG magic

        _service.DeleteSvgFile(imported.FileName);
        Assert.DoesNotContain(_service.ListSvgFiles().Files, f => f.FileName == "star.svg");
    }
}
