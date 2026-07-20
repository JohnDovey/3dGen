using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public class MeshComposer : IMeshComposer
{
    public Mesh ComposeModel(Mesh baseMesh, IReadOnlyList<Mesh> textMeshes, IReadOnlyList<Transform> transforms)
    {
        if (textMeshes.Count != transforms.Count)
        {
            throw new ArgumentException("Must supply exactly one transform per text mesh.");
        }

        var result = new Mesh();
        result.Append(baseMesh);
        for (int i = 0; i < textMeshes.Count; i++)
        {
            result.Append(textMeshes[i].Transformed(transforms[i].Position, transforms[i].RotationZ));
        }
        return result;
    }

    public Mesh MergeMeshes(IReadOnlyList<Mesh> meshes)
    {
        var result = new Mesh();
        foreach (var mesh in meshes)
        {
            result.Append(mesh);
        }
        return result;
    }
}
