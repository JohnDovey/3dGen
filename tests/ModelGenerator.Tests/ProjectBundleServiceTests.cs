using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Services.ProjectBundle;
using Xunit;

namespace ModelGenerator.Tests;

public class ProjectBundleServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"bundle-{Guid.NewGuid():N}");
    private readonly ProjectBundleService _service;

    public ProjectBundleServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
        var svgDir = Path.Combine(_tempDir, "svg");
        var imgDir = Path.Combine(_tempDir, "img");
        Directory.CreateDirectory(svgDir);
        Directory.CreateDirectory(imgDir);
        _service = new ProjectBundleService(new SvgLibraryService(svgDir), new ImageLibraryService(imgDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void ExportImport_RoundTripsTextSvgImageAndCustomShape()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
              <rect width="10" height="10"/>
            </svg>
            """;
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        var model = new Model
        {
            Name = "Bundle Demo",
            ShapeType = ShapeType.CustomSvg,
            ShapeSize = 50,
            ShapeThickness = 8,
            BorderThickness = 4,
            BorderHeight = 3,
            CustomShapeSvgContent = svg,
            CustomShapeSourceFileName = "star.svg",
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "HI", FontName = "Arial", FontSize = 12, TextHeight = 5 }
            },
            SvgInserts =
            {
                new SvgInsert { LineNumber = 0, SourceFileName = "badge.svg", SvgContent = svg, Scale = 20, EmbossHeight = 2 }
            },
            ImageInserts =
            {
                new ImageInsert { LineNumber = 0, SourceFileName = "dot.png", ImageData = png, Scale = 15, ReliefHeight = 2 }
            },
            BorderTextLines =
            {
                new BorderTextLine { LineNumber = 0, Content = "RIM", FontSize = 6, Height = 1, Mode = BorderTextMode.Embossed, AnchorAngleDegrees = 90 }
            }
        };

        string path = Path.Combine(_tempDir, "demo.mgproj");
        _service.ExportBundle(model, path, appVersion: "0.9.0");
        Assert.True(File.Exists(path));

        var loaded = _service.ImportBundle(path);
        Assert.Equal(0, loaded.Id);
        Assert.Equal("Bundle Demo", loaded.Name);
        Assert.Equal(ShapeType.CustomSvg, loaded.ShapeType);
        Assert.Equal(svg, loaded.CustomShapeSvgContent);
        Assert.Single(loaded.TextLines);
        Assert.Equal("HI", loaded.TextLines[0].Content);
        Assert.Single(loaded.SvgInserts);
        Assert.Equal(svg, loaded.SvgInserts[0].SvgContent);
        Assert.Single(loaded.ImageInserts);
        Assert.Equal(png, loaded.ImageInserts[0].ImageData);
        Assert.Single(loaded.BorderTextLines);
        Assert.Equal("RIM", loaded.BorderTextLines[0].Content);
    }

    [Fact]
    public void ImportBundle_DoesNotDuplicateLibraryFilesOnReimport()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="5" height="5"><circle cx="2.5" cy="2.5" r="2"/></svg>
            """;
        var model = new Model
        {
            Name = "Dedup",
            ShapeType = ShapeType.Circle,
            ShapeSize = 40,
            SvgInserts =
            {
                new SvgInsert { LineNumber = 0, SourceFileName = "c.svg", SvgContent = svg, Scale = 10, EmbossHeight = 1 }
            }
        };

        string path = Path.Combine(_tempDir, "dedup.mgproj");
        _service.ExportBundle(model, path);

        var svgLib = new SvgLibraryService(Path.Combine(_tempDir, "svg"));
        _ = _service.ImportBundle(path);
        int count1 = svgLib.ListSvgFiles().Count;
        _ = _service.ImportBundle(path);
        int count2 = svgLib.ListSvgFiles().Count;
        Assert.Equal(count1, count2);
    }

    [Fact]
    public void ImportBundle_UnsupportedFormatVersion_Throws()
    {
        // Minimal zip with bad version via Export then rewrite is heavy; call Import with hand-made zip
        string path = Path.Combine(_tempDir, "bad.mgproj");
        using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("manifest.json");
            using var w = new StreamWriter(entry.Open());
            w.Write("""{"formatVersion":99,"modelName":"x","shapeType":0,"shapeSize":10,"textLines":[],"svgInserts":[],"imageInserts":[],"borderTextLines":[]}""");
        }

        var ex = Assert.Throws<InvalidOperationException>(() => _service.ImportBundle(path));
        Assert.Contains("Unsupported project bundle format version", ex.Message);
    }
}
