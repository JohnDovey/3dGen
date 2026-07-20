using System.Numerics;
using System.Runtime.Versioning;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>Windows-only for v1: the four built-in outlines (Circle/Triangle/Shield/Rectangle)
/// don't need Windows, but CustomSvg's outline extraction depends on System.Drawing/GDI+ via
/// SvgContourExtractor, same as TextMeshConverter/SvgMeshConverter.</summary>
[SupportedOSPlatform("windows")]
public class ShapeGenerator : IShapeGenerator
{
    private const int CircleSegments = 64;

    public Mesh Generate(Model model) => model.ShapeType switch
    {
        ShapeType.Circle => GenerateCircle(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Rectangle => GenerateRectangle(model.ShapeSize, model.ShapeHeight, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Triangle => GenerateTriangle(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Shield => GenerateShield(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.CustomSvg => GenerateCustomSvg(RequireCustomShapeSvg(model), model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        _ => throw new ArgumentOutOfRangeException(nameof(model), model.ShapeType, "Unknown shape type.")
    };

    public (Mesh Floor, Mesh Border) GenerateParts(Model model) => model.ShapeType switch
    {
        ShapeType.Circle => BuildCircleParts(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Rectangle => BuildRectangleParts(model.ShapeSize, model.ShapeHeight, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Triangle => BuildTriangleParts(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Shield => BuildShieldParts(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.CustomSvg => BuildCustomSvgParts(RequireCustomShapeSvg(model), model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        _ => throw new ArgumentOutOfRangeException(nameof(model), model.ShapeType, "Unknown shape type.")
    };

    private static string RequireCustomShapeSvg(Model model)
    {
        if (string.IsNullOrWhiteSpace(model.CustomShapeSvgContent))
        {
            throw new ArgumentException("Choose a custom shape SVG first.");
        }
        return model.CustomShapeSvgContent;
    }

    public Mesh GenerateCircle(float diameter, float thickness, float borderThickness, float borderHeight)
    {
        var (floor, border) = BuildCircleParts(diameter, thickness, borderThickness, borderHeight);
        floor.Append(border);
        return floor;
    }

    public Mesh GenerateRectangle(float width, float height, float thickness, float borderThickness, float borderHeight)
    {
        var (floor, border) = BuildRectangleParts(width, height, thickness, borderThickness, borderHeight);
        floor.Append(border);
        return floor;
    }

    public Mesh GenerateTriangle(float size, float thickness, float borderThickness, float borderHeight)
    {
        var (floor, border) = BuildTriangleParts(size, thickness, borderThickness, borderHeight);
        floor.Append(border);
        return floor;
    }

    public Mesh GenerateShield(float size, float thickness, float borderThickness, float borderHeight)
    {
        var (floor, border) = BuildShieldParts(size, thickness, borderThickness, borderHeight);
        floor.Append(border);
        return floor;
    }

    public Mesh GenerateCustomSvg(string svgContent, float size, float thickness, float borderThickness, float borderHeight)
    {
        var (floor, border) = BuildCustomSvgParts(svgContent, size, thickness, borderThickness, borderHeight);
        floor.Append(border);
        return floor;
    }

    private static (Mesh Floor, Mesh Border) BuildCircleParts(float diameter, float thickness, float borderThickness, float borderHeight)
    {
        float outerRadius = diameter / 2f;
        float innerRadius = outerRadius - borderThickness;
        if (innerRadius <= 0)
        {
            throw new ArgumentException("Border thickness must be less than the circle's radius.");
        }

        var outer = CircleOutline(outerRadius);
        var inner = CircleOutline(innerRadius);

        var floor = MeshMath.ExtrudeSolid(outer, 0, thickness);
        var border = MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight);
        return (floor, border);
    }

    private static (Mesh Floor, Mesh Border) BuildRectangleParts(float width, float height, float thickness, float borderThickness, float borderHeight)
    {
        if (borderThickness * 2 >= width || borderThickness * 2 >= height)
        {
            throw new ArgumentException("Border thickness is too large for the given rectangle dimensions.");
        }

        var outer = RectangleOutline(width, height);
        var inner = RectangleOutline(width - 2 * borderThickness, height - 2 * borderThickness);

        var floor = MeshMath.ExtrudeSolid(outer, 0, thickness);
        var border = MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight);
        return (floor, border);
    }

    private static (Mesh Floor, Mesh Border) BuildTriangleParts(float size, float thickness, float borderThickness, float borderHeight)
    {
        // Equilateral triangle, centroid at the origin, "size" = side length.
        float height = size * MathF.Sqrt(3) / 2f;

        // For a regular polygon, offsetting every edge inward by a constant distance is the
        // same as scaling about the centroid by (inradius - offset) / inradius — the incenter
        // and centroid coincide for a regular polygon, so this gives an exact constant-width
        // border (unlike the radial vertex-distance approach used for the irregular Shield).
        float inradius = height / 3f;
        if (borderThickness >= inradius)
        {
            throw new ArgumentException("Border thickness is too large for this triangle's size.");
        }

        var outer = new List<Vector2>
        {
            new(0, 2f * height / 3f),
            new(-size / 2f, -height / 3f),
            new(size / 2f, -height / 3f)
        };

        float scale = (inradius - borderThickness) / inradius;
        var inner = outer.Select(p => p * scale).ToList();

        var floor = MeshMath.ExtrudeSolid(outer, 0, thickness);
        var border = MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight);
        return (floor, border);
    }

    private static (Mesh Floor, Mesh Border) BuildShieldParts(float size, float thickness, float borderThickness, float borderHeight)
    {
        var outer = ShieldOutline(size);
        var inner = RadialInset(outer, borderThickness, "shield");

        var floor = MeshMath.ExtrudeSolid(outer, 0, thickness);
        var border = MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight);
        return (floor, border);
    }

    private static (Mesh Floor, Mesh Border) BuildCustomSvgParts(string svgContent, float size, float thickness, float borderThickness, float borderHeight)
    {
        var outer = ExtractCustomOutline(svgContent, size);
        var inner = RadialInset(outer, borderThickness, "custom shape");

        var floor = MeshMath.ExtrudeSolid(outer, 0, thickness);
        var border = MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight);
        return (floor, border);
    }

    /// <summary>Picks the SVG's largest-area contour as the shape's outer boundary, normalizes its
    /// winding to CCW (matching the other built-in shapes), and fits/centers it so its longer
    /// bounding-box dimension equals size and its bounding-box center sits at the origin. Only
    /// works cleanly for a single simple closed path — for a multi-path SVG, "largest contour
    /// wins" and the rest is silently ignored.</summary>
    private static List<Vector2> ExtractCustomOutline(string svgContent, float size)
    {
        var contours = SvgContourExtractor.ExtractContours(svgContent);
        if (contours.Count == 0)
        {
            throw new ArgumentException("The custom shape SVG has no visible geometry.");
        }

        var outer = contours.OrderByDescending(c => MathF.Abs(MeshMath.SignedArea(c))).First();
        if (MeshMath.SignedArea(outer) < 0)
        {
            outer.Reverse();
        }

        var (min, max) = MeshMath.BoundingBox(outer);
        var center = (min + max) / 2f;
        float maxDimension = MathF.Max(max.X - min.X, max.Y - min.Y);
        float scale = maxDimension > 0 ? size / maxDimension : 1f;

        return outer.Select(p => (p - center) * scale).ToList();
    }

    private static List<Vector2> CircleOutline(float radius)
    {
        var points = new List<Vector2>(CircleSegments);
        for (int i = 0; i < CircleSegments; i++)
        {
            double angle = 2 * Math.PI * i / CircleSegments;
            points.Add(new Vector2(radius * (float)Math.Cos(angle), radius * (float)Math.Sin(angle)));
        }
        return points;
    }

    private static List<Vector2> RectangleOutline(float width, float height)
    {
        float hw = width / 2f;
        float hh = height / 2f;
        return new List<Vector2>
        {
            new(-hw, -hh),
            new(hw, -hh),
            new(hw, hh),
            new(-hw, hh)
        };
    }

    /// <summary>Classic heraldic-shield silhouette: flat top, a slight shoulder flare, tapering
    /// to a single point at the bottom. "size" sets both the top-edge width and, via a fixed
    /// aspect ratio, the overall height.</summary>
    private static List<Vector2> ShieldOutline(float size)
    {
        float hw = size / 2f;
        float topY = size * 0.45f;
        float bottomY = -size * 0.75f;
        float totalHeight = topY - bottomY;
        float shoulderY = topY - totalHeight * 0.15f;
        float midY = topY - totalHeight * 0.55f;

        // CCW winding (matches Circle/Rectangle/Triangle): down the left side, across the
        // bottom point, up the right side, and back along the top edge.
        return new List<Vector2>
        {
            new(-hw, topY),
            new(-hw * 1.05f, shoulderY),
            new(-hw * 0.7f, midY),
            new(0, bottomY),
            new(hw * 0.7f, midY),
            new(hw * 1.05f, shoulderY),
            new(hw, topY)
        };
    }

    /// <summary>Moves each vertex inward toward the centroid by insetDistance. Exact for a
    /// regular polygon; for an irregular outline like the Shield, this is an approximation of a
    /// true constant-width offset — good enough for a decorative embossed border, not
    /// geometrically exact along every edge.</summary>
    private static List<Vector2> RadialInset(IReadOnlyList<Vector2> outline, float insetDistance, string shapeName)
    {
        var centroid = MeshMath.Centroid(outline);
        var result = new List<Vector2>(outline.Count);
        foreach (var p in outline)
        {
            var toPoint = p - centroid;
            float distance = toPoint.Length();
            if (distance <= insetDistance)
            {
                throw new ArgumentException($"Border thickness is too large for this {shapeName}'s size.");
            }
            result.Add(centroid + toPoint * ((distance - insetDistance) / distance));
        }
        return result;
    }
}
