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
    public void GenerateModelParts_ReturnsBaseAndOnePositionedMeshPerLine_MatchingGenerateModel()
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

        var (baseMesh, textMeshes) = _orchestrator.GenerateModelParts(model);
        var merged = _orchestrator.GenerateModel(model);

        Assert.Equal(2, textMeshes.Count);
        Assert.Same(model.TextLines[0], textMeshes[0].Line);
        Assert.Same(model.TextLines[1], textMeshes[1].Line);
        Assert.NotEmpty(baseMesh.Vertices);
        Assert.All(textMeshes, t => Assert.NotEmpty(t.Mesh.Vertices));

        // Parts should sum to exactly the same geometry as the merged mesh.
        int expectedVertexCount = baseMesh.Vertices.Count + textMeshes.Sum(t => t.Mesh.Vertices.Count);
        Assert.Equal(expectedVertexCount, merged.Vertices.Count);
    }

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
