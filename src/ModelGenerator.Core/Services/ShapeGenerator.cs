using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>Generates base shape meshes (Circle/Triangle/Shield/Rectangle/CustomSvg). Built-in
/// outlines are pure math; CustomSvg uses portable SvgContourExtractor (Svg.Skia).</summary>
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

    public (Mesh Floor, Mesh Border) GenerateParts(Model model) =>
        GenerateParts(model, Array.Empty<IReadOnlyList<Vector2>>(), cutoutDepth: 0);

    public (Mesh Floor, Mesh Border) GenerateParts(
        Model model,
        IReadOnlyList<IReadOnlyList<Vector2>> borderTopCutouts,
        float cutoutDepth)
    {
        var (outer, inner) = GenerateBorderOutline(model);
        return BuildFloorAndBorder(outer, inner, model.ShapeThickness, model.BorderHeight, borderTopCutouts, cutoutDepth);
    }

    public (IReadOnlyList<Vector2> Outer, IReadOnlyList<Vector2> Inner) GenerateBorderOutline(Model model) =>
        model.ShapeType switch
        {
            ShapeType.Circle => GetCircleOutline(model.ShapeSize, model.BorderThickness),
            ShapeType.Rectangle => GetRectangleOutline(model.ShapeSize, model.ShapeHeight, model.BorderThickness),
            ShapeType.Triangle => GetTriangleOutline(model.ShapeSize, model.BorderThickness),
            ShapeType.Shield => GetShieldOutline(model.ShapeSize, model.BorderThickness),
            ShapeType.CustomSvg => GetCustomSvgOutline(RequireCustomShapeSvg(model), model.ShapeSize, model.BorderThickness),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.ShapeType, "Unknown shape type.")
        };

    private static (Mesh Floor, Mesh Border) BuildFloorAndBorder(
        IReadOnlyList<Vector2> outer,
        IReadOnlyList<Vector2> inner,
        float thickness,
        float borderHeight,
        IReadOnlyList<IReadOnlyList<Vector2>> cutouts,
        float cutoutDepth)
    {
        var floor = MeshMath.ExtrudeSolid(outer, 0, thickness);
        var border = cutouts.Count > 0 && cutoutDepth > 0
            ? MeshMath.ExtrudeRingWithTopCutouts(outer, inner, cutouts, thickness, thickness + borderHeight, cutoutDepth)
            : MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight);
        return (floor, border);
    }

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
        var (outer, inner) = GetCircleOutline(diameter, borderThickness);
        return BuildFloorAndBorder(outer, inner, thickness, borderHeight, Array.Empty<IReadOnlyList<Vector2>>(), 0);
    }

    private static (List<Vector2> Outer, List<Vector2> Inner) GetCircleOutline(float diameter, float borderThickness)
    {
        float outerRadius = diameter / 2f;
        float innerRadius = outerRadius - borderThickness;
        if (innerRadius <= 0)
        {
            throw new ArgumentException("Border thickness must be less than the circle's radius.");
        }

        return (CircleOutline(outerRadius), CircleOutline(innerRadius));
    }

    private static (Mesh Floor, Mesh Border) BuildRectangleParts(float width, float height, float thickness, float borderThickness, float borderHeight)
    {
        var (outer, inner) = GetRectangleOutline(width, height, borderThickness);
        return BuildFloorAndBorder(outer, inner, thickness, borderHeight, Array.Empty<IReadOnlyList<Vector2>>(), 0);
    }

    private static (List<Vector2> Outer, List<Vector2> Inner) GetRectangleOutline(float width, float height, float borderThickness)
    {
        if (borderThickness * 2 >= width || borderThickness * 2 >= height)
        {
            throw new ArgumentException("Border thickness is too large for the given rectangle dimensions.");
        }

        return (
            RectangleOutline(width, height),
            RectangleOutline(width - 2 * borderThickness, height - 2 * borderThickness));
    }

    private static (Mesh Floor, Mesh Border) BuildTriangleParts(float size, float thickness, float borderThickness, float borderHeight)
    {
        var (outer, inner) = GetTriangleOutline(size, borderThickness);
        return BuildFloorAndBorder(outer, inner, thickness, borderHeight, Array.Empty<IReadOnlyList<Vector2>>(), 0);
    }

    private static (List<Vector2> Outer, List<Vector2> Inner) GetTriangleOutline(float size, float borderThickness)
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
        return (outer, inner);
    }

    private static (Mesh Floor, Mesh Border) BuildShieldParts(float size, float thickness, float borderThickness, float borderHeight)
    {
        var (outer, inner) = GetShieldOutline(size, borderThickness);
        return BuildFloorAndBorder(outer, inner, thickness, borderHeight, Array.Empty<IReadOnlyList<Vector2>>(), 0);
    }

    private static (List<Vector2> Outer, List<Vector2> Inner) GetShieldOutline(float size, float borderThickness)
    {
        var outer = ShieldOutline(size);
        var inner = RadialInset(outer, borderThickness, "shield");
        return (outer, inner);
    }

    private static (Mesh Floor, Mesh Border) BuildCustomSvgParts(string svgContent, float size, float thickness, float borderThickness, float borderHeight)
    {
        var (outer, inner) = GetCustomSvgOutline(svgContent, size, borderThickness);
        return BuildFloorAndBorder(outer, inner, thickness, borderHeight, Array.Empty<IReadOnlyList<Vector2>>(), 0);
    }

    private static (List<Vector2> Outer, List<Vector2> Inner) GetCustomSvgOutline(string svgContent, float size, float borderThickness)
    {
        var outer = ExtractCustomOutline(svgContent, size);
        var inner = RadialInset(outer, borderThickness, "custom shape");
        return (outer, inner);
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
