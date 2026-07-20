using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

/// <summary>Merges the base shape mesh with positioned text meshes into one final mesh. Implemented in Phase 2.</summary>
public interface IMeshComposer
{
    Mesh ComposeModel(Mesh baseMesh, IReadOnlyList<Mesh> textMeshes, IReadOnlyList<Transform> transforms);
    Mesh MergeMeshes(IReadOnlyList<Mesh> meshes);
}
