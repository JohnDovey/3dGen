namespace ModelGenerator.Core.Services;

public interface IImageLibraryService
{
    IReadOnlyList<string> ListImageFiles();
    byte[] ReadImageBytes(string fileName);

    /// <summary>Copies the file into the library folder, deduping name collisions. Returns the
    /// final filename actually used.</summary>
    string ImportFile(string sourceFilePath);

    /// <summary>Writes in-memory image bytes into the library (same name-collision rules as
    /// <see cref="ImportFile"/>). Used by project-bundle import.</summary>
    string ImportContent(string preferredFileName, byte[] imageData);

    /// <summary>Renders a PNG thumbnail of the image (width × height). UI layers decode the bytes
    /// into their toolkit image type.</summary>
    byte[] RenderThumbnail(byte[] imageData, int width, int height);

    /// <summary>Permanently removes a file (and its keywords) from the library.</summary>
    void DeleteFile(string fileName);

    /// <summary>Files whose name or keywords contain query (case-insensitive) — a blank query
    /// returns every file, same as ListImageFiles().</summary>
    IReadOnlyList<string> SearchFiles(string query);

    IReadOnlyList<string> GetKeywords(string fileName);
    void SetKeywords(string fileName, IReadOnlyList<string> keywords);
}
