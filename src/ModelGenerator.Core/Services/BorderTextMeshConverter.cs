using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;
using SkiaSharp;

namespace ModelGenerator.Core.Services;

/// <summary>Lays out embossed (or cutout-contour) text along the raised border's midline.</summary>
public sealed class BorderTextMeshConverter : IBorderTextMeshConverter
{
    public Mesh ConvertBorderTextToMesh(
        BorderTextLine borderText,
        IReadOnlyList<Vector2> borderOuter,
        IReadOnlyList<Vector2> borderInner,
        float borderTopZ)
    {
        if (borderText.Mode == BorderTextMode.Engraved || string.IsNullOrEmpty(borderText.Content))
        {
            // Engraved geometry is cut from the border mesh itself, not a separate solid.
            return new Mesh();
        }

        var (midline, length) = BuildMidline(borderOuter, borderInner);
        float bandWidth = BorderBandWidth(borderOuter, borderInner);
        var contours = LayoutGlyphContours(borderText, midline, length, bandWidth);
        if (contours.Count == 0)
        {
            return new Mesh();
        }

        return MeshMath.ExtrudeContours(contours, borderTopZ, borderTopZ + borderText.Height);
    }

    public IReadOnlyList<Mesh> ConvertMultipleBorderTextLines(
        IReadOnlyList<BorderTextLine> borderTextLines,
        IReadOnlyList<Vector2> borderOuter,
        IReadOnlyList<Vector2> borderInner,
        float borderTopZ) =>
        borderTextLines.Select(l => ConvertBorderTextToMesh(l, borderOuter, borderInner, borderTopZ)).ToList();

    public IReadOnlyList<IReadOnlyList<Vector2>> LayoutGlyphContours(
        BorderTextLine borderText,
        IReadOnlyList<Vector2> borderMidline,
        float totalMidlineLength,
        float bandWidth)
    {
        if (string.IsNullOrEmpty(borderText.Content) || borderMidline.Count < 2 || totalMidlineLength <= 0)
        {
            return Array.Empty<IReadOnlyList<Vector2>>();
        }

        using var typeface = SkiaFontResolver.ResolveTypeface(borderText.FontName);
        float naturalFontSize = borderText.FontSize;
        using var font = new SKFont(typeface, naturalFontSize)
        {
            Edging = SKFontEdging.Alias,
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = false
        };

        ushort[] glyphs = font.GetGlyphs(borderText.Content);
        if (glyphs.Length == 0)
        {
            return Array.Empty<IReadOnlyList<Vector2>>();
        }

        float[] widths = font.GetGlyphWidths(glyphs.AsSpan(), paint: null);
        float naturalWidth = 0;
        for (int i = 0; i < widths.Length; i++)
        {
            naturalWidth += widths[i];
        }

        // Natural size; shrink only if the string would wrap more than once around the loop...
        float widthScale = 1f;
        if (naturalWidth > totalMidlineLength && naturalWidth > 1e-6f)
        {
            widthScale = totalMidlineLength / naturalWidth;
        }

        // ...or if it would poke out past the border's outer/inner edges — ascent+descent (not
        // just this string's own glyphs) so the shrink stays stable regardless of which
        // characters happen to be typed.
        float heightScale = 1f;
        if (bandWidth > 0)
        {
            var naturalMetrics = font.Metrics;
            float naturalVerticalExtent = -naturalMetrics.Ascent + naturalMetrics.Descent;
            if (naturalVerticalExtent > bandWidth && naturalVerticalExtent > 1e-6f)
            {
                heightScale = bandWidth / naturalVerticalExtent;
            }
        }

        float fontSize = naturalFontSize * MathF.Min(widthScale, heightScale);

        using var scaledFont = new SKFont(typeface, fontSize)
        {
            Edging = SKFontEdging.Alias,
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = false
        };
        widths = scaledFont.GetGlyphWidths(glyphs.AsSpan(), paint: null);
        float spanWidth = 0;
        for (int i = 0; i < widths.Length; i++)
        {
            spanWidth += widths[i];
        }

        // Recenter the (possibly shrunk) glyphs on the midline using the font's own metrics
        // rather than this specific string's rendered bounds, so baseline placement stays
        // consistent regardless of which characters are typed.
        var scaledMetrics = scaledFont.Metrics;
        float verticalShift = (scaledMetrics.Ascent + scaledMetrics.Descent) / 2f;

        // Arc-length table on the closed midline.
        var (cum, totalLen) = BuildArcLengthTable(borderMidline);
        if (totalLen <= 0)
        {
            return Array.Empty<IReadOnlyList<Vector2>>();
        }

        // Place the span relative to AnchorAngleDegrees (0 = +X, CCW) — either centered on it, or
        // starting at it (text then runs CCW from there).
        float anchorAngleRad = borderText.AnchorAngleDegrees * MathF.PI / 180f;
        // Sample a point on the approximate circle at that angle to find arc position via nearest point.
        float targetArc = ArcLengthAtAngle(borderMidline, cum, totalLen, anchorAngleRad);
        float startArc = borderText.AnchorMode == BorderTextAnchorMode.Start
            ? targetArc
            : targetArc - spanWidth * 0.5f;
        // Normalize into [0, totalLen)
        startArc = ((startArc % totalLen) + totalLen) % totalLen;

        var result = new List<IReadOnlyList<Vector2>>();
        float cursor = startArc;
        for (int g = 0; g < glyphs.Length; g++)
        {
            float advance = widths[g];
            float glyphCenterArc = cursor + advance * 0.5f;
            glyphCenterArc = ((glyphCenterArc % totalLen) + totalLen) % totalLen;

            var (position, tangent) = SampleMidline(borderMidline, cum, totalLen, glyphCenterArc);
            // Rotate so local +X follows tangent; local +Y is left of tangent (outward-ish for CCW loop).
            float angle = MathF.Atan2(tangent.Y, tangent.X);
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            using var glyphPath = scaledFont.GetGlyphPath(glyphs[g]);
            if (glyphPath is null || glyphPath.IsEmpty)
            {
                cursor += advance;
                continue;
            }

            // Center glyph on its advance box roughly: Skia glyph paths are relative to baseline origin.
            // Shift so glyph sits centered on the sample point along X and baseline on the midline.
            var localContours = SkiaPathContours.ExtractContours(glyphPath);
            float halfAdvance = advance * 0.5f;
            foreach (var contour in localContours)
            {
                var world = new List<Vector2>(contour.Count);
                foreach (var p in contour)
                {
                    // Local: X along baseline, Y up (SkiaPathContours already negated Y from Skia).
                    float lx = p.X - halfAdvance;
                    float ly = p.Y + verticalShift;
                    float wx = position.X + lx * cos - ly * sin;
                    float wy = position.Y + lx * sin + ly * cos;
                    world.Add(new Vector2(wx, wy));
                }
                if (world.Count >= 3)
                {
                    result.Add(world);
                }
            }

            cursor += advance;
        }

        return result;
    }

    public static (List<Vector2> Midline, float Length) BuildMidline(
        IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner)
    {
        int n = Math.Min(outer.Count, inner.Count);
        var midline = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            midline.Add((outer[i] + inner[i]) * 0.5f);
        }

        float length = 0;
        for (int i = 0; i < n; i++)
        {
            length += Vector2.Distance(midline[i], midline[(i + 1) % n]);
        }

        return (midline, length);
    }

    /// <summary>Average distance between corresponding outer/inner points — the border's radial
    /// width, i.e. how tall glyphs can be before they poke out past its outer or inner edge.</summary>
    public static float BorderBandWidth(IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner)
    {
        int n = Math.Min(outer.Count, inner.Count);
        if (n == 0)
        {
            return 0f;
        }

        float sum = 0;
        for (int i = 0; i < n; i++)
        {
            sum += Vector2.Distance(outer[i], inner[i]);
        }
        return sum / n;
    }

    private static (float[] Cumulative, float Total) BuildArcLengthTable(IReadOnlyList<Vector2> loop)
    {
        int n = loop.Count;
        var cum = new float[n + 1];
        cum[0] = 0;
        for (int i = 0; i < n; i++)
        {
            cum[i + 1] = cum[i] + Vector2.Distance(loop[i], loop[(i + 1) % n]);
        }
        return (cum, cum[n]);
    }

    private static (Vector2 Position, Vector2 Tangent) SampleMidline(
        IReadOnlyList<Vector2> loop, float[] cum, float total, float arc)
    {
        arc = ((arc % total) + total) % total;
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            if (arc <= cum[i + 1] || i == n - 1)
            {
                float segLen = cum[i + 1] - cum[i];
                float t = segLen > 1e-8f ? (arc - cum[i]) / segLen : 0;
                var a = loop[i];
                var b = loop[(i + 1) % n];
                var pos = Vector2.Lerp(a, b, t);
                var tan = b - a;
                if (tan.LengthSquared() < 1e-12f)
                {
                    tan = new Vector2(1, 0);
                }
                else
                {
                    tan = Vector2.Normalize(tan);
                }
                return (pos, tan);
            }
        }

        return (loop[0], new Vector2(1, 0));
    }

    /// <summary>Finds the arc length along the midline closest to the ray at the given angle from origin.</summary>
    private static float ArcLengthAtAngle(
        IReadOnlyList<Vector2> loop, float[] cum, float total, float angleRad)
    {
        var targetDir = new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad));
        float bestDot = float.MinValue;
        float bestArc = 0;
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            var p = loop[i];
            if (p.LengthSquared() < 1e-12f)
            {
                continue;
            }

            float dot = Vector2.Dot(Vector2.Normalize(p), targetDir);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestArc = cum[i];
            }
        }

        return bestArc;
    }
}
