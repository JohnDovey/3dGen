using System.Drawing;

namespace ModelGenerator.Core.Models;

public enum ImageDetail
{
    Low,
    Medium,
    High
}

/// <summary>A JPG/PNG photo embossed onto the shape's surface as a grayscale bas-relief —
/// positioned/adjusted the same way as a TextLine/SvgInsert (see IPositionable), but converted to
/// geometry via a heightmap grid instead of contour extrusion.</summary>
public class ImageInsert : IPositionable
{
    public int Id { get; set; }
    public int LineNumber { get; set; }

    /// <summary>Library filename at insert time — display/reference only, not used for geometry
    /// (the model is self-contained via ImageData, so it stays valid if the library file is later
    /// renamed or deleted).</summary>
    public string? SourceFileName { get; set; }

    /// <summary>Raw PNG/JPG bytes, copied from the library at insert time.</summary>
    public byte[] ImageData { get; set; } = [];

    /// <summary>Target size in mm along the image's longer bounding-box dimension.</summary>
    public float Scale { get; set; } = 40f;

    /// <summary>Maximum raised height above the shape surface, in mm.</summary>
    public float ReliefHeight { get; set; } = 3f;

    /// <summary>Heightmap grid resolution (samples along the longer side) — trades relief/edge
    /// fidelity for triangle count and live-preview responsiveness.</summary>
    public ImageDetail Detail { get; set; } = ImageDetail.Medium;

    /// <summary>When true, darker pixels are raised higher instead of brighter ones.</summary>
    public bool Invert { get; set; }

    public TextPositionMode PositionMode { get; set; } = TextPositionMode.AutoCenter;

    /// <summary>Position in mm. Meaningful when PositionMode is Manual or Relative.</summary>
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationZ { get; set; }

    public int ColorArgb { get; set; } = Color.DarkOrange.ToArgb();
}
