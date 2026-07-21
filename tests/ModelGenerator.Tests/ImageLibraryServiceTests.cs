using ModelGenerator.Core.Services;
using SkiaSharp;
using Xunit;

namespace ModelGenerator.Tests;

public class ImageLibraryServiceTests : IDisposable
{
    private readonly string _libraryDir;
    private readonly ImageLibraryService _service;

    public ImageLibraryServiceTests()
    {
        _libraryDir = Path.Combine(Path.GetTempPath(), $"imagelib-{Guid.NewGuid()}");
        Directory.CreateDirectory(_libraryDir);
        _service = new ImageLibraryService(_libraryDir);
    }

    [Fact]
    public void ImportFile_ThenListAndRead_RoundTrips()
    {
        byte[] sampleImage = CreateSamplePng();
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        File.WriteAllBytes(sourcePath, sampleImage);

        try
        {
            string importedName = _service.ImportFile(sourcePath);

            Assert.Contains(importedName, _service.ListImageFiles());
            Assert.Equal(sampleImage, _service.ReadImageBytes(importedName));
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void ImportFile_NameCollision_DedupesWithSuffix()
    {
        byte[] sampleImage = CreateSamplePng();
        string sourcePath = Path.Combine(Path.GetTempPath(), $"logo-{Guid.NewGuid()}.png");
        File.WriteAllBytes(sourcePath, sampleImage);

        try
        {
            string firstName = _service.ImportFile(sourcePath);
            string secondName = _service.ImportFile(sourcePath);

            Assert.NotEqual(firstName, secondName);
            Assert.Equal(2, _service.ListImageFiles().Count);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void ImportContent_ThenListAndRead_RoundTrips()
    {
        byte[] sampleImage = CreateSamplePng();

        string importedName = _service.ImportContent("photo.png", sampleImage);

        Assert.Contains(importedName, _service.ListImageFiles());
        Assert.Equal(sampleImage, _service.ReadImageBytes(importedName));
    }

    [Fact]
    public void ImportContent_NameCollision_DedupesWithSuffix()
    {
        byte[] sampleImage = CreateSamplePng();

        string firstName = _service.ImportContent("photo.png", sampleImage);
        string secondName = _service.ImportContent("photo.png", sampleImage);

        Assert.NotEqual(firstName, secondName);
        Assert.Equal(2, _service.ListImageFiles().Count);
    }

    [Fact]
    public void RenderThumbnail_ProducesNonEmptyPng()
    {
        byte[] sampleImage = CreateSamplePng();

        byte[] png = _service.RenderThumbnail(sampleImage, 64, 64);

        Assert.NotEmpty(png);
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
        Assert.Equal((byte)'N', png[2]);
        Assert.Equal((byte)'G', png[3]);
    }

    [Fact]
    public void DeleteFile_RemovesItFromTheLibrary()
    {
        byte[] sampleImage = CreateSamplePng();
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        File.WriteAllBytes(sourcePath, sampleImage);
        string importedName;
        try
        {
            importedName = _service.ImportFile(sourcePath);
        }
        finally
        {
            File.Delete(sourcePath);
        }

        _service.DeleteFile(importedName);

        Assert.DoesNotContain(importedName, _service.ListImageFiles());
    }

    [Fact]
    public void DeleteFile_AlsoRemovesItsKeywords()
    {
        byte[] sampleImage = CreateSamplePng();
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        File.WriteAllBytes(sourcePath, sampleImage);
        string importedName;
        try
        {
            importedName = _service.ImportFile(sourcePath);
        }
        finally
        {
            File.Delete(sourcePath);
        }
        _service.SetKeywords(importedName, new[] { "photo" });

        _service.DeleteFile(importedName);

        Assert.Empty(_service.GetKeywords(importedName));
    }

    [Fact]
    public void SetKeywords_ThenSearchFiles_FindsItByKeyword()
    {
        byte[] sampleImage = CreateSamplePng();
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        File.WriteAllBytes(sourcePath, sampleImage);
        string importedName;
        try
        {
            importedName = _service.ImportFile(sourcePath);
        }
        finally
        {
            File.Delete(sourcePath);
        }
        _service.SetKeywords(importedName, new[] { "portrait" });

        Assert.Contains(importedName, _service.SearchFiles("portrait"));
        Assert.DoesNotContain(importedName, _service.SearchFiles("nonexistent-tag"));
    }

    private static byte[] CreateSamplePng() => TestPng.Solid(10, 10, SKColors.Gray);

    public void Dispose()
    {
        if (Directory.Exists(_libraryDir))
        {
            Directory.Delete(_libraryDir, recursive: true);
        }
    }
}
