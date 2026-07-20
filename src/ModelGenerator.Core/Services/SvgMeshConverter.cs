using System.Numerics;
using System.Runtime.Versioning;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>
/// Converts an SVG document to embossed 3D geometry using the shared SVG contour-extraction
/// (<see cref="SvgContourExtractor"/>) and tessellation/extrusion pipeline
/// (<see cref="MeshMath.ExtrudeContours"/>) — every SvgVisualElement's outline in the document is
/// combined into one tessellation pass, so holes/cutouts (e.g. a logo with a cutout) are handled
/// correctly via the nonzero winding rule, the same way a glyph counter is. Windows-only for v1,
/// same as TextMeshConverter (depends on System.Drawing/GDI+).
/// </summary>
[SupportedOSPlatform("windows")]
public class SvgMeshConverter : ISvgMeshConverter
{
    public Mesh ConvertSvgToMesh(SvgInsert insert)
    {
        var contours = SvgContourExtractor.ExtractContours(insert.SvgContent);
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
}
