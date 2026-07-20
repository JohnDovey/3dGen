using System.Numerics;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Utilities;

/// <summary>Extrusion helpers shared by the shape generators.</summary>
public static class MeshMath
{
    /// <summary>Extrudes a simple, convex (or star-shaped-from-centroid) 2D outline into a solid
    /// slab between zBottom and zTop, with flat top/bottom caps and vertical side walls.</summary>
    public static Mesh ExtrudeSolid(IReadOnlyList<Vector2> outline, float zBottom, float zTop)
    {
        var mesh = new Mesh();
        var centroid = Centroid(outline);

        // Bottom cap (fan from centroid, wound so the normal faces down).
        for (int i = 0; i < outline.Count; i++)
        {
            var p1 = outline[i];
            var p2 = outline[(i + 1) % outline.Count];
            mesh.AddTriangle(
                new Vector3(centroid.X, centroid.Y, zBottom),
                new Vector3(p2.X, p2.Y, zBottom),
                new Vector3(p1.X, p1.Y, zBottom));
        }

        // Top cap (fan from centroid, wound so the normal faces up).
        for (int i = 0; i < outline.Count; i++)
        {
            var p1 = outline[i];
            var p2 = outline[(i + 1) % outline.Count];
            mesh.AddTriangle(
                new Vector3(centroid.X, centroid.Y, zTop),
                new Vector3(p1.X, p1.Y, zTop),
                new Vector3(p2.X, p2.Y, zTop));
        }

        AddSideWall(mesh, outline, zBottom, zTop, outward: true);

        return mesh;
    }

    /// <summary>Extrudes the frame between two matching (same point count, same winding) outlines
    /// into a solid ring between zBottom and zTop — used for the embossed border.</summary>
    public static Mesh ExtrudeRing(IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner, float zBottom, float zTop)
    {
        if (outer.Count != inner.Count)
        {
            throw new ArgumentException("Outer and inner outlines must have the same point count.");
        }

        var mesh = new Mesh();
        int n = outer.Count;

        // Bottom ring (facing down).
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            mesh.AddTriangle(
                new Vector3(outer[i].X, outer[i].Y, zBottom),
                new Vector3(outer[j].X, outer[j].Y, zBottom),
                new Vector3(inner[j].X, inner[j].Y, zBottom));
            mesh.AddTriangle(
                new Vector3(outer[i].X, outer[i].Y, zBottom),
                new Vector3(inner[j].X, inner[j].Y, zBottom),
                new Vector3(inner[i].X, inner[i].Y, zBottom));
        }

        // Top ring (facing up).
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            mesh.AddTriangle(
                new Vector3(outer[i].X, outer[i].Y, zTop),
                new Vector3(inner[j].X, inner[j].Y, zTop),
                new Vector3(outer[j].X, outer[j].Y, zTop));
            mesh.AddTriangle(
                new Vector3(outer[i].X, outer[i].Y, zTop),
                new Vector3(inner[i].X, inner[i].Y, zTop),
                new Vector3(inner[j].X, inner[j].Y, zTop));
        }

        AddSideWall(mesh, outer, zBottom, zTop, outward: true);
        AddSideWall(mesh, inner, zBottom, zTop, outward: false);

        return mesh;
    }

    /// <summary>Builds the vertical wall along a contour between zBottom and zTop.
    /// Pass outward = SignedArea(outline) > 0 to get an outward-facing wall regardless
    /// of whether the contour is an outer boundary or a hole.</summary>
    public static void AddSideWall(Mesh mesh, IReadOnlyList<Vector2> outline, float zBottom, float zTop, bool outward)
    {
        for (int i = 0; i < outline.Count; i++)
        {
            var p1 = outline[i];
            var p2 = outline[(i + 1) % outline.Count];

            var a = new Vector3(p1.X, p1.Y, zBottom);
            var b = new Vector3(p2.X, p2.Y, zBottom);
            var c = new Vector3(p2.X, p2.Y, zTop);
            var d = new Vector3(p1.X, p1.Y, zTop);

            if (outward)
            {
                mesh.AddTriangle(a, b, c);
                mesh.AddTriangle(a, c, d);
            }
            else
            {
                mesh.AddTriangle(b, a, c);
                mesh.AddTriangle(a, d, c);
            }
        }
    }

    public static Vector2 Centroid(IReadOnlyList<Vector2> points)
    {
        var sum = Vector2.Zero;
        foreach (var p in points)
        {
            sum += p;
        }
        return sum / points.Count;
    }

    /// <summary>Twice the signed area of the polygon (shoelace formula). Positive = counter-clockwise.</summary>
    public static float SignedArea(IReadOnlyList<Vector2> outline)
    {
        float sum = 0;
        for (int i = 0; i < outline.Count; i++)
        {
            var p1 = outline[i];
            var p2 = outline[(i + 1) % outline.Count];
            sum += p1.X * p2.Y - p2.X * p1.Y;
        }
        return sum;
    }

    public static (Vector2 Min, Vector2 Max) BoundingBox(IEnumerable<Vector2> points)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        bool any = false;
        foreach (var p in points)
        {
            any = true;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return any ? (min, max) : (Vector2.Zero, Vector2.Zero);
    }

    /// <summary>Signed volume of a closed triangle mesh via the divergence theorem. Positive
    /// indicates consistently outward-facing winding (a well-formed, watertight solid).</summary>
    public static float SignedVolume(Mesh mesh)
    {
        float volume = 0;
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            var v0 = mesh.Vertices[mesh.Indices[i]];
            var v1 = mesh.Vertices[mesh.Indices[i + 1]];
            var v2 = mesh.Vertices[mesh.Indices[i + 2]];
            volume += Vector3.Dot(v0, Vector3.Cross(v1, v2));
        }
        return volume / 6f;
    }
}
