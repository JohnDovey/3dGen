using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace ModelGenerator.Core.Services;

/// <summary>Manages the app's local library of JPG/PNG files: listing, reading, importing new
/// files (deduping name collisions), and rendering thumbnails for a picker UI — mirrors
/// SvgLibraryService, but thumbnailing a raster image is a plain GDI+ resize rather than SVG
/// parsing/rendering, so it's kept as a separate implementation.</summary>
[SupportedOSPlatform("windows")]
public class ImageLibraryService : IImageLibraryService
{
    private static readonly string[] ImageExtensions = { "*.png", "*.jpg", "*.jpeg" };

    private readonly string _libraryDirectory;
    private readonly LibraryMetadataStore _metadata;

    public ImageLibraryService(string libraryDirectory)
    {
        _libraryDirectory = libraryDirectory;
        _metadata = new LibraryMetadataStore(libraryDirectory);
    }

    public IReadOnlyList<string> ListImageFiles()
    {
        if (!Directory.Exists(_libraryDirectory))
        {
            return new List<string>();
        }

        return ImageExtensions
            .SelectMany(pattern => Directory.GetFiles(_libraryDirectory, pattern))
            .Select(Path.GetFileName)
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();
    }

    public byte[] ReadImageBytes(string fileName) => File.ReadAllBytes(Path.Combine(_libraryDirectory, fileName));

    public string ImportFile(string sourceFilePath)
    {
        string fileName = Path.GetFileName(sourceFilePath);
        string destination = Path.Combine(_libraryDirectory, fileName);

        if (File.Exists(destination))
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int counter = 2;
            do
            {
                fileName = $"{baseName} ({counter}){extension}";
                destination = Path.Combine(_libraryDirectory, fileName);
                counter++;
            } while (File.Exists(destination));
        }

        File.Copy(sourceFilePath, destination);
        return fileName;
    }

    public Bitmap RenderThumbnail(byte[] imageData, int width, int height)
    {
        using var source = new Bitmap(new MemoryStream(imageData));
        var thumbnail = new Bitmap(width, height);
        using var g = Graphics.FromImage(thumbnail);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, 0, 0, width, height);
        return thumbnail;
    }

    public void DeleteFile(string fileName)
    {
        string path = Path.Combine(_libraryDirectory, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        _metadata.RemoveEntry(fileName);
    }

    public IReadOnlyList<string> SearchFiles(string query) => _metadata.Filter(ListImageFiles(), query);

    public IReadOnlyList<string> GetKeywords(string fileName) => _metadata.GetKeywords(fileName);

    public void SetKeywords(string fileName, IReadOnlyList<string> keywords) => _metadata.SetKeywords(fileName, keywords);
}
