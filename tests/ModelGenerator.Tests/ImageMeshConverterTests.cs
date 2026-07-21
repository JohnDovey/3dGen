using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;
using CoreMesh = ModelGenerator.Core.Models.Mesh;

namespace ModelGenerator.Tests;

public class ImageMeshConverterTests
{
    private readonly ImageMeshConverter _converter = new();

    [Fact]
    public void ConvertImageToMesh_SolidOpaqueImage_ProducesWatertightMesh()
    {
        byte[] imageData = CreateTestPng(20, 20, g => g.FillRectangle(Brushes.Gray, 0, 0, 20, 20));
        var insert = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };

        var mesh = _converter.ConvertImageToMesh(insert);

        Assert.NotEmpty(mesh.Vertices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void ConvertImageToMesh_OpaqueSquareImage_FootprintMatchesScale()
    {
        byte[] imageData = CreateTestPng(50, 50, g => g.FillRectangle(Brushes.Gray, 0, 0, 50, 50));
        var insert = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };

        var mesh = _converter.ConvertImageToMesh(insert);

        var (min, max) = MeshMath.BoundingBox(mesh.Vertices.Select(v => new Vector2(v.X, v.Y)));
        Assert.Equal(40f, max.X - min.X, precision: 1);
        Assert.Equal(40f, max.Y - min.Y, precision: 1);
    }

    [Fact]
    public void ConvertImageToMesh_HigherDetail_ProducesMoreVertices()
    {
        byte[] imageData = CreateTestPng(50, 50, g => g.FillRectangle(Brushes.Gray, 0, 0, 50, 50));
        var low = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };
        var high = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.High };

        var lowMesh = _converter.ConvertImageToMesh(low);
        var highMesh = _converter.ConvertImageToMesh(high);

        Assert.True(highMesh.Vertices.Count > lowMesh.Vertices.Count);
    }

    [Fact]
    public void ConvertImageToMesh_Invert_FlipsWhichSideIsTallest()
    {
        byte[] imageData = CreateTestPng(40, 40, g =>
        {
            g.FillRectangle(Brushes.Black, 0, 0, 20, 40);
            g.FillRectangle(Brushes.White, 20, 0, 20, 40);
        });

        var normal = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low, Invert = false };
        var inverted = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low, Invert = true };

        var normalMesh = _converter.ConvertImageToMesh(normal);
        var invertedMesh = _converter.ConvertImageToMesh(inverted);

        Assert.True(AverageXOfTallestVertices(normalMesh) > 0);
        Assert.True(AverageXOfTallestVertices(invertedMesh) < 0);
    }

    [Fact]
    public void ConvertImageToMesh_TransparentHalf_ClipsFootprintToOpaqueSide()
    {
        byte[] opaqueData = CreateTestPng(40, 40, g => g.FillRectangle(Brushes.Gray, 0, 0, 40, 40));
        byte[] halfTransparentData = CreateTestPng(40, 40, g => g.FillRectangle(Brushes.Gray, 0, 0, 20, 40));
        // Right half of halfTransparentData is left untouched, i.e. still fully transparent from
        // CreateTestPng's initial Color.Transparent clear.

        var opaqueInsert = new ImageInsert { ImageData = opaqueData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };
        var halfInsert = new ImageInsert { ImageData = halfTransparentData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };

        var opaqueMesh = _converter.ConvertImageToMesh(opaqueInsert);
        var halfMesh = _converter.ConvertImageToMesh(halfInsert);

        var (fullMin, fullMax) = MeshMath.BoundingBox(opaqueMesh.Vertices.Select(v => new Vector2(v.X, v.Y)));
        var (halfMin, halfMax) = MeshMath.BoundingBox(halfMesh.Vertices.Select(v => new Vector2(v.X, v.Y)));

        Assert.True(halfMax.X - halfMin.X < (fullMax.X - fullMin.X) * 0.75f);
    }

    private static float AverageXOfTallestVertices(CoreMesh mesh)
    {
        float maxZ = mesh.Vertices.Max(v => v.Z);
        return mesh.Vertices.Where(v => v.Z > maxZ - 0.01f).Average(v => v.X);
    }

    private static byte[] CreateTestPng(int width, int height, Action<Graphics> paint)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            paint(g);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
