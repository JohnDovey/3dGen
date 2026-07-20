using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class ShapeGeneratorTests
{
    private readonly ShapeGenerator _generator = new();

    [Fact]
    public void GenerateCircle_ProducesWatertightIndexedMesh()
    {
        var mesh = _generator.GenerateCircle(diameter: 60, thickness: 10, borderThickness: 5, borderHeight: 5);

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Count - 1));
    }

    [Fact]
    public void GenerateRectangle_ProducesWatertightIndexedMesh()
    {
        var mesh = _generator.GenerateRectangle(width: 80, height: 50, thickness: 10, borderThickness: 5, borderHeight: 5);

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Count - 1));
    }

    [Fact]
    public void GenerateRectangle_ThrowsWhenBorderTooWide()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateRectangle(width: 20, height: 50, thickness: 10, borderThickness: 15, borderHeight: 5));
    }

    [Fact]
    public void GenerateCircle_ThrowsWhenBorderTooWide()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateCircle(diameter: 20, thickness: 10, borderThickness: 15, borderHeight: 5));
    }

    [Fact]
    public void GenerateTriangle_ProducesWatertightMeshWithPositiveSignedVolume()
    {
        var mesh = _generator.GenerateTriangle(size: 60, thickness: 10, borderThickness: 5, borderHeight: 5);

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Count - 1));
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void GenerateTriangle_ThrowsWhenBorderTooWide()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateTriangle(size: 20, thickness: 10, borderThickness: 15, borderHeight: 5));
    }

    [Fact]
    public void GenerateShield_ProducesWatertightMeshWithPositiveSignedVolume()
    {
        var mesh = _generator.GenerateShield(size: 60, thickness: 10, borderThickness: 5, borderHeight: 5);

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Count - 1));
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void GenerateShield_ThrowsWhenBorderTooWide()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateShield(size: 20, thickness: 10, borderThickness: 15, borderHeight: 5));
    }

    [Theory]
    [InlineData(ShapeType.Circle)]
    [InlineData(ShapeType.Rectangle)]
    [InlineData(ShapeType.Triangle)]
    [InlineData(ShapeType.Shield)]
    public void GenerateParts_FloorAndBorderAreEachWatertight(ShapeType shapeType)
    {
        var model = new Model { ShapeType = shapeType, ShapeSize = 60, ShapeHeight = 50 };

        var (floor, border) = _generator.GenerateParts(model);

        Assert.NotEmpty(floor.Vertices);
        Assert.NotEmpty(border.Vertices);
        Assert.True(MeshMath.SignedVolume(floor) > 0);
        Assert.True(MeshMath.SignedVolume(border) > 0);
    }

    [Theory]
    [InlineData(ShapeType.Circle)]
    [InlineData(ShapeType.Rectangle)]
    [InlineData(ShapeType.Triangle)]
    [InlineData(ShapeType.Shield)]
    public void GenerateParts_IsLosslessRelativeToTheMergedGenerate(ShapeType shapeType)
    {
        var model = new Model { ShapeType = shapeType, ShapeSize = 60, ShapeHeight = 50 };

        var (floor, border) = _generator.GenerateParts(model);
        var merged = _generator.Generate(model);

        Assert.Equal(merged.Vertices.Count, floor.Vertices.Count + border.Vertices.Count);
        Assert.Equal(merged.Indices.Count, floor.Indices.Count + border.Indices.Count);
    }

    private const string SquareOutlineSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
            <rect x="10" y="10" width="80" height="80" />
        </svg>
        """;

    [Fact]
    public void GenerateCustomSvg_ProducesWatertightMeshWithPositiveSignedVolume()
    {
        var mesh = _generator.GenerateCustomSvg(SquareOutlineSvg, size: 60, thickness: 10, borderThickness: 5, borderHeight: 5);

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Count - 1));
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void GenerateCustomSvg_FitsOutlineToRequestedSize()
    {
        // The square's bounding box is 80x80 in SVG-source units; requesting size=60 should scale
        // it so the mesh's own footprint bounding box is 60 in its longer dimension.
        var mesh = _generator.GenerateCustomSvg(SquareOutlineSvg, size: 60, thickness: 10, borderThickness: 5, borderHeight: 5);

        var (min, max) = MeshMath.BoundingBox(mesh.Vertices.Select(v => new System.Numerics.Vector2(v.X, v.Y)));
        Assert.Equal(60f, max.X - min.X, precision: 1);
        Assert.Equal(60f, max.Y - min.Y, precision: 1);
    }

    [Fact]
    public void GenerateCustomSvg_ThrowsWhenSvgHasNoGeometry()
    {
        const string emptySvg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10"></svg>""";

        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateCustomSvg(emptySvg, size: 60, thickness: 10, borderThickness: 5, borderHeight: 5));
    }

    [Fact]
    public void Generate_CustomSvgShapeType_ThrowsWhenModelHasNoCustomShapeSvgContent()
    {
        var model = new Model { ShapeType = ShapeType.CustomSvg, ShapeSize = 60 };

        Assert.Throws<ArgumentException>(() => _generator.Generate(model));
        Assert.Throws<ArgumentException>(() => _generator.GenerateParts(model));
    }

    [Fact]
    public void GenerateParts_CustomSvgShapeType_MatchesGenerateCustomSvg()
    {
        var model = new Model
        {
            ShapeType = ShapeType.CustomSvg,
            ShapeSize = 60,
            ShapeHeight = 50,
            CustomShapeSvgContent = SquareOutlineSvg
        };

        var (floor, border) = _generator.GenerateParts(model);
        var merged = _generator.Generate(model);

        Assert.NotEmpty(floor.Vertices);
        Assert.NotEmpty(border.Vertices);
        Assert.True(MeshMath.SignedVolume(floor) > 0);
        Assert.True(MeshMath.SignedVolume(border) > 0);
        Assert.Equal(merged.Vertices.Count, floor.Vertices.Count + border.Vertices.Count);
    }
}
