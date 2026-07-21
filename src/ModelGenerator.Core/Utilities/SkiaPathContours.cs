using System.Numerics;
using SkiaSharp;

namespace ModelGenerator.Core.Utilities;

/// <summary>Flattens a Skia <see cref="SKPath"/> into Y-up polygon contours suitable for
/// <see cref="MeshMath.ExtrudeContours"/>. Shared by text (glyph outlines) and SVG path extraction.
/// Curves are subdivided until they fall within a linear tolerance (same role as GDI+
/// <c>GraphicsPath.Flatten</c> in the previous Windows-only pipeline).</summary>
public static class SkiaPathContours
{
    private const float DefaultFlattenTolerance = 0.05f;

    /// <summary>Walks path verbs, flattens curves to line segments, splits on Move, and negates Y
    /// so callers get contours in Y-up world space (Skia/SVG grow Y downward).</summary>
    public static List<List<Vector2>> ExtractContours(SKPath path, float flattenTolerance = DefaultFlattenTolerance)
    {
        var contours = new List<List<Vector2>>();
        List<Vector2>? current = null;
        SKPoint last = default;

        using var iterator = path.CreateRawIterator();
        var points = new SKPoint[4];
        SKPathVerb verb;
        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    current = new List<Vector2>();
                    contours.Add(current);
                    last = points[0];
                    current.Add(ToWorld(points[0]));
                    break;

                case SKPathVerb.Line:
                    last = points[1];
                    current?.Add(ToWorld(points[1]));
                    break;

                case SKPathVerb.Quad:
                    FlattenQuad(current, last, points[1], points[2], flattenTolerance);
                    last = points[2];
                    break;

                case SKPathVerb.Conic:
                    // Treat conics as quads (weight ignored) — good enough for embossed outlines.
                    FlattenQuad(current, last, points[1], points[2], flattenTolerance);
                    last = points[2];
                    break;

                case SKPathVerb.Cubic:
                    FlattenCubic(current, last, points[1], points[2], points[3], flattenTolerance);
                    last = points[3];
                    break;

                case SKPathVerb.Close:
                    break;
            }
        }

        return contours.Where(c => c.Count >= 3).ToList();
    }

    private static Vector2 ToWorld(SKPoint p) => new(p.X, -p.Y);

    private static void FlattenQuad(List<Vector2>? current, SKPoint p0, SKPoint p1, SKPoint p2, float tol, int depth = 0)
    {
        if (current is null)
        {
            return;
        }

        float dx = p2.X - p0.X;
        float dy = p2.Y - p0.Y;
        float d = Math.Abs((p1.X - p2.X) * dy - (p1.Y - p2.Y) * dx);
        if (d * d < tol * tol * (dx * dx + dy * dy) || depth > 8)
        {
            current.Add(ToWorld(p2));
            return;
        }

        var p01 = Mid(p0, p1);
        var p12 = Mid(p1, p2);
        var p012 = Mid(p01, p12);
        FlattenQuad(current, p0, p01, p012, tol, depth + 1);
        FlattenQuad(current, p012, p12, p2, tol, depth + 1);
    }

    private static void FlattenCubic(List<Vector2>? current, SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, float tol, int depth = 0)
    {
        if (current is null)
        {
            return;
        }

        float dx = p3.X - p0.X;
        float dy = p3.Y - p0.Y;
        float d1 = Math.Abs((p1.X - p3.X) * dy - (p1.Y - p3.Y) * dx);
        float d2 = Math.Abs((p2.X - p3.X) * dy - (p2.Y - p3.Y) * dx);
        if ((d1 + d2) * (d1 + d2) < tol * tol * (dx * dx + dy * dy) || depth > 8)
        {
            current.Add(ToWorld(p3));
            return;
        }

        var p01 = Mid(p0, p1);
        var p12 = Mid(p1, p2);
        var p23 = Mid(p2, p3);
        var p012 = Mid(p01, p12);
        var p123 = Mid(p12, p23);
        var p0123 = Mid(p012, p123);
        FlattenCubic(current, p0, p01, p012, p0123, tol, depth + 1);
        FlattenCubic(current, p0123, p123, p23, p3, tol, depth + 1);
    }

    private static SKPoint Mid(SKPoint a, SKPoint b) => new((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);
}
