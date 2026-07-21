using ModelGenerator.Core.Services;
using Xunit;

namespace ModelGenerator.Tests;

public class SvgLibraryServiceTests : IDisposable
{
    private readonly string _libraryDir;
    private readonly SvgLibraryService _service;

    private const string SampleSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
            <rect x="0" y="0" width="10" height="10" />
        </svg>
        """;

    public SvgLibraryServiceTests()
    {
        _libraryDir = Path.Combine(Path.GetTempPath(), $"svglib-{Guid.NewGuid()}");
        Directory.CreateDirectory(_libraryDir);
        _service = new SvgLibraryService(_libraryDir);
    }

    [Fact]
    public void ImportFile_ThenListAndRead_RoundTrips()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.svg");
        File.WriteAllText(sourcePath, SampleSvg);

        try
        {
            string importedName = _service.ImportFile(sourcePath);

            Assert.Contains(importedName, _service.ListSvgFiles());
            Assert.Equal(SampleSvg, _service.ReadSvgContent(importedName));
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void ImportFile_NameCollision_DedupesWithSuffix()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"logo-{Guid.NewGuid()}.svg");
        File.WriteAllText(sourcePath, SampleSvg);

        try
        {
            string firstName = _service.ImportFile(sourcePath);
            string secondName = _service.ImportFile(sourcePath);

            Assert.NotEqual(firstName, secondName);
            Assert.Equal(2, _service.ListSvgFiles().Count);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void RenderThumbnail_ProducesNonEmptyPng()
    {
        byte[] png = _service.RenderThumbnail(SampleSvg, 64, 64);

        Assert.NotEmpty(png);
        // PNG magic number
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
        Assert.Equal((byte)'N', png[2]);
        Assert.Equal((byte)'G', png[3]);
    }

    [Fact]
    public void DeleteFile_RemovesItFromTheLibrary()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.svg");
        File.WriteAllText(sourcePath, SampleSvg);
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

        Assert.DoesNotContain(importedName, _service.ListSvgFiles());
    }

    [Fact]
    public void DeleteFile_AlsoRemovesItsKeywords()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.svg");
        File.WriteAllText(sourcePath, SampleSvg);
        string importedName;
        try
        {
            importedName = _service.ImportFile(sourcePath);
        }
        finally
        {
            File.Delete(sourcePath);
        }
        _service.SetKeywords(importedName, new[] { "logo" });

        _service.DeleteFile(importedName);

        Assert.Empty(_service.GetKeywords(importedName));
    }

    [Fact]
    public void SetKeywords_ThenSearchFiles_FindsItByKeyword()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.svg");
        File.WriteAllText(sourcePath, SampleSvg);
        string importedName;
        try
        {
            importedName = _service.ImportFile(sourcePath);
        }
        finally
        {
            File.Delete(sourcePath);
        }
        _service.SetKeywords(importedName, new[] { "mascot" });

        Assert.Contains(importedName, _service.SearchFiles("mascot"));
        Assert.DoesNotContain(importedName, _service.SearchFiles("nonexistent-tag"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_libraryDir))
        {
            Directory.Delete(_libraryDir, recursive: true);
        }
    }
}
