using System.Numerics;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Host.Protocol;

/// <summary>JSON-friendly mesh: flat float arrays (xyz packed) plus triangle indices.
/// Colors travel separately so STL export can ignore them.</summary>
public sealed class WireMesh
{
    public float[] Vertices { get; set; } = [];
    public float[] Normals { get; set; } = [];
    public int[] Indices { get; set; } = [];
    public int ColorArgb { get; set; }

    public static WireMesh FromMesh(Mesh mesh, int colorArgb = 0)
    {
        var vertices = new float[mesh.Vertices.Count * 3];
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i];
            vertices[i * 3] = v.X;
            vertices[i * 3 + 1] = v.Y;
            vertices[i * 3 + 2] = v.Z;
        }

        var normals = new float[mesh.Normals.Count * 3];
        for (int i = 0; i < mesh.Normals.Count; i++)
        {
            var n = mesh.Normals[i];
            normals[i * 3] = n.X;
            normals[i * 3 + 1] = n.Y;
            normals[i * 3 + 2] = n.Z;
        }

        return new WireMesh
        {
            Vertices = vertices,
            Normals = normals,
            Indices = mesh.Indices.ToArray(),
            ColorArgb = colorArgb
        };
    }

    public Mesh ToMesh()
    {
        var mesh = new Mesh();
        int vertexCount = Vertices.Length / 3;
        for (int i = 0; i < vertexCount; i++)
        {
            mesh.Vertices.Add(new Vector3(Vertices[i * 3], Vertices[i * 3 + 1], Vertices[i * 3 + 2]));
        }

        int normalCount = Normals.Length / 3;
        for (int i = 0; i < normalCount; i++)
        {
            mesh.Normals.Add(new Vector3(Normals[i * 3], Normals[i * 3 + 1], Normals[i * 3 + 2]));
        }

        mesh.Indices.AddRange(Indices);
        return mesh;
    }
}

public sealed class WirePositionedMesh
{
    public int Index { get; set; }
    public int ColorArgb { get; set; }
    public WireMesh Mesh { get; set; } = new();
}

public sealed class GeneratePartsResult
{
    public WireMesh Floor { get; set; } = new();
    public WireMesh Border { get; set; } = new();
    public List<WirePositionedMesh> TextMeshes { get; set; } = new();
    public List<WirePositionedMesh> SvgMeshes { get; set; } = new();
    public List<WirePositionedMesh> ImageMeshes { get; set; } = new();
    public int VertexCount { get; set; }
    public int TriangleCount { get; set; }
}

public sealed class PingResult
{
    public bool Ok { get; set; } = true;
    public string Version { get; set; } = string.Empty;
    public string Protocol { get; set; } = HostProtocol.Version;
}

public sealed class ExportStlResult
{
    public string Path { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public int VertexCount { get; set; }
    public int TriangleCount { get; set; }
}

public sealed class ModelSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ShapeType { get; set; }
    public DateTime ModifiedDate { get; set; }
}

public sealed class ListModelsResult
{
    public List<ModelSummaryDto> Models { get; set; } = new();
}

public sealed class GetModelResult
{
    public Model Model { get; set; } = new();
}

public sealed class SaveModelResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class DeleteModelResult
{
    public int Id { get; set; }
    public bool Deleted { get; set; }
}
