using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

public class TextPositioner : ITextPositioner
{
    private const float LineGapFactor = 0.3f;

    /// <summary>Stacks lines top-to-bottom, each horizontally centered on x=0, with the whole
    /// block vertically centered on y=0. Z is set to the shape's top surface.</summary>
    public IReadOnlyList<Transform> AutoCenter(IReadOnlyList<Mesh> textMeshes, Model model)
    {
        var boxes = textMeshes
            .Select(m => MeshMath.BoundingBox(m.Vertices.Select(v => new Vector2(v.X, v.Y))))
            .ToList();

        float gap = boxes.Count > 0 ? boxes.Max(b => b.Max.Y - b.Min.Y) * LineGapFactor : 0f;
        float totalHeight = boxes.Sum(b => b.Max.Y - b.Min.Y) + gap * Math.Max(0, boxes.Count - 1);

        var transforms = new List<Transform>(boxes.Count);
        float cursorTop = totalHeight / 2f;

        foreach (var (min, max) in boxes)
        {
            float lineHeight = max.Y - min.Y;
            float lineCenterY = cursorTop - lineHeight / 2f;
            float centerX = (min.X + max.X) / 2f;
            float centerY = (min.Y + max.Y) / 2f;

            var offset = new Vector3(-centerX, lineCenterY - centerY, model.ShapeThickness);
            transforms.Add(new Transform(offset, 0f));

            cursorTop -= lineHeight + gap;
        }

        return transforms;
    }

    /// <summary>Uses the text line's stored X/Y/Z as absolute world coordinates — the coordinate
    /// space the live 3D preview's drag-and-drop operates in.</summary>
    public Transform ApplyManualOffset(TextLine textLine) =>
        new(new Vector3(textLine.PositionX, textLine.PositionY, textLine.PositionZ), DegreesToRadians(textLine.RotationZ));

    /// <summary>Uses the text line's stored X/Y as an offset from the shape's center, and Z as an
    /// air-gap above the shape's top surface — re-appliable if the shape is resized.</summary>
    public Transform CalculateRelativeCoords(TextLine textLine, Model model) =>
        new(
            new Vector3(textLine.PositionX, textLine.PositionY, model.ShapeThickness + textLine.PositionZ),
            DegreesToRadians(textLine.RotationZ));

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;
}
