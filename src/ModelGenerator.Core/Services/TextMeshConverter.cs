using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.Versioning;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>
/// Rasterizes text to embossed 3D geometry using GDI+ glyph outlines (System.Drawing), shared
/// contour extraction (<see cref="GraphicsPathContours"/>), and shared tessellation/extrusion
/// (<see cref="MeshMath.ExtrudeContours"/>) — including glyph counters (the holes in letters
/// like O/A/B). Windows-only for v1; swap this implementation when porting the font/tessellation
/// layer to Mac/Linux.
/// </summary>
[SupportedOSPlatform("windows")]
public class TextMeshConverter : ITextMeshConverter
{
    public Mesh ConvertTextToMesh(TextLine textLine)
    {
        var contours = ExtractGlyphContours(textLine.Content, textLine.FontName, textLine.FontSize);
        return MeshMath.ExtrudeContours(contours, 0, textLine.TextHeight);
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

        return GraphicsPathContours.ExtractContours(path);
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
}
