using System.Numerics;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public readonly record struct Transform(Vector3 Position, float RotationZ);

/// <summary>Computes placement for text meshes within the shape bounds. Implemented in Phase 2.</summary>
public interface ITextPositioner
{
    IReadOnlyList<Transform> AutoCenter(IReadOnlyList<Mesh> textMeshes, Model model);
    Transform ApplyManualOffset(TextLine textLine);
    Transform CalculateRelativeCoords(TextLine textLine, Model model);
}
