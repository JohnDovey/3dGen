using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.Versioning;
using LibTessDotNet;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>
/// Rasterizes text to embossed 3D geometry using GDI+ glyph outlines (System.Drawing) and
/// LibTessDotNet to tessellate the outlines — including glyph counters (the holes in letters
/// like O/A/B) — into triangles. Windows-only for v1; swap this implementation when porting
/// the font/tessellation layer to Mac/Linux.
/// </summary>
[SupportedOSPlatform("windows")]
public class TextMeshConverter : ITextMeshConverter
{
    private const float FlattenTolerance = 0.05f;

    public Mesh ConvertTextToMesh(TextLine textLine)
    {
        var contours = ExtractGlyphContours(textLine.Content, textLine.FontName, textLine.FontSize);
        return ExtrudeContours(contours, textLine.TextHeight);
    }

    public IReadOnlyList<Mesh> ConvertMultilineText(IReadOnlyList<TextLine> textLines)
    {
        return textLines.Select(ConvertTextToMesh).ToList();
    }

    private static List<List<Vector2>> ExtractGlyphContours(string text, string fontName, float emSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<List<Vector2>>();
        }

        using var fontFamily = ResolveFontFamily(fontName);
        using var path = new GraphicsPath();
        using var stringFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
        stringFormat.FormatFlags |= StringFormatFlags.NoClip;

        // GraphicsUnit.World with emSize in mm means path coordinates come out directly in mm.
        path.AddString(text, fontFamily, (int)FontStyle.Regular, emSize, PointF.Empty, stringFormat);
        path.Flatten(null, FlattenTolerance);

        var points = path.PathPoints;
        var types = path.PathTypes;

        var contours = new List<List<Vector2>>();
        List<Vector2>? current = null;

        for (int i = 0; i < points.Length; i++)
        {
            var pointType = (PathPointType)(types[i] & 0x07);
            if (pointType == PathPointType.Start || current is null)
            {
                current = new List<Vector2>();
                contours.Add(current);
            }
            // Negate Y: GDI+ path coordinates grow downward; world coordinates are Y-up.
            current.Add(new Vector2(points[i].X, -points[i].Y));
        }

        return contours.Where(c => c.Count >= 3).ToList();
    }

    private static FontFamily ResolveFontFamily(string fontName)
    {
        try
        {
            return new FontFamily(fontName);
        }
        catch (ArgumentException)
        {
            return FontFamily.GenericSansSerif;
        }
    }

    private static Mesh ExtrudeContours(List<List<Vector2>> contours, float height)
    {
        var mesh = new Mesh();
        if (contours.Count == 0)
        {
            return mesh;
        }

        var tess = new Tess();
        foreach (var contour in contours)
        {
            var contourVertices = contour
                .Select(p => new ContourVertex { Position = new Vec3 { X = p.X, Y = p.Y, Z = 0 } })
                .ToArray();
            tess.AddContour(contourVertices);
        }
        tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3);

        for (int i = 0; i < tess.ElementCount; i++)
        {
            var v0 = tess.Vertices[tess.Elements[i * 3]].Position;
            var v1 = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
            var v2 = tess.Vertices[tess.Elements[i * 3 + 2]].Position;

            // Top cap.
            mesh.AddTriangle(
                new Vector3(v0.X, v0.Y, height),
                new Vector3(v1.X, v1.Y, height),
                new Vector3(v2.X, v2.Y, height));

            // Bottom cap (reversed winding to face down).
            mesh.AddTriangle(
                new Vector3(v0.X, v0.Y, 0),
                new Vector3(v2.X, v2.Y, 0),
                new Vector3(v1.X, v1.Y, 0));
        }

        // Side walls per contour. Outer glyph boundaries and hole (counter) boundaries are wound
        // in opposite directions by GDI+'s nonzero fill convention, so a single sign check on the
        // contour's own signed area is enough to orient every wall outward.
        foreach (var contour in contours)
        {
            bool outward = MeshMath.SignedArea(contour) > 0;
            MeshMath.AddSideWall(mesh, contour, 0, height, outward);
        }

        return mesh;
    }
}
