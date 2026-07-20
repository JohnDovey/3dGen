using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

/// <summary>One text line's mesh, already positioned/transformed, alongside the line it came from
/// (so a caller — e.g. the UI's viewport — can map a picked mesh back to its TextLine).</summary>
public readonly record struct PositionedTextMesh(TextLine Line, Mesh Mesh);

/// <summary>Orchestrates the full shape -> text -> composed mesh -> STL workflow.</summary>
public interface IModelOrchestrator
{
    Mesh GenerateModel(Model model);

    /// <summary>Same generation as GenerateModel, but returns the base shape and each text
    /// line's mesh separately instead of merged into one mesh — lets the UI render/hit-test
    /// individual text lines (e.g. for drag-to-reposition) while still being able to merge them
    /// for STL export.</summary>
    (Mesh BaseMesh, IReadOnlyList<PositionedTextMesh> TextMeshes) GenerateModelParts(Model model);

    void ExportSTL(Mesh mesh, string filePath);
}
