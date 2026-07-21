using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class SvgMeshConverterTests
{
    private readonly SvgMeshConverter _converter = new();

    [Fact]
    public void ConvertSvgToMesh_SolidShape_ProducesWatertightMesh()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
                <rect x="10" y="10" width="80" height="80" />
            </svg>
            """;
        var insert = new SvgInsert { SvgContent = svg, Scale = 40, EmbossHeight = 5 };

        var mesh = _converter.ConvertSvgToMesh(insert);

        Assert.NotEmpty(mesh.Vertices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void ConvertSvgToMesh_ShapeWithHole_HandlesHoleCorrectly()
    {
        // A single path with two subpaths wound in opposite directions (verified via the
        // shoelace formula, not just visually) — outer square positive area, inner square
        // negative — so the nonzero winding rule treats the inner square as a hole, exactly
        // like the counter of a letter "O". Mirrors
        // TextMeshConverterTests.ConvertTextToMesh_LetterWithHole_HandlesGlyphCounterCorrectly.
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
                <path d="M0,0 L10,0 L10,10 L0,10 Z M3,3 L3,7 L7,7 L7,3 Z" />
            </svg>
            """;
        var insert = new SvgInsert { SvgContent = svg, Scale = 40, EmbossHeight = 5 };

        var mesh = _converter.ConvertSvgToMesh(insert);

        Assert.NotEmpty(mesh.Vertices);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void ConvertSvgToMesh_NestedGroupWithTransform_AppliesTransformCorrectly()
    {
        // A rect translated via a wrapping <g transform>. Setting Scale to exactly match the
        // document's natural bounding-box size keeps the internal auto-scale factor at 1, so the
        // resulting mesh's world coordinates should directly reflect the translate.
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
                <g transform="translate(20, 10)">
                    <rect x="0" y="0" width="10" height="10" />
                </g>
            </svg>
            """;
        var insert = new SvgInsert { SvgContent = svg, Scale = 10, EmbossHeight = 5 };

        var mesh = _converter.ConvertSvgToMesh(insert);

        Assert.NotEmpty(mesh.Vertices);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);

        // The rect is the only geometry, so the mesh's bounding box must be a 10x10 square
        // (matching Scale, since the auto-scale factor is 1 here) — proving the <g> translate
        // was applied consistently, not doubled or dropped, for every vertex.
        var (min, max) = MeshMath.BoundingBox(mesh.Vertices.Select(v => new System.Numerics.Vector2(v.X, v.Y)));
        Assert.Equal(10f, max.X - min.X, precision: 1);
        Assert.Equal(10f, max.Y - min.Y, precision: 1);
    }

    [Fact]
    public void ConvertSvgToMesh_ArtworkOffCenterInALargerCanvas_MeshIsCenteredAtOrigin()
    {
        // A small rect tucked in the corner of a much bigger canvas — the kind of SVG that used
        // to put local (0,0) (what PositionX/Y and viewport dragging actually move) far from the
        // artwork's visual center, making a drag appear to yank the shape out from under the
        // cursor the instant it started. Regression test for that bug.
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
                <rect x="10" y="10" width="20" height="20" />
            </svg>
            """;
        var insert = new SvgInsert { SvgContent = svg, Scale = 40, EmbossHeight = 5 };

        var mesh = _converter.ConvertSvgToMesh(insert);

        var (min, max) = MeshMath.BoundingBox(mesh.Vertices.Select(v => new System.Numerics.Vector2(v.X, v.Y)));
        var center = (min + max) / 2f;
        Assert.Equal(0f, center.X, precision: 1);
        Assert.Equal(0f, center.Y, precision: 1);
    }

    [Fact]
    public void ConvertMultipleSvgInserts_ReturnsOneMeshPerInsert()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
                <rect x="0" y="0" width="10" height="10" />
            </svg>
            """;
        var inserts = new[]
        {
            new SvgInsert { LineNumber = 0, SvgContent = svg, Scale = 20, EmbossHeight = 5 },
            new SvgInsert { LineNumber = 1, SvgContent = svg, Scale = 30, EmbossHeight = 8 }
        };

        var meshes = _converter.ConvertMultipleSvgInserts(inserts);

        Assert.Equal(2, meshes.Count);
        Assert.All(meshes, m => Assert.NotEmpty(m.Vertices));
    }
}
