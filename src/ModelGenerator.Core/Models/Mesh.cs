using System.Numerics;

namespace ModelGenerator.Core.Models;

/// <summary>Plain triangle mesh: flat vertex/normal buffers plus triangle indices.</summary>
public class Mesh
{
    public List<Vector3> Vertices { get; } = new();
    public List<Vector3> Normals { get; } = new();
    public List<int> Indices { get; } = new();

    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        var cross = Vector3.Cross(b - a, c - a);
        // A degenerate (zero-area) triangle — collinear or coincident points, which can arise
        // from tessellation of thin/small features — has a zero cross product. Normalizing that
        // divides by zero and produces NaN/Infinity, which then fails JSON serialization when the
        // mesh is cached. Such a triangle contributes no visible area, so Vector3.Zero is a safe
        // stand-in normal.
        var normal = cross.LengthSquared() > 1e-12f ? Vector3.Normalize(cross) : Vector3.Zero;
        int baseIndex = Vertices.Count;

        Vertices.Add(a);
        Vertices.Add(b);
        Vertices.Add(c);
        Normals.Add(normal);
        Normals.Add(normal);
        Normals.Add(normal);

        Indices.Add(baseIndex);
        Indices.Add(baseIndex + 1);
        Indices.Add(baseIndex + 2);
    }

    /// <summary>Appends another mesh's geometry, offsetting its indices.</summary>
    public void Append(Mesh other)
    {
        int offset = Vertices.Count;
        Vertices.AddRange(other.Vertices);
        Normals.AddRange(other.Normals);
        foreach (var index in other.Indices)
        {
            Indices.Add(index + offset);
        }
    }

    /// <summary>Applies a rigid transform (rotation then translation) to every vertex/normal.</summary>
    public Mesh Transformed(Vector3 translation, float rotationZRadians)
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotationZRadians);
        var result = new Mesh();
        result.Vertices.AddRange(Vertices.Select(v => Vector3.Transform(v, rotation) + translation));
        result.Normals.AddRange(Normals.Select(n => Vector3.Transform(n, rotation)));
        result.Indices.AddRange(Indices);
        return result;
    }
}
