using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.Versioning;
using Svg;

namespace ModelGenerator.Core.Utilities;

/// <summary>Extracts flattened 2D contours (document space, Y-up) from every visible element of an
/// SVG document — shared by SvgMeshConverter (every contour, so embossed inserts keep their
/// holes/cutouts) and ShapeGenerator's CustomSvg shape (the single largest contour, used as the
/// shape's outer boundary).</summary>
[SupportedOSPlatform("windows")]
public static class SvgContourExtractor
{
    public static List<List<Vector2>> ExtractContours(string svgContent)
    {
        var document = SvgDocument.FromSvg<SvgDocument>(svgContent);

        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = SvgRenderer.FromGraphics(graphics);

        var contours = new List<List<Vector2>>();
        CollectContours(document, new Matrix(), renderer, contours);
        return contours;
    }

    /// <summary>Recursively walks the SVG element tree, accumulating each element's transform, and
    /// extracts every visible SvgVisualElement's outline (already transformed into document space)
    /// into the shared contours list.</summary>
    private static void CollectContours(SvgElement element, Matrix parentTransform, ISvgRenderer renderer, List<List<Vector2>> contours)
    {
        var localTransform = (Matrix)parentTransform.Clone();
        var elementMatrix = element.Transforms?.GetMatrix();
        if (elementMatrix is not null)
        {
            localTransform.Multiply(elementMatrix, MatrixOrder.Prepend);
        }

        if (element is SvgVisualElement { Visible: true } visual)
        {
            var path = visual.Path(renderer);
            if (path is not null)
            {
                using var transformedPath = (GraphicsPath)path.Clone();
                transformedPath.Transform(localTransform);
                contours.AddRange(GraphicsPathContours.ExtractContours(transformedPath));
            }
        }

        foreach (var child in element.Children)
        {
            CollectContours(child, localTransform, renderer, contours);
        }
    }
}
