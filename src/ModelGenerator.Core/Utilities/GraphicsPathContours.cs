using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.Versioning;

namespace ModelGenerator.Core.Utilities;

/// <summary>Extracts flattened 2D contours from a GDI+ GraphicsPath — shared by any converter
/// that turns vector outlines (font glyphs, SVG shapes) into extrudable polygon contours.</summary>
[SupportedOSPlatform("windows")]
public static class GraphicsPathContours
{
    private const float DefaultFlattenTolerance = 0.05f;

    /// <summary>Flattens the path (curves become line segments) and splits it into contours at
    /// each subpath boundary. Both GDI+ and SVG coordinate systems grow Y downward; this negates
    /// Y so callers get contours directly in Y-up world space.</summary>
    public static List<List<Vector2>> ExtractContours(GraphicsPath path, float flattenTolerance = DefaultFlattenTolerance)
    {
        path.Flatten(null, flattenTolerance);

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
            // Negate Y: GDI+/SVG path coordinates grow downward; world coordinates are Y-up.
            current.Add(new Vector2(points[i].X, -points[i].Y));
        }

        return contours.Where(c => c.Count >= 3).ToList();
    }
}
