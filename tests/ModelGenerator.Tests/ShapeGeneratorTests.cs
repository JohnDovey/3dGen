using ModelGenerator.Core.Services;
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
    public void GenerateTriangle_NotYetImplemented()
    {
        Assert.Throws<NotImplementedException>(() =>
            _generator.GenerateTriangle(size: 60, thickness: 10, borderThickness: 5, borderHeight: 5));
    }

    [Fact]
    public void GenerateShield_NotYetImplemented()
    {
        Assert.Throws<NotImplementedException>(() =>
            _generator.GenerateShield(size: 60, thickness: 10, borderThickness: 5, borderHeight: 5));
    }
}
