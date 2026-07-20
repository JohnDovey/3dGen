using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.Versioning;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;
using Svg;

namespace ModelGenerator.Core.Services;

/// <summary>
/// Converts an SVG document to embossed 3D geometry using the same shared contour-extraction and
/// tessellation/extrusion pipeline as TextMeshConverter (GraphicsPathContours + MeshMath.
/// ExtrudeContours) — every SvgVisualElement's outline in the document is combined into one
/// tessellation pass, so holes/cutouts (e.g. a logo with a cutout) are handled correctly via the
/// nonzero winding rule, the same way a glyph counter is. Windows-only for v1, same as
/// TextMeshConverter (depends on System.Drawing/GDI+).
/// </summary>
[SupportedOSPlatform("windows")]
public class SvgMeshConverter : ISvgMeshConverter
{
    public Mesh ConvertSvgToMesh(SvgInsert insert)
    {
        var document = SvgDocument.FromSvg<SvgDocument>(insert.SvgContent);

        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = SvgRenderer.FromGraphics(graphics);

        var contours = new List<List<Vector2>>();
        CollectContours(document, new Matrix(), renderer, contours);

        if (contours.Count == 0)
        {
            return new Mesh();
        }

        var (min, max) = MeshMath.BoundingBox(contours.SelectMany(c => c));
        float maxDimension = MathF.Max(max.X - min.X, max.Y - min.Y);
        float scaleFactor = maxDimension > 0 ? insert.Scale / maxDimension : 1f;

        var scaledContours = contours
            .Select(contour => (IReadOnlyList<Vector2>)contour.Select(p => p * scaleFactor).ToList())
            .ToList();

        return MeshMath.ExtrudeContours(scaledContours, 0, insert.EmbossHeight);
    }

    public IReadOnlyList<Mesh> ConvertMultipleSvgInserts(IReadOnlyList<SvgInsert> inserts) =>
        inserts.Select(ConvertSvgToMesh).ToList();

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
