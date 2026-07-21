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
        float bandWidth = BorderTextMeshConverter.BorderBandWidth(outer, inner);
        var line = new BorderTextLine
        {
            Content = "THIS IS A VERY LONG STRING THAT SHOULD NOT FIT AT FULL SIZE",
            FontName = "Arial",
            FontSize = 20,
            Mode = BorderTextMode.Engraved
        };

        var contours = _converter.LayoutGlyphContours(line, midline, length, bandWidth);
        Assert.NotEmpty(contours);

        // Approximate total span via bounding box diagonal of all points — must not explode beyond loop.
        var all = contours.SelectMany(c => c).ToList();
        var (min, max) = MeshMath.BoundingBox(all);
        float diag = Vector2.Distance(min, max);
        Assert.True(diag < length * 1.5f);
    }

    [Fact]
    public void LayoutGlyphContours_FontTallerThanBand_ShrinksAndStaysWithinBand()
    {
        // BorderThickness (4) is much smaller than FontSize (20) — at natural size the glyphs
        // would poke out past both the outer and inner edge of the border.
        var model = new Model { ShapeType = ShapeType.Circle, ShapeSize = 60, BorderThickness = 4, BorderHeight = 3, ShapeThickness = 10 };
        var (outer, inner) = _shapes.GenerateBorderOutline(model);
        var (midline, length) = BorderTextMeshConverter.BuildMidline(outer, inner);
        float bandWidth = BorderTextMeshConverter.BorderBandWidth(outer, inner);
        var line = new BorderTextLine { Content = "TEXT", FontName = "Arial", FontSize = 20, Mode = BorderTextMode.Engraved, AnchorAngleDegrees = 90 };

        var contours = _converter.LayoutGlyphContours(line, midline, length, bandWidth);
        Assert.NotEmpty(contours);

        // On a circle the midline sits at one constant radius from the centroid — every glyph
        // point should stay within roughly half a band-width of it, i.e. not extend past the
        // border's outer/inner edges.
        var centroid = MeshMath.Centroid(midline);
        float midlineRadius = midline.Average(m => Vector2.Distance(m, centroid));
        foreach (var point in contours.SelectMany(c => c))
        {
            float radialDistance = Vector2.Distance(point, centroid);
            Assert.True(MathF.Abs(radialDistance - midlineRadius) <= bandWidth / 2f + 0.5f);
        }
    }

    [Fact]
    public void LayoutGlyphContours_StartAnchorMode_BeginsNearAnchorAngleInsteadOfCentering()
    {
        var model = new Model { ShapeType = ShapeType.Circle, ShapeSize = 80, BorderThickness = 5, BorderHeight = 3, ShapeThickness = 10 };
        var (outer, inner) = _shapes.GenerateBorderOutline(model);
        var (midline, length) = BorderTextMeshConverter.BuildMidline(outer, inner);
        float bandWidth = BorderTextMeshConverter.BorderBandWidth(outer, inner);
        var centroid = MeshMath.Centroid(midline);

        // "IIII" — no glyph counters, so each character maps to exactly one contour, making the
        // first character's contour unambiguous.
        var centerLine = new BorderTextLine { Content = "IIII", FontName = "Arial", FontSize = 10, Mode = BorderTextMode.Engraved, AnchorAngleDegrees = 90, AnchorMode = BorderTextAnchorMode.Center };
        var startLine = new BorderTextLine { Content = "IIII", FontName = "Arial", FontSize = 10, Mode = BorderTextMode.Engraved, AnchorAngleDegrees = 90, AnchorMode = BorderTextAnchorMode.Start };

        var centerContours = _converter.LayoutGlyphContours(centerLine, midline, length, bandWidth);
        var startContours = _converter.LayoutGlyphContours(startLine, midline, length, bandWidth);

        Assert.Equal(4, centerContours.Count);
        Assert.Equal(4, startContours.Count);

        float centerFirstAngle = GlyphAngleDegrees(centerContours[0], centroid);
        float startFirstAngle = GlyphAngleDegrees(startContours[0], centroid);

        // In Start mode the first glyph should sit right at the anchor angle (90°); in Center
        // mode the whole span (so the first glyph too) sits noticeably earlier than 90°.
        Assert.True(AngularDistanceDegrees(startFirstAngle, 90f) < AngularDistanceDegrees(centerFirstAngle, 90f));
    }

    private static float GlyphAngleDegrees(IReadOnlyList<Vector2> glyphContour, Vector2 centroid)
    {
        var offset = MeshMath.Centroid(glyphContour) - centroid;
        float angle = MathF.Atan2(offset.Y, offset.X) * 180f / MathF.PI;
        return ((angle % 360f) + 360f) % 360f;
    }

    private static float AngularDistanceDegrees(float a, float b)
    {
        float diff = MathF.Abs(a - b) % 360f;
        return diff > 180f ? 360f - diff : diff;
    }
}
