using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;
using SkiaSharp;

namespace ModelGenerator.Core.Services;

/// <summary>
/// Rasterizes text to embossed 3D geometry using SkiaSharp glyph outlines, shared contour
/// extraction (<see cref="SkiaPathContours"/>), and shared tessellation/extrusion
/// (<see cref="MeshMath.ExtrudeContours"/>) — including glyph counters (the holes in letters
/// like O/A/B). Cross-platform (Windows/macOS/Linux) via Skia.
/// </summary>
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

        using var typeface = SkiaFontResolver.ResolveTypeface(fontName);
        // Size is in user units (mm), matching the previous GDI+ GraphicsUnit.World convention.
        using var font = new SKFont(typeface, emSize)
        {
            // Outline extraction should not depend on raster hinting — keeps contours stable across OSes.
            Edging = SKFontEdging.Alias,
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = false
        };

        using var path = font.GetTextPath(text, new SKPoint(0, 0));
        return SkiaPathContours.ExtractContours(path);
    }
}
