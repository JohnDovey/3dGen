using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

public class ShapeGenerator : IShapeGenerator
{
    private const int CircleSegments = 64;

    public Mesh Generate(Model model) => model.ShapeType switch
    {
        ShapeType.Circle => GenerateCircle(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Rectangle => GenerateRectangle(model.ShapeSize, model.ShapeHeight, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Triangle => GenerateTriangle(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        ShapeType.Shield => GenerateShield(model.ShapeSize, model.ShapeThickness, model.BorderThickness, model.BorderHeight),
        _ => throw new ArgumentOutOfRangeException(nameof(model), model.ShapeType, "Unknown shape type.")
    };

    public Mesh GenerateCircle(float diameter, float thickness, float borderThickness, float borderHeight)
    {
        float outerRadius = diameter / 2f;
        float innerRadius = outerRadius - borderThickness;
        if (innerRadius <= 0)
        {
            throw new ArgumentException("Border thickness must be less than the circle's radius.");
        }

        var outer = CircleOutline(outerRadius);
        var inner = CircleOutline(innerRadius);

        var mesh = MeshMath.ExtrudeSolid(outer, 0, thickness);
        mesh.Append(MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight));
        return mesh;
    }

    public Mesh GenerateRectangle(float width, float height, float thickness, float borderThickness, float borderHeight)
    {
        if (borderThickness * 2 >= width || borderThickness * 2 >= height)
        {
            throw new ArgumentException("Border thickness is too large for the given rectangle dimensions.");
        }

        var outer = RectangleOutline(width, height);
        var inner = RectangleOutline(width - 2 * borderThickness, height - 2 * borderThickness);

        var mesh = MeshMath.ExtrudeSolid(outer, 0, thickness);
        mesh.Append(MeshMath.ExtrudeRing(outer, inner, thickness, thickness + borderHeight));
        return mesh;
    }

    public Mesh GenerateTriangle(float size, float thickness, float borderThickness, float borderHeight)
    {
        // TODO(Phase 2): implement triangle outline + inset border to match Circle/Rectangle.
        throw new NotImplementedException("Triangle shape generation is planned for Phase 2.");
    }

    public Mesh GenerateShield(float size, float thickness, float borderThickness, float borderHeight)
    {
        // TODO(Phase 2): implement shield outline + inset border to match Circle/Rectangle.
        throw new NotImplementedException("Shield shape generation is planned for Phase 2.");
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
}
