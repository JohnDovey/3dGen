using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using SkiaSharp;
using Xunit;
using CoreMesh = ModelGenerator.Core.Models.Mesh;

namespace ModelGenerator.Tests;

public class ImageMeshConverterTests
{
    private readonly ImageMeshConverter _converter = new();

    [Fact]
    public void ConvertImageToMesh_SolidOpaqueImage_ProducesWatertightMesh()
    {
        byte[] imageData = TestPng.Solid(20, 20, SKColors.Gray);
        var insert = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };

        var mesh = _converter.ConvertImageToMesh(insert);

        Assert.NotEmpty(mesh.Vertices);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.True(MeshMath.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void ConvertImageToMesh_OpaqueSquareImage_FootprintMatchesScale()
    {
        byte[] imageData = TestPng.Solid(50, 50, SKColors.Gray);
        var insert = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };

        var mesh = _converter.ConvertImageToMesh(insert);

        var (min, max) = MeshMath.BoundingBox(mesh.Vertices.Select(v => new Vector2(v.X, v.Y)));
        Assert.Equal(40f, max.X - min.X, precision: 1);
        Assert.Equal(40f, max.Y - min.Y, precision: 1);
    }

    [Fact]
    public void ConvertImageToMesh_HigherDetail_ProducesMoreVertices()
    {
        byte[] imageData = TestPng.Solid(50, 50, SKColors.Gray);
        var low = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };
        var high = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.High };

        var lowMesh = _converter.ConvertImageToMesh(low);
        var highMesh = _converter.ConvertImageToMesh(high);

        Assert.True(highMesh.Vertices.Count > lowMesh.Vertices.Count);
    }

    [Fact]
    public void ConvertImageToMesh_Invert_FlipsWhichSideIsTallest()
    {
        byte[] imageData = TestPng.Create(40, 40, canvas =>
        {
            using var black = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
            using var white = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
            canvas.DrawRect(0, 0, 20, 40, black);
            canvas.DrawRect(20, 0, 20, 40, white);
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
        byte[] opaqueData = TestPng.Solid(40, 40, SKColors.Gray);
        byte[] halfTransparentData = TestPng.Create(40, 40, canvas =>
        {
            using var paint = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Fill };
            canvas.DrawRect(0, 0, 20, 40, paint);
        });

        var opaqueInsert = new ImageInsert { ImageData = opaqueData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };
        var halfInsert = new ImageInsert { ImageData = halfTransparentData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Low };

        var opaqueMesh = _converter.ConvertImageToMesh(opaqueInsert);
        var halfMesh = _converter.ConvertImageToMesh(halfInsert);

        var (fullMin, fullMax) = MeshMath.BoundingBox(opaqueMesh.Vertices.Select(v => new Vector2(v.X, v.Y)));
        var (halfMin, halfMax) = MeshMath.BoundingBox(halfMesh.Vertices.Select(v => new Vector2(v.X, v.Y)));

        Assert.True(halfMax.X - halfMin.X < (fullMax.X - fullMin.X) * 0.75f);
    }

    [Fact]
    public void ConvertImageToMesh_OpaqueContentOffCenterInLargerCanvas_MeshIsCenteredAtOrigin()
    {
        byte[] imageData = TestPng.Create(100, 100, canvas =>
        {
            using var paint = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Fill };
            canvas.DrawRect(5, 5, 20, 20, paint);
        });
        var insert = new ImageInsert { ImageData = imageData, Scale = 40, ReliefHeight = 5, Detail = ImageDetail.Medium };

        var mesh = _converter.ConvertImageToMesh(insert);

        var (min, max) = MeshMath.BoundingBox(mesh.Vertices.Select(v => new Vector2(v.X, v.Y)));
        var center = (min + max) / 2f;
        Assert.Equal(0f, center.X, precision: 0);
        Assert.Equal(0f, center.Y, precision: 0);
    }

    private static float AverageXOfTallestVertices(CoreMesh mesh)
    {
        float maxZ = mesh.Vertices.Max(v => v.Z);
        return mesh.Vertices.Where(v => v.Z > maxZ - 0.01f).Average(v => v.X);
    }
}
