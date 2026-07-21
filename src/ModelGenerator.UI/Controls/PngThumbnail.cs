using System.IO;

namespace ModelGenerator.UI.Controls;

/// <summary>Decodes Core library PNG thumbnail bytes into a WinForms <see cref="Image"/> the
/// dialogs/editors can assign to PictureBox / ImageList. The returned image owns its own
/// buffer (stream is copied), so callers may dispose it independently of the source bytes.</summary>
internal static class PngThumbnail
{
    public static Image? TryDecode(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(pngBytes);
            using var decoded = Image.FromStream(stream);
            // Clone so the Image is not tied to the MemoryStream lifetime.
            return new Bitmap(decoded);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
