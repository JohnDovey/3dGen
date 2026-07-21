using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>
/// Converts a JPG/PNG photo to an embossed bas-relief mesh: grayscale luminance becomes height on
/// a heightmap grid (MeshMath.ExtrudeMaskedHeightfield), and any PNG alpha channel clips the
/// footprint to the image's actual silhouette instead of always producing a rectangular tile — a
/// JPG (or a fully-opaque PNG) has no alpha variation, so every cell ends up included and this
/// degenerates to a plain rectangular relief tile. Windows-only for v1, same as
/// TextMeshConverter/SvgMeshConverter (depends on System.Drawing/GDI+).
/// </summary>
[SupportedOSPlatform("windows")]
public class ImageMeshConverter : IImageMeshConverter
{
    private const float AlphaInclusionThreshold = 0.5f;

    public Mesh ConvertImageToMesh(ImageInsert insert)
    {
        using var bitmap = new Bitmap(new MemoryStream(insert.ImageData));

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

        return MeshMath.ExtrudeMaskedHeightfield(topZ, included, cellSize, cellSize, zBottom: 0);
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

    /// <summary>Box-samples every cell's block of source pixels into average luminance (0..1) and
    /// average alpha (0..1), via LockBits for speed — this runs on every live-preview
    /// regeneration, and per-pixel GetPixel calls are far too slow for anything but tiny images.
    /// Requesting Format32bppArgb from LockBits works regardless of the source's native pixel
    /// format (GDI+ converts internally), so this handles JPGs (no alpha) and any PNG variant.</summary>
    private static (float[,] Luminance, float[,] Alpha) SampleCells(Bitmap source, int cellRows, int cellCols)
    {
        var luminance = new float[cellRows, cellCols];
        var alpha = new float[cellRows, cellCols];

        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            int byteCount = stride * source.Height;
            var buffer = new byte[byteCount];
            Marshal.Copy(data.Scan0, buffer, 0, byteCount);

            for (int cellRow = 0; cellRow < cellRows; cellRow++)
            {
                int y0 = (int)((long)cellRow * source.Height / cellRows);
                int y1 = Math.Min(source.Height, Math.Max(y0 + 1, (int)((long)(cellRow + 1) * source.Height / cellRows)));

                for (int cellCol = 0; cellCol < cellCols; cellCol++)
                {
                    int x0 = (int)((long)cellCol * source.Width / cellCols);
                    int x1 = Math.Min(source.Width, Math.Max(x0 + 1, (int)((long)(cellCol + 1) * source.Width / cellCols)));

                    long sumLuminance = 0;
                    long sumAlpha = 0;
                    int count = 0;
                    for (int y = y0; y < y1; y++)
                    {
                        int rowOffset = y * stride;
                        for (int x = x0; x < x1; x++)
                        {
                            int i = rowOffset + x * 4;
                            byte b = buffer[i];
                            byte g = buffer[i + 1];
                            byte r = buffer[i + 2];
                            byte a = buffer[i + 3];
                            sumLuminance += (long)(0.299 * r + 0.587 * g + 0.114 * b);
                            sumAlpha += a;
                            count++;
                        }
                    }

                    luminance[cellRow, cellCol] = count > 0 ? sumLuminance / (255f * count) : 0f;
                    alpha[cellRow, cellCol] = count > 0 ? sumAlpha / (255f * count) : 1f;
                }
            }
        }
        finally
        {
            source.UnlockBits(data);
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
