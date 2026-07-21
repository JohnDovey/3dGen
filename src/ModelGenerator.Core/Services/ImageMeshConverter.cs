using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;
using SkiaSharp;

namespace ModelGenerator.Core.Services;

/// <summary>
/// Converts a JPG/PNG photo to an embossed bas-relief mesh: grayscale luminance becomes height on
/// a heightmap grid (MeshMath.ExtrudeMaskedHeightfield), and any PNG alpha channel clips the
/// footprint to the image's actual silhouette instead of always producing a rectangular tile — a
/// JPG (or a fully-opaque PNG) has no alpha variation, so every cell ends up included and this
/// degenerates to a plain rectangular relief tile. Cross-platform via SkiaSharp decode + pixel
/// buffer sampling (replaces the previous GDI+ LockBits path).
/// </summary>
public class ImageMeshConverter : IImageMeshConverter
{
    private const float AlphaInclusionThreshold = 0.5f;

    public Mesh ConvertImageToMesh(ImageInsert insert)
    {
        using var bitmap = DecodeToRgba(insert.ImageData);

        int longerSamples = SamplesForDetail(insert.Detail);
        int cellCols, cellRows;
        if (bitmap.Width >= bitmap.Height)
        {
            cellCols = longerSamples;
            cellRows = Math.Max(1, (int)Math.Round(longerSamples * (float)bitmap.Height / bitmap.Width));
        }
        else
        {
            cellRows = longerSamples;
            cellCols = Math.Max(1, (int)Math.Round(longerSamples * (float)bitmap.Width / bitmap.Height));
        }

        var (cellLuminance, cellAlpha) = SampleCells(bitmap, cellRows, cellCols);

        var included = new bool[cellRows, cellCols];
        for (int r = 0; r < cellRows; r++)
        {
            for (int c = 0; c < cellCols; c++)
            {
                included[r, c] = cellAlpha[r, c] >= AlphaInclusionThreshold;
            }
        }

        int rows = cellRows + 1;
        int cols = cellCols + 1;
        var topZ = new float[rows, cols];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float luminance = AverageAdjacentCellLuminance(cellLuminance, cellRows, cellCols, row, col);
                float normalized = insert.Invert ? 1f - luminance : luminance;
                topZ[row, col] = normalized * insert.ReliefHeight;
            }
        }

        // The longer axis has `longerSamples` cells spanning insert.Scale mm, so each cell (and
        // the shorter axis, which uses the same physical cell size) is insert.Scale/longerSamples
        // mm — this is what fits the grid's physical footprint to Scale along its longer side.
        float cellSize = insert.Scale / longerSamples;

        var mesh = MeshMath.ExtrudeMaskedHeightfield(topZ, included, cellSize, cellSize, zBottom: 0);

        // ExtrudeMaskedHeightfield centers the grid on the image's full pixel bounds, not on the
        // included (opaque) footprint — an image with a lot of asymmetric transparent padding
        // (a logo pushed into one corner of a bigger canvas) would otherwise put local (0,0) —
        // the point PositionX/Y and viewport dragging actually move — away from the relief's
        // visual center, making a drag appear to yank it out from under the cursor at the start.
        var (min, max) = MeshMath.BoundingBox(mesh.Vertices.Select(v => new Vector2(v.X, v.Y)));
        var footprintCenter = (min + max) / 2f;
        return mesh.Transformed(new Vector3(-footprintCenter.X, -footprintCenter.Y, 0f), rotationZRadians: 0f);
    }

    public IReadOnlyList<Mesh> ConvertMultipleImageInserts(IReadOnlyList<ImageInsert> inserts) =>
        inserts.Select(ConvertImageToMesh).ToList();

    private static int SamplesForDetail(ImageDetail detail) => detail switch
    {
        ImageDetail.Low => 32,
        ImageDetail.Medium => 64,
        ImageDetail.High => 128,
        _ => 64
    };

    private static SKBitmap DecodeToRgba(byte[] imageData)
    {
        using var decoded = SKBitmap.Decode(imageData)
            ?? throw new InvalidOperationException("Could not decode image data.");

        if (decoded.ColorType == SKColorType.Rgba8888 && decoded.AlphaType != SKAlphaType.Opaque)
        {
            // Already a convenient layout; clone so the using-scope owns a unique instance.
            return decoded.Copy() ?? throw new InvalidOperationException("Could not copy decoded image.");
        }

        var info = new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var converted = new SKBitmap(info);
        if (!decoded.CopyTo(converted))
        {
            converted.Dispose();
            throw new InvalidOperationException("Could not convert image to RGBA.");
        }

        return converted;
    }

    /// <summary>Box-samples every cell's block of source pixels into average luminance (0..1) and
    /// average alpha (0..1) from a tightly packed RGBA8888 buffer — this runs on every live-preview
    /// regeneration.</summary>
    private static (float[,] Luminance, float[,] Alpha) SampleCells(SKBitmap source, int cellRows, int cellCols)
    {
        var luminance = new float[cellRows, cellCols];
        var alpha = new float[cellRows, cellCols];

        IntPtr pixels = source.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            return (luminance, alpha);
        }

        int width = source.Width;
        int height = source.Height;
        int rowBytes = source.RowBytes;
        // Copy once into managed memory for safe, bounds-checked indexing.
        var buffer = new byte[rowBytes * height];
        System.Runtime.InteropServices.Marshal.Copy(pixels, buffer, 0, buffer.Length);

        for (int cellRow = 0; cellRow < cellRows; cellRow++)
        {
            int y0 = (int)((long)cellRow * height / cellRows);
            int y1 = Math.Min(height, Math.Max(y0 + 1, (int)((long)(cellRow + 1) * height / cellRows)));

            for (int cellCol = 0; cellCol < cellCols; cellCol++)
            {
                int x0 = (int)((long)cellCol * width / cellCols);
                int x1 = Math.Min(width, Math.Max(x0 + 1, (int)((long)(cellCol + 1) * width / cellCols)));

                long sumLuminance = 0;
                long sumAlpha = 0;
                int count = 0;
                for (int y = y0; y < y1; y++)
                {
                    int rowOffset = y * rowBytes;
                    for (int x = x0; x < x1; x++)
                    {
                        int i = rowOffset + x * 4;
                        byte r = buffer[i];
                        byte g = buffer[i + 1];
                        byte b = buffer[i + 2];
                        byte a = buffer[i + 3];
                        sumLuminance += (long)(0.299 * r + 0.587 * g + 0.114 * b);
                        sumAlpha += a;
                        count++;
                    }
                }

                luminance[cellRow, cellCol] = count > 0 ? sumLuminance / (255f * count) : 0f;
                // Fully-opaque sources (JPG / opaque PNG) have a=255 everywhere → alpha averages to 1.
                alpha[cellRow, cellCol] = count > 0 ? sumAlpha / (255f * count) : 1f;
            }
        }

        return (luminance, alpha);
    }

    /// <summary>A grid vertex sits at the corner of up to 4 cells (fewer at edges/corners) —
    /// averaging those cells' luminance gives a smooth, continuous height field without a second,
    /// separate per-vertex sampling pass.</summary>
    private static float AverageAdjacentCellLuminance(float[,] cellLuminance, int cellRows, int cellCols, int row, int col)
    {
        float sum = 0;
        int count = 0;
        foreach (var (r, c) in new[] { (row - 1, col - 1), (row - 1, col), (row, col - 1), (row, col) })
        {
            if (r >= 0 && r < cellRows && c >= 0 && c < cellCols)
            {
                sum += cellLuminance[r, c];
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }
}
