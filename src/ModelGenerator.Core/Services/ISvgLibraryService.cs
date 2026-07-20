using System.Drawing;

namespace ModelGenerator.Core.Services;

public interface ISvgLibraryService
{
    IReadOnlyList<string> ListSvgFiles();
    string ReadSvgContent(string fileName);

    /// <summary>Copies the file into the library folder, deduping name collisions. Returns the
    /// final filename actually used.</summary>
    string ImportFile(string sourceFilePath);

    Bitmap RenderThumbnail(string svgContent, int width, int height);
}
