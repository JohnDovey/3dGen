using SkiaSharp;
using Svg.Skia;

namespace ModelGenerator.Core.Services;

/// <summary>Manages the app's local library of SVG files: listing, reading, importing new files
/// (deduping name collisions), and rendering PNG thumbnails for a picker UI.</summary>
public class SvgLibraryService : ISvgLibraryService
{
    private readonly string _libraryDirectory;
    private readonly LibraryMetadataStore _metadata;

    public SvgLibraryService(string libraryDirectory)
    {
        _libraryDirectory = libraryDirectory;
        _metadata = new LibraryMetadataStore(libraryDirectory);
    }

    public IReadOnlyList<string> ListSvgFiles() =>
        Directory.Exists(_libraryDirectory)
            ? Directory.GetFiles(_libraryDirectory, "*.svg").Select(Path.GetFileName).Cast<string>().OrderBy(n => n).ToList()
            : new List<string>();

    public string ReadSvgContent(string fileName) => File.ReadAllText(Path.Combine(_libraryDirectory, fileName));

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

    public string ImportContent(string preferredFileName, string svgContent)
    {
        string fileName = string.IsNullOrWhiteSpace(preferredFileName) ? "import.svg" : Path.GetFileName(preferredFileName);
        if (!fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".svg";
        }

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

        Directory.CreateDirectory(_libraryDirectory);
        File.WriteAllText(destination, svgContent);
        return fileName;
    }

    public byte[] RenderThumbnail(string svgContent, int width, int height)
    {
        using var skSvg = new SKSvg();
        var picture = skSvg.FromSvg(svgContent);
        if (picture is null)
        {
            return EncodeSolidPlaceholder(width, height);
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = picture.CullRect;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            float scale = Math.Min(width / bounds.Width, height / bounds.Height);
            float dx = (width - bounds.Width * scale) * 0.5f;
            float dy = (height - bounds.Height * scale) * 0.5f;
            canvas.Translate(dx, dy);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
        }

        canvas.DrawPicture(picture);

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

    public IReadOnlyList<string> SearchFiles(string query) => _metadata.Filter(ListSvgFiles(), query);

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
