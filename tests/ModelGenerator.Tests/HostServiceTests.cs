using System.Text;
using System.Text.Json;
using ModelGenerator.Core.Models;
using ModelGenerator.Host;
using ModelGenerator.Host.Protocol;
using Xunit;

namespace ModelGenerator.Tests;

public class HostServiceTests
{
    private readonly HostService _service = HostService.CreateDefault();

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
}
