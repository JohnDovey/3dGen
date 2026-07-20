using System.Drawing;

namespace ModelGenerator.Core.Models;

/// <summary>An SVG graphic embossed onto the shape's surface — positioned/adjusted the same way
/// as a TextLine (see IPositionable), but sized via Scale instead of FontSize.</summary>
public class SvgInsert : IPositionable
{
    public int Id { get; set; }
    public int LineNumber { get; set; }

    /// <summary>Library filename at insert time — display/reference only, not used for geometry
    /// (the model is self-contained via SvgContent, so it stays valid if the library file is
    /// later renamed or deleted).</summary>
    public string? SourceFileName { get; set; }

    /// <summary>Full SVG XML content, copied from the library at insert time.</summary>
    public string SvgContent { get; set; } = string.Empty;

    /// <summary>Target size in mm along the SVG's longer bounding-box dimension.</summary>
    public float Scale { get; set; } = 40f;

    /// <summary>Emboss height above the shape surface, in mm.</summary>
    public float EmbossHeight { get; set; } = 5f;

    public TextPositionMode PositionMode { get; set; } = TextPositionMode.AutoCenter;

    /// <summary>Position in mm. Meaningful when PositionMode is Manual or Relative.</summary>
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationZ { get; set; }

    public int ColorArgb { get; set; } = Color.DarkOrange.ToArgb();
}
