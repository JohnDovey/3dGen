using System.Numerics;
using ModelGenerator.Core.Models;
using Xunit;

namespace ModelGenerator.Tests;

public class MeshTests
{
    [Fact]
    public void AddTriangle_DegenerateCollinearPoints_DoesNotProduceNaNNormal()
    {
        var mesh = new Mesh();

        // Three collinear points: zero-area triangle, zero cross product.
        mesh.AddTriangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0));

        Assert.All(mesh.Normals, n =>
        {
            Assert.False(float.IsNaN(n.X) || float.IsNaN(n.Y) || float.IsNaN(n.Z));
            Assert.False(float.IsInfinity(n.X) || float.IsInfinity(n.Y) || float.IsInfinity(n.Z));
        });
    }

    [Fact]
    public void AddTriangle_CoincidentPoints_DoesNotProduceNaNNormal()
    {
        var mesh = new Mesh();

        // All three points identical: zero-length edges, zero cross product.
        var p = new Vector3(3, 3, 3);
        mesh.AddTriangle(p, p, p);

        Assert.All(mesh.Normals, n =>
        {
            Assert.False(float.IsNaN(n.X) || float.IsNaN(n.Y) || float.IsNaN(n.Z));
        });
    }

    [Fact]
    public void AddTriangle_NonDegenerateTriangle_ProducesUnitNormal()
    {
        var mesh = new Mesh();

        mesh.AddTriangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0));

        Assert.All(mesh.Normals, n => Assert.Equal(1f, n.Length(), precision: 4));
    }
}
