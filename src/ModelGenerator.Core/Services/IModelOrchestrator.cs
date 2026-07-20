using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

/// <summary>Orchestrates the full shape -> text -> composed mesh -> STL workflow. Implemented in Phase 2/3.</summary>
public interface IModelOrchestrator
{
    Mesh GenerateModel(Model model);
    void ExportSTL(Mesh mesh, string filePath);
}
