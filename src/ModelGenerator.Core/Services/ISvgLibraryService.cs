namespace ModelGenerator.Core.Services;

public interface ISvgLibraryService
{
    IReadOnlyList<string> ListSvgFiles();
    string ReadSvgContent(string fileName);

    /// <summary>Copies the file into the library folder, deduping name collisions. Returns the
    /// final filename actually used.</summary>
    string ImportFile(string sourceFilePath);

    /// <summary>Writes in-memory SVG text into the library (same name-collision rules as
    /// <see cref="ImportFile"/>). Used by project-bundle import.</summary>
    string ImportContent(string preferredFileName, string svgContent);

    /// <summary>Renders a PNG thumbnail of the SVG (width × height). UI layers decode the bytes
    /// into their toolkit image type (Bitmap, NSImage, etc.).</summary>
    byte[] RenderThumbnail(string svgContent, int width, int height);

    /// <summary>Permanently removes a file (and its keywords) from the library.</summary>
    void DeleteFile(string fileName);

    /// <summary>Files whose name or keywords contain query (case-insensitive) — a blank query
    /// returns every file, same as ListSvgFiles().</summary>
    IReadOnlyList<string> SearchFiles(string query);

    IReadOnlyList<string> GetKeywords(string fileName);
    void SetKeywords(string fileName, IReadOnlyList<string> keywords);
}
