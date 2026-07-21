using System.Numerics;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public interface IBorderTextMeshConverter
{
    Mesh ConvertBorderTextToMesh(
        BorderTextLine borderText,
        IReadOnlyList<Vector2> borderOuter,
        IReadOnlyList<Vector2> borderInner,
        float borderTopZ);

    IReadOnlyList<Mesh> ConvertMultipleBorderTextLines(
        IReadOnlyList<BorderTextLine> borderTextLines,
        IReadOnlyList<Vector2> borderOuter,
        IReadOnlyList<Vector2> borderInner,
        float borderTopZ);

    /// <summary>Glyph contours laid out on the border midline in world XY (for engraved cutouts).
    /// bandWidth is the border's radial width (outer-to-inner distance) — font size is shrunk (in
    /// addition to any circumference-driven shrink) and vertically centered so glyphs stay within
    /// it instead of poking out past the border's outer/inner edges.</summary>
    IReadOnlyList<IReadOnlyList<Vector2>> LayoutGlyphContours(
        BorderTextLine borderText,
        IReadOnlyList<Vector2> borderMidline,
        float totalMidlineLength,
        float bandWidth);
}
