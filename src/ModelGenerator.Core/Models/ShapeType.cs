namespace ModelGenerator.Core.Models;

public enum ShapeType
{
    Circle = 0,
    Triangle = 1,
    Shield = 2,
    Rectangle = 3,

    /// <summary>The shape's outline comes from a library SVG (Model.CustomShapeSvgContent)
    /// instead of one of the built-in outlines. Only the largest contour in the SVG is used as
    /// the outer boundary — see ShapeGenerator.BuildCustomSvgParts.</summary>
    CustomSvg = 4
}
