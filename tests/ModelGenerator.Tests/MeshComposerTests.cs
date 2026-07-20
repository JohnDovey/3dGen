using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using Xunit;

namespace ModelGenerator.Tests;

public class MeshComposerTests
{
    private readonly MeshComposer _composer = new();

    [Fact]
    public void ComposeModel_MergesBaseAndTransformedTextMeshes()
    {
        var baseMesh = new Mesh();
        baseMesh.AddTriangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0));

        var textMesh = new Mesh();
        textMesh.AddTriangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0));

        var transforms = new[] { new Transform(new Vector3(5, 0, 10), 0f) };
        var composed = _composer.ComposeModel(baseMesh, new[] { textMesh }, transforms);

        Assert.Equal(6, composed.Vertices.Count);
        Assert.Equal(6, composed.Indices.Count);
        // The text mesh's vertices should have been translated by (5, 0, 10).
        Assert.Contains(composed.Vertices, v => v.Equals(new Vector3(5, 0, 10)));
    }

    [Fact]
    public void ComposeModel_MismatchedCounts_Throws()
    {
        var baseMesh = new Mesh();
        Assert.Throws<ArgumentException>(() =>
            _composer.ComposeModel(baseMesh, new[] { new Mesh(), new Mesh() }, new[] { new Transform(Vector3.Zero, 0f) }));
    }

    [Fact]
    public void MergeMeshes_ConcatenatesAllGeometry()
    {
        var a = new Mesh();
        a.AddTriangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0));
        var b = new Mesh();
        b.AddTriangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0));

        var merged = _composer.MergeMeshes(new[] { a, b });

        Assert.Equal(6, merged.Vertices.Count);
        Assert.Equal(6, merged.Indices.Count);
    }
}
