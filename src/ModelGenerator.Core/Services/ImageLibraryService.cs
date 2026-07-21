using SkiaSharp;

namespace ModelGenerator.Core.Services;

/// <summary>Manages the app's local library of JPG/PNG files: listing, reading, importing new
/// files (deduping name collisions), and rendering PNG thumbnails for a picker UI — mirrors
/// SvgLibraryService.</summary>
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

    public string ImportContent(string preferredFileName, byte[] imageData)
    {
        string fileName = string.IsNullOrWhiteSpace(preferredFileName) ? "import.png" : Path.GetFileName(preferredFileName);
        string destination = Path.Combine(_libraryDirectory, fileName);
        if (File.Exists(destination))
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".png";
            }

            int counter = 2;
            do
            {
                fileName = $"{baseName} ({counter}){extension}";
                destination = Path.Combine(_libraryDirectory, fileName);
                counter++;
            } while (File.Exists(destination));
        }

        Directory.CreateDirectory(_libraryDirectory);
        File.WriteAllBytes(destination, imageData);
        return fileName;
    }

    public byte[] RenderThumbnail(byte[] imageData, int width, int height)
    {
        using var source = SKBitmap.Decode(imageData);
        if (source is null)
        {
            return EncodeSolidPlaceholder(width, height);
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data?.ToArray() ?? EncodeSolidPlaceholder(width, height);
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

    private static byte[] EncodeSolidPlaceholder(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.LightGray);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data?.ToArray() ?? Array.Empty<byte>();
    }
}
