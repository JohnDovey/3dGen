using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public interface IShapeGenerator
{
    Mesh GenerateCircle(float diameter, float thickness, float borderThickness, float borderHeight);
    Mesh GenerateTriangle(float size, float thickness, float borderThickness, float borderHeight);
    Mesh GenerateShield(float size, float thickness, float borderThickness, float borderHeight);
    Mesh GenerateRectangle(float width, float height, float thickness, float borderThickness, float borderHeight);

    /// <summary>Dispatches to the matching Generate* method based on the model's ShapeType.</summary>
    Mesh Generate(Model model);
}
