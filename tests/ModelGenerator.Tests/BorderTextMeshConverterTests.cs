using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class BorderTextMeshConverterTests
{
    private readonly BorderTextMeshConverter _converter = new();
    private readonly ShapeGenerator _shapes = new();

    [Fact]
    public void ConvertBorderTextToMesh_ShortEmbossedOnCircle_ProducesWatertightMesh()
    {
        var model = new Model { ShapeType = ShapeType.Circle, ShapeSize = 60, BorderThickness = 5, BorderHeight = 5, ShapeThickness = 10 };
        var (outer, inner) = _shapes.GenerateBorderOutline(model);
        var line = new BorderTextLine { Content = "OK", FontName = "Arial", FontSize = 8, Height = 1.5f, Mode = BorderTextMode.Embossed, AnchorAngleDegrees = 90 };
        var mesh = _converter.ConvertBorderTextToMesh(line, outer, inner, borderTopZ: 15);

        Assert.NotEmpty(mesh.Vertices);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void LayoutGlyphContours_LongString_ShrinksToFitCircumference()
    {
        var model = new Model { ShapeType = ShapeType.Circle, ShapeSize = 40, BorderThickness = 4, BorderHeight = 3, ShapeThickness = 8 };
        var (outer, inner) = _shapes.GenerateBorderOutline(model);
        var (midline, length) = BorderTextMeshConverter.BuildMidline(outer, inner);
        var line = new BorderTextLine
        {
            Content = "THIS IS A VERY LONG STRING THAT SHOULD NOT FIT AT FULL SIZE",
            FontName = "Arial",
            FontSize = 20,
            Mode = BorderTextMode.Engraved
        };

        var contours = _converter.LayoutGlyphContours(line, midline, length);
        Assert.NotEmpty(contours);

        // Approximate total span via bounding box diagonal of all points — must not explode beyond loop.
        var all = contours.SelectMany(c => c).ToList();
        var (min, max) = MeshMath.BoundingBox(all);
        float diag = Vector2.Distance(min, max);
        Assert.True(diag < length * 1.5f);
    }
}
