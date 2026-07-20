using System.Numerics;
using System.Text.Json;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Data.Repository;

/// <summary>Maps between Mesh and the flat JSON arrays stored in MeshCache
/// (System.Numerics.Vector3's fields aren't serialized by System.Text.Json by default).</summary>
internal static class MeshJson
{
    public static (string VerticesJson, string IndicesJson, string NormalsJson) Serialize(Mesh mesh)
    {
        var vertices = mesh.Vertices.Select(v => new[] { v.X, v.Y, v.Z });
        var normals = mesh.Normals.Select(n => new[] { n.X, n.Y, n.Z });
        return (
            JsonSerializer.Serialize(vertices),
            JsonSerializer.Serialize(mesh.Indices),
            JsonSerializer.Serialize(normals));
    }

    public static Mesh Deserialize(string verticesJson, string indicesJson, string normalsJson)
    {
        var vertices = JsonSerializer.Deserialize<float[][]>(verticesJson) ?? [];
        var indices = JsonSerializer.Deserialize<int[]>(indicesJson) ?? [];
        var normals = JsonSerializer.Deserialize<float[][]>(normalsJson) ?? [];

        var mesh = new Mesh();
        mesh.Vertices.AddRange(vertices.Select(v => new Vector3(v[0], v[1], v[2])));
        mesh.Normals.AddRange(normals.Select(n => new Vector3(n[0], n[1], n[2])));
        mesh.Indices.AddRange(indices);
        return mesh;
    }
}
