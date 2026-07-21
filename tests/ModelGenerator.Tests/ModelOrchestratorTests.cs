using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class ModelOrchestratorTests
{
    private readonly ModelOrchestrator _orchestrator = new(
        new ShapeGenerator(),
        new TextMeshConverter(),
        new SvgMeshConverter(),
        new ImageMeshConverter(),
        new BorderTextMeshConverter(),
        new TextPositioner(),
        new MeshComposer());

    [Fact]
    public void GenerateModel_MixedPositionModes_ProducesSingleWatertightMesh()
    {
        var model = new Model
        {
            ShapeType = ShapeType.Rectangle,
            ShapeSize = 80,
            ShapeHeight = 50,
            ShapeThickness = 10,
            BorderThickness = 5,
            BorderHeight = 5,
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "TAG", FontName = "Arial", FontSize = 12, TextHeight = 5, PositionMode = TextPositionMode.AutoCenter },
                new TextLine { LineNumber = 1, Content = "42", FontName = "Arial", FontSize = 8, TextHeight = 5, PositionMode = TextPositionMode.Relative, PositionX = 20, PositionY = -15 }
            }
        };

        var mesh = _orchestrator.GenerateModel(model);

        Assert.NotEmpty(mesh.Vertices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void GenerateModel_NoText_ProducesJustTheBaseShape()
    {
        var model = new Model { ShapeType = ShapeType.Circle, ShapeSize = 60 };
        var withText = new ShapeGenerator().GenerateCircle(60, model.ShapeThickness, model.BorderThickness, model.BorderHeight);

        var mesh = _orchestrator.GenerateModel(model);

        Assert.Equal(withText.Vertices.Count, mesh.Vertices.Count);
    }

    [Fact]
    public void GenerateModelParts_ReturnsFloorBorderAndOnePositionedMeshPerLine_MatchingGenerateModel()
    {
        var model = new Model
        {
            ShapeType = ShapeType.Rectangle,
            ShapeSize = 80,
            ShapeHeight = 50,
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "TAG", FontName = "Arial", FontSize = 12, TextHeight = 5, PositionMode = TextPositionMode.AutoCenter },
                new TextLine { LineNumber = 1, Content = "42", FontName = "Arial", FontSize = 8, TextHeight = 5, PositionMode = TextPositionMode.Relative, PositionX = 20, PositionY = -15 }
            }
        };

        var (floor, border, textMeshes, svgMeshes, imageMeshes, borderTextMeshes) = _orchestrator.GenerateModelParts(model);
        var merged = _orchestrator.GenerateModel(model);

        Assert.Equal(2, textMeshes.Count);
        Assert.Empty(svgMeshes);
        Assert.Empty(imageMeshes);
        Assert.Same(model.TextLines[0], textMeshes[0].Line);
        Assert.Same(model.TextLines[1], textMeshes[1].Line);
        Assert.NotEmpty(floor.Vertices);
        Assert.NotEmpty(border.Vertices);
        Assert.All(textMeshes, t => Assert.NotEmpty(t.Mesh.Vertices));

        // Parts should sum to exactly the same geometry as the merged mesh.
        int expectedVertexCount = floor.Vertices.Count + border.Vertices.Count + textMeshes.Sum(t => t.Mesh.Vertices.Count);
        Assert.Equal(expectedVertexCount, merged.Vertices.Count);
    }

    [Fact]
    public void GenerateModelParts_MixedTextAndSvgInserts_PositionsBothIndependently()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
                <rect x="0" y="0" width="10" height="10" />
            </svg>
            """;
        var model = new Model
        {
            ShapeType = ShapeType.Circle,
            ShapeSize = 80,
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "TAG", FontName = "Arial", FontSize = 12, TextHeight = 5, PositionMode = TextPositionMode.AutoCenter }
            },
            SvgInserts =
            {
                new SvgInsert { LineNumber = 0, SvgContent = svg, Scale = 15, EmbossHeight = 5, PositionMode = TextPositionMode.Manual, PositionX = 20, PositionY = 20, PositionZ = 10 }
            }
        };

        var (floor, border, textMeshes, svgMeshes, imageMeshes, borderTextMeshes) = _orchestrator.GenerateModelParts(model);
        var merged = _orchestrator.GenerateModel(model);

        Assert.Single(textMeshes);
        Assert.Single(svgMeshes);
        Assert.Empty(imageMeshes);
        Assert.Same(model.SvgInserts[0], svgMeshes[0].Insert);
        Assert.NotEmpty(svgMeshes[0].Mesh.Vertices);

        int expectedVertexCount = floor.Vertices.Count + border.Vertices.Count
            + textMeshes.Sum(t => t.Mesh.Vertices.Count) + svgMeshes.Sum(s => s.Mesh.Vertices.Count);
        Assert.Equal(expectedVertexCount, merged.Vertices.Count);
        Assert.True(MeshMath.SignedVolume(merged) > 0);
    }

    [Fact]
    public void GenerateModelParts_MixedTextSvgAndImageInserts_PositionsAllIndependently()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
                <rect x="0" y="0" width="10" height="10" />
            </svg>
            """;
        byte[] imageData = CreateSampleImage();
        var model = new Model
        {
            ShapeType = ShapeType.Circle,
            ShapeSize = 80,
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "TAG", FontName = "Arial", FontSize = 12, TextHeight = 5, PositionMode = TextPositionMode.AutoCenter }
            },
            SvgInserts =
            {
                new SvgInsert { LineNumber = 0, SvgContent = svg, Scale = 15, EmbossHeight = 5, PositionMode = TextPositionMode.Manual, PositionX = 20, PositionY = 20, PositionZ = 10 }
            },
            ImageInserts =
            {
                new ImageInsert { LineNumber = 0, ImageData = imageData, Scale = 15, ReliefHeight = 3, Detail = ImageDetail.Low, PositionMode = TextPositionMode.Manual, PositionX = -20, PositionY = -20, PositionZ = 10 }
            }
        };

        var (floor, border, textMeshes, svgMeshes, imageMeshes, borderTextMeshes) = _orchestrator.GenerateModelParts(model);
        var merged = _orchestrator.GenerateModel(model);

        Assert.Single(textMeshes);
        Assert.Single(svgMeshes);
        Assert.Single(imageMeshes);
        Assert.Same(model.ImageInserts[0], imageMeshes[0].Insert);
        Assert.NotEmpty(imageMeshes[0].Mesh.Vertices);

        int expectedVertexCount = floor.Vertices.Count + border.Vertices.Count
            + textMeshes.Sum(t => t.Mesh.Vertices.Count) + svgMeshes.Sum(s => s.Mesh.Vertices.Count)
            + imageMeshes.Sum(im => im.Mesh.Vertices.Count);
        Assert.Equal(expectedVertexCount, merged.Vertices.Count);
        Assert.True(MeshMath.SignedVolume(merged) > 0);
    }

    private static byte[] CreateSampleImage() => TestPng.Solid(10, 10, SkiaSharp.SKColors.Gray);

    [Fact]
    public void ExportSTL_WritesFileMatchingGeneratedMesh()
    {
        var model = new Model { ShapeType = ShapeType.Circle, ShapeSize = 60 };
        var mesh = _orchestrator.GenerateModel(model);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.stl");

        try
        {
            _orchestrator.ExportSTL(mesh, path);
            Assert.True(File.Exists(path));

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.ReadBytes(80);
            uint triangleCount = reader.ReadUInt32();
            Assert.Equal((uint)(mesh.Indices.Count / 3), triangleCount);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
