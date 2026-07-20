using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class MeshMathTests
{
    [Fact]
    public void ShapeGenerator_Circle_HasPositiveSignedVolume()
    {
        var mesh = new ShapeGenerator().GenerateCircle(diameter: 60, thickness: 10, borderThickness: 5, borderHeight: 5);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void ShapeGenerator_Rectangle_HasPositiveSignedVolume()
    {
        var mesh = new ShapeGenerator().GenerateRectangle(width: 80, height: 50, thickness: 10, borderThickness: 5, borderHeight: 5);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }
}
