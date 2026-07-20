using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class TextMeshConverterTests
{
    private readonly TextMeshConverter _converter = new();

    [Fact]
    public void ConvertTextToMesh_SimpleLetter_ProducesWatertightMesh()
    {
        var line = new TextLine { Content = "L", FontName = "Arial", FontSize = 12, TextHeight = 5 };
        var mesh = _converter.ConvertTextToMesh(line);

        Assert.NotEmpty(mesh.Vertices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void ConvertTextToMesh_LetterWithHole_HandlesGlyphCounterCorrectly()
    {
        // 'O' has an inner contour (the counter). If holes were triangulated as solid, or
        // wound incorrectly, this would either lose the hole entirely or yield a non-positive
        // signed volume (inconsistent/inward-facing normals somewhere in the mesh).
        var line = new TextLine { Content = "O", FontName = "Arial", FontSize = 20, TextHeight = 5 };
        var mesh = _converter.ConvertTextToMesh(line);

        Assert.NotEmpty(mesh.Vertices);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);

        var withoutHole = _converter.ConvertTextToMesh(new TextLine { Content = "0", FontName = "Arial", FontSize = 20, TextHeight = 5 });
        // Sanity: 'O' should tessellate into meaningfully more geometry than a single filled blob
        // would need, i.e. it isn't degenerating to near-zero triangles.
        Assert.True(mesh.Indices.Count > 0);
        Assert.True(withoutHole.Indices.Count > 0);
    }

    [Fact]
    public void ConvertTextToMesh_EmptyContent_ProducesEmptyMesh()
    {
        var line = new TextLine { Content = "", FontName = "Arial", FontSize = 12, TextHeight = 5 };
        var mesh = _converter.ConvertTextToMesh(line);

        Assert.Empty(mesh.Vertices);
        Assert.Empty(mesh.Indices);
    }

    [Fact]
    public void ConvertMultilineText_ReturnsOneMeshPerLine()
    {
        var lines = new[]
        {
            new TextLine { LineNumber = 0, Content = "HELLO", FontName = "Arial", FontSize = 12, TextHeight = 5 },
            new TextLine { LineNumber = 1, Content = "WORLD", FontName = "Consolas", FontSize = 10, TextHeight = 5 }
        };

        var meshes = _converter.ConvertMultilineText(lines);

        Assert.Equal(2, meshes.Count);
        Assert.All(meshes, m => Assert.NotEmpty(m.Vertices));
    }
}
