using System.Numerics;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public interface IShapeGenerator
{
    Mesh GenerateCircle(float diameter, float thickness, float borderThickness, float borderHeight);
    Mesh GenerateTriangle(float size, float thickness, float borderThickness, float borderHeight);
    Mesh GenerateShield(float size, float thickness, float borderThickness, float borderHeight);
    Mesh GenerateRectangle(float width, float height, float thickness, float borderThickness, float borderHeight);

    /// <summary>Uses the largest contour in svgContent as the shape's outer boundary (see
    /// ShapeGenerator.BuildCustomSvgParts for the "largest contour wins" rule and its limits).
    /// size is the target length of the outline's longer bounding-box dimension, mirroring
    /// SvgInsert.Scale.</summary>
    Mesh GenerateCustomSvg(string svgContent, float size, float thickness, float borderThickness, float borderHeight);

    /// <summary>Dispatches to the matching Generate* method based on the model's ShapeType.</summary>
    Mesh Generate(Model model);

    /// <summary>Same shape as Generate(Model), but with the floor slab and the raised border
    /// ring kept as separate meshes instead of merged — lets the UI color/render them
    /// independently. Dispatches by the model's ShapeType, mirroring Generate(Model).</summary>
    (Mesh Floor, Mesh Border) GenerateParts(Model model);

    /// <summary>Border ring with optional top-surface cutouts (engraved border text). Empty
    /// cutouts match <see cref="GenerateParts(Model)"/>.</summary>
    (Mesh Floor, Mesh Border) GenerateParts(
        Model model,
        IReadOnlyList<IReadOnlyList<Vector2>> borderTopCutouts,
        float cutoutDepth);

    /// <summary>Outer/inner 2D polygons of the raised border (same as used for extrusion), without
    /// building meshes — for border-text layout along the midline.</summary>
    (IReadOnlyList<Vector2> Outer, IReadOnlyList<Vector2> Inner) GenerateBorderOutline(Model model);
}
