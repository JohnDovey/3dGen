using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

/// <summary>One text line's mesh, already positioned/transformed, alongside the line it came from
/// (so a caller — e.g. the UI's viewport — can map a picked mesh back to its TextLine).</summary>
public readonly record struct PositionedTextMesh(TextLine Line, Mesh Mesh);

/// <summary>One SVG insert's mesh, already positioned/transformed, alongside the insert it came
/// from (so a caller can map a picked mesh back to its SvgInsert).</summary>
public readonly record struct PositionedSvgMesh(SvgInsert Insert, Mesh Mesh);

/// <summary>One image insert's mesh, already positioned/transformed, alongside the insert it came
/// from (so a caller can map a picked mesh back to its ImageInsert).</summary>
public readonly record struct PositionedImageMesh(ImageInsert Insert, Mesh Mesh);

/// <summary>Border text entry: embossed lines carry real geometry; engraved lines have an empty
/// mesh (the cut is baked into the border) so the UI still has one row per BorderTextLine.</summary>
public readonly record struct RenderedBorderTextMesh(BorderTextLine Line, Mesh Mesh);

/// <summary>Orchestrates the full shape -> text/svg/image/border-text -> composed mesh -> STL workflow.</summary>
public interface IModelOrchestrator
{
    Mesh GenerateModel(Model model);

    /// <summary>Same generation as GenerateModel, but keeps the shape's floor and border, and
    /// each text/SVG/image/border-text item's mesh, separate instead of merged into one mesh — lets the UI
    /// render/color/hit-test each piece independently (e.g. for drag-to-reposition and per-item
    /// coloring) while still being able to merge everything for STL export.</summary>
    (Mesh Floor, Mesh Border, IReadOnlyList<PositionedTextMesh> TextMeshes, IReadOnlyList<PositionedSvgMesh> SvgMeshes, IReadOnlyList<PositionedImageMesh> ImageMeshes, IReadOnlyList<RenderedBorderTextMesh> BorderTextMeshes) GenerateModelParts(Model model);

    void ExportSTL(Mesh mesh, string filePath);
}
