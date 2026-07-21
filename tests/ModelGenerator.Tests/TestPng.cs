using SkiaSharp;

namespace ModelGenerator.Tests;

/// <summary>Builds small PNG fixtures for tests without System.Drawing (so tests run on macOS).</summary>
internal static class TestPng
{
    public static byte[] Solid(int width, int height, SKColor color)
    {
        return Create(width, height, canvas =>
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = false };
            canvas.DrawRect(0, 0, width, height, paint);
        });
    }

    public static byte[] Create(int width, int height, Action<SKCanvas> paint)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? Array.Empty<byte>();
    }
}
