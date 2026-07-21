using System.Drawing;
using System.Runtime.Versioning;
using Svg;

namespace ModelGenerator.Core.Services;

/// <summary>Manages the app's local library of SVG files: listing, reading, importing new files
/// (deduping name collisions), and rendering thumbnails for a picker UI.</summary>
[SupportedOSPlatform("windows")]
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

    public Bitmap RenderThumbnail(string svgContent, int width, int height)
    {
        var document = SvgDocument.FromSvg<SvgDocument>(svgContent);
        return document.Draw(width, height);
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
}
