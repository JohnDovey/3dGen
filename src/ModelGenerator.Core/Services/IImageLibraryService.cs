using System.Drawing;

namespace ModelGenerator.Core.Services;

public interface IImageLibraryService
{
    IReadOnlyList<string> ListImageFiles();
    byte[] ReadImageBytes(string fileName);

    /// <summary>Copies the file into the library folder, deduping name collisions. Returns the
    /// final filename actually used.</summary>
    string ImportFile(string sourceFilePath);

    Bitmap RenderThumbnail(byte[] imageData, int width, int height);

    /// <summary>Permanently removes a file (and its keywords) from the library.</summary>
    void DeleteFile(string fileName);

    /// <summary>Files whose name or keywords contain query (case-insensitive) — a blank query
    /// returns every file, same as ListImageFiles().</summary>
    IReadOnlyList<string> SearchFiles(string query);

    IReadOnlyList<string> GetKeywords(string fileName);
    void SetKeywords(string fileName, IReadOnlyList<string> keywords);
}
