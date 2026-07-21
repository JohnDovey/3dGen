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
}
