using System.Numerics;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public readonly record struct Transform(Vector3 Position, float RotationZ);

/// <summary>Computes placement for text/SVG meshes within the shape bounds.</summary>
public interface ITextPositioner
{
    IReadOnlyList<Transform> AutoCenter(IReadOnlyList<Mesh> meshes, Model model);
    Transform ApplyManualOffset(IPositionable item);
    Transform CalculateRelativeCoords(IPositionable item, Model model);
}
