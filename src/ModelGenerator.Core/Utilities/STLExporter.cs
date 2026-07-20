using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Utilities;

/// <summary>Writes a Mesh to a binary STL file (the common format accepted by 3D print slicers).</summary>
public static class STLExporter
{
    public static void ExportToSTL(Mesh mesh, string filePath)
    {
        int triangleCount = mesh.Indices.Count / 3;

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write(new byte[80]); // header, unused
        writer.Write((uint)triangleCount);

        for (int t = 0; t < triangleCount; t++)
        {
            int i0 = mesh.Indices[t * 3];
            int i1 = mesh.Indices[t * 3 + 1];
            int i2 = mesh.Indices[t * 3 + 2];

            var v0 = mesh.Vertices[i0];
            var v1 = mesh.Vertices[i1];
            var v2 = mesh.Vertices[i2];
            var normal = mesh.Normals[i0];

            writer.Write(normal.X);
            writer.Write(normal.Y);
            writer.Write(normal.Z);

            writer.Write(v0.X); writer.Write(v0.Y); writer.Write(v0.Z);
            writer.Write(v1.X); writer.Write(v1.Y); writer.Write(v1.Z);
            writer.Write(v2.X); writer.Write(v2.Y); writer.Write(v2.Z);

            writer.Write((ushort)0); // attribute byte count, unused
        }
    }
}
