using System.Numerics;
using LibTessDotNet;
using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Utilities;

/// <summary>Extrusion helpers shared by the shape generators.</summary>
public static class MeshMath
{
    /// <summary>Tessellates a set of 2D contours (handling holes/counters via the nonzero winding
    /// rule — e.g. the counter of a letter "O", or a cutout in an SVG) and extrudes the result
    /// into a solid slab between zBottom and zTop, with flat top/bottom caps and vertical side
    /// walls per contour. Shared by text and SVG mesh conversion.</summary>
    public static Mesh ExtrudeContours(IReadOnlyList<IReadOnlyList<Vector2>> contours, float zBottom, float zTop)
    {
        var mesh = new Mesh();
        if (contours.Count == 0)
        {
            return mesh;
        }

        var tess = new Tess();
        foreach (var contour in contours)
        {
            var contourVertices = contour
                .Select(p => new ContourVertex { Position = new Vec3 { X = p.X, Y = p.Y, Z = 0 } })
                .ToArray();
            tess.AddContour(contourVertices);
        }
        tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3);

        for (int i = 0; i < tess.ElementCount; i++)
        {
            var v0 = tess.Vertices[tess.Elements[i * 3]].Position;
            var v1 = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
            var v2 = tess.Vertices[tess.Elements[i * 3 + 2]].Position;

            // Top cap.
            mesh.AddTriangle(
                new Vector3(v0.X, v0.Y, zTop),
                new Vector3(v1.X, v1.Y, zTop),
                new Vector3(v2.X, v2.Y, zTop));

            // Bottom cap (reversed winding to face down).
            mesh.AddTriangle(
                new Vector3(v0.X, v0.Y, zBottom),
                new Vector3(v2.X, v2.Y, zBottom),
                new Vector3(v1.X, v1.Y, zBottom));
        }

        // Side walls per contour. Outer boundaries and hole (counter) boundaries are wound in
        // opposite directions by the nonzero fill convention, so a single sign check on each
        // contour's own signed area is enough to orient every wall outward.
        foreach (var contour in contours)
        {
            bool outward = SignedArea(contour) > 0;
            AddSideWall(mesh, contour, zBottom, zTop, outward);
        }

        return mesh;
    }

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

    /// <summary>Like <see cref="ExtrudeRing"/>, but the top cap is tessellated with additional
    /// cutout contours as holes (for engraved border text). Each cutout gets walls down to
    /// <paramref name="zTop"/> − <paramref name="cutoutDepth"/> and a floor at that depth.
    /// Empty cutouts or non-positive depth fall back to <see cref="ExtrudeRing"/>.</summary>
    public static Mesh ExtrudeRingWithTopCutouts(
        IReadOnlyList<Vector2> outer,
        IReadOnlyList<Vector2> inner,
        IReadOnlyList<IReadOnlyList<Vector2>> cutoutContours,
        float zBottom,
        float zTop,
        float cutoutDepth)
    {
        if (cutoutContours.Count == 0 || cutoutDepth <= 0f)
        {
            return ExtrudeRing(outer, inner, zBottom, zTop);
        }

        if (outer.Count != inner.Count)
        {
            throw new ArgumentException("Outer and inner outlines must have the same point count.");
        }

        float floorZ = zTop - cutoutDepth;
        if (floorZ < zBottom)
        {
            floorZ = zBottom;
        }

        var mesh = new Mesh();
        int n = outer.Count;

        // Bottom ring (unchanged — recess does not go through).
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

        AddSideWall(mesh, outer, zBottom, zTop, outward: true);
        AddSideWall(mesh, inner, zBottom, zTop, outward: false);

        // Top cap with ring hole + cutout holes via LibTessDotNet.
        var tess = new Tess();
        AddContourEnsuringWinding(tess, outer, wantPositiveArea: true);
        AddContourEnsuringWinding(tess, inner, wantPositiveArea: false);
        foreach (var cutout in cutoutContours)
        {
            if (cutout.Count >= 3)
            {
                AddContourEnsuringWinding(tess, cutout, wantPositiveArea: false);
            }
        }
        tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3);

        for (int i = 0; i < tess.ElementCount; i++)
        {
            var v0 = tess.Vertices[tess.Elements[i * 3]].Position;
            var v1 = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
            var v2 = tess.Vertices[tess.Elements[i * 3 + 2]].Position;
            mesh.AddTriangle(
                new Vector3(v0.X, v0.Y, zTop),
                new Vector3(v1.X, v1.Y, zTop),
                new Vector3(v2.X, v2.Y, zTop));
        }

        // Cutout cavities: walls into the recess + floor at floorZ.
        foreach (var cutout in cutoutContours)
        {
            if (cutout.Count < 3)
            {
                continue;
            }

            // Walls facing into the cavity.
            AddSideWall(mesh, cutout, floorZ, zTop, outward: false);

            // Floor of the recess (tessellated — glyphs may not be star-shaped).
            var floorTess = new Tess();
            AddContourEnsuringWinding(floorTess, cutout, wantPositiveArea: true);
            floorTess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3);
            for (int i = 0; i < floorTess.ElementCount; i++)
            {
                var v0 = floorTess.Vertices[floorTess.Elements[i * 3]].Position;
                var v1 = floorTess.Vertices[floorTess.Elements[i * 3 + 1]].Position;
                var v2 = floorTess.Vertices[floorTess.Elements[i * 3 + 2]].Position;
                // Face up (same winding as ExtrudeContours top) so the floor is visible from above.
                mesh.AddTriangle(
                    new Vector3(v0.X, v0.Y, floorZ),
                    new Vector3(v1.X, v1.Y, floorZ),
                    new Vector3(v2.X, v2.Y, floorZ));
            }
        }

        return mesh;
    }

    private static void AddContourEnsuringWinding(Tess tess, IReadOnlyList<Vector2> contour, bool wantPositiveArea)
    {
        bool positive = SignedArea(contour) > 0;
        IEnumerable<Vector2> points = (positive == wantPositiveArea) ? contour : contour.Reverse();
        var verts = points
            .Select(p => new ContourVertex { Position = new Vec3 { X = p.X, Y = p.Y, Z = 0 } })
            .ToArray();
        tess.AddContour(verts);
    }

    /// <summary>Extrudes a masked heightmap grid into a solid — used for image bas-relief inserts.
    /// topZ[row, col] is the world Z of each grid vertex (row 0 / col 0 at the -Y/-X corner, grid
    /// centered at the origin); included[cellRow, cellCol] marks which cells (one row/col smaller
    /// than topZ) actually have geometry — a fully-true mask degenerates to a plain rectangular
    /// slab with a heightmapped top. Each included cell gets a top face (using its own corners'
    /// topZ) and a flat bottom face at zBottom; a vertical wall is added along every cell edge
    /// where the neighboring cell is excluded (or off-grid), so transparent/excluded regions are
    /// genuinely absent from the mesh rather than merely flattened.</summary>
    public static Mesh ExtrudeMaskedHeightfield(float[,] topZ, bool[,] included, float cellWidth, float cellDepth, float zBottom)
    {
        int rows = topZ.GetLength(0);
        int cols = topZ.GetLength(1);
        int cellRows = rows - 1;
        int cellCols = cols - 1;

        if (included.GetLength(0) != cellRows || included.GetLength(1) != cellCols)
        {
            throw new ArgumentException("included must be exactly one row/column smaller than topZ.");
        }

        var mesh = new Mesh();
        if (cellRows <= 0 || cellCols <= 0)
        {
            return mesh;
        }

        Vector3 VertexTop(int row, int col) => new(
            (col - (cols - 1) / 2f) * cellWidth,
            (row - (rows - 1) / 2f) * cellDepth,
            topZ[row, col]);

        Vector3 VertexBottom(int row, int col) => new(
            (col - (cols - 1) / 2f) * cellWidth,
            (row - (rows - 1) / 2f) * cellDepth,
            zBottom);

        bool IsIncluded(int cellRow, int cellCol) =>
            cellRow >= 0 && cellRow < cellRows && cellCol >= 0 && cellCol < cellCols && included[cellRow, cellCol];

        for (int cellRow = 0; cellRow < cellRows; cellRow++)
        {
            for (int cellCol = 0; cellCol < cellCols; cellCol++)
            {
                if (!included[cellRow, cellCol])
                {
                    continue;
                }

                // Corners in CCW order (viewed from +Z), matching the winding convention used by
                // every other Extrude* helper in this file.
                var topA = VertexTop(cellRow, cellCol);
                var topB = VertexTop(cellRow, cellCol + 1);
                var topC = VertexTop(cellRow + 1, cellCol + 1);
                var topD = VertexTop(cellRow + 1, cellCol);

                mesh.AddTriangle(topA, topB, topC);
                mesh.AddTriangle(topA, topC, topD);

                var bottomA = VertexBottom(cellRow, cellCol);
                var bottomB = VertexBottom(cellRow, cellCol + 1);
                var bottomC = VertexBottom(cellRow + 1, cellCol + 1);
                var bottomD = VertexBottom(cellRow + 1, cellCol);

                // Reversed winding so the bottom cap faces down.
                mesh.AddTriangle(bottomA, bottomC, bottomB);
                mesh.AddTriangle(bottomA, bottomD, bottomC);

                if (!IsIncluded(cellRow - 1, cellCol))
                {
                    AddHeightfieldWall(mesh, topA, topB, bottomA, bottomB);
                }
                if (!IsIncluded(cellRow, cellCol + 1))
                {
                    AddHeightfieldWall(mesh, topB, topC, bottomB, bottomC);
                }
                if (!IsIncluded(cellRow + 1, cellCol))
                {
                    AddHeightfieldWall(mesh, topC, topD, bottomC, bottomD);
                }
                if (!IsIncluded(cellRow, cellCol - 1))
                {
                    AddHeightfieldWall(mesh, topD, topA, bottomD, bottomA);
                }
            }
        }

        return mesh;
    }

    /// <summary>Builds one outward-facing wall quad along a single heightfield cell edge, from its
    /// (possibly non-flat) top down to the flat bottom — same outward-normal convention as
    /// AddSideWall, generalized to a per-edge top instead of one flat top.</summary>
    private static void AddHeightfieldWall(Mesh mesh, Vector3 topP1, Vector3 topP2, Vector3 bottomP1, Vector3 bottomP2)
    {
        mesh.AddTriangle(bottomP1, bottomP2, topP2);
        mesh.AddTriangle(bottomP1, topP2, topP1);
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
