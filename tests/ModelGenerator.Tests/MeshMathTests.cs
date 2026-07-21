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

    [Fact]
    public void ExtrudeMaskedHeightfield_FullyIncludedConstantHeight_BehavesLikeABox()
    {
        var topZ = new float[3, 3];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                topZ[r, c] = 5f;
            }
        }
        var included = new bool[2, 2] { { true, true }, { true, true } };

        var mesh = MeshMath.ExtrudeMaskedHeightfield(topZ, included, cellWidth: 10, cellDepth: 10, zBottom: 0);

        // 2x2mm grid of cells -> 20x20mm footprint, 5mm tall: volume = 20*20*5 = 2000.
        Assert.Equal(2000f, MeshMath.SignedVolume(mesh), precision: 1);
    }

    [Fact]
    public void ExtrudeMaskedHeightfield_Peaked_IsWatertightWithPositiveVolume()
    {
        var topZ = new float[3, 3];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                topZ[r, c] = (r == 1 && c == 1) ? 8f : 2f;
            }
        }
        var included = new bool[2, 2] { { true, true }, { true, true } };

        var mesh = MeshMath.ExtrudeMaskedHeightfield(topZ, included, cellWidth: 10, cellDepth: 10, zBottom: 0);

        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void ExtrudeMaskedHeightfield_PartiallyIncluded_IsWatertightWithSmallerVolumeThanFull()
    {
        var topZ = new float[3, 3];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                topZ[r, c] = 5f;
            }
        }
        var fullyIncluded = new bool[2, 2] { { true, true }, { true, true } };
        var partiallyIncluded = new bool[2, 2] { { true, false }, { false, false } };

        var fullMesh = MeshMath.ExtrudeMaskedHeightfield(topZ, fullyIncluded, cellWidth: 10, cellDepth: 10, zBottom: 0);
        var partialMesh = MeshMath.ExtrudeMaskedHeightfield(topZ, partiallyIncluded, cellWidth: 10, cellDepth: 10, zBottom: 0);

        Assert.True(MeshMath.SignedVolume(partialMesh) > 0);
        Assert.True(MeshMath.SignedVolume(partialMesh) < MeshMath.SignedVolume(fullMesh));
    }
}
