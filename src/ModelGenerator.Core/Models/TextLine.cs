using System.Drawing;

namespace ModelGenerator.Core.Models;

public class TextLine : IPositionable
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public float FontSize { get; set; } = 12f;

    /// <summary>Emboss height above the shape surface, in mm.</summary>
    public float TextHeight { get; set; } = 5f;

    public TextPositionMode PositionMode { get; set; } = TextPositionMode.AutoCenter;

    /// <summary>Position in mm. Meaningful when PositionMode is Manual or Relative.</summary>
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationZ { get; set; }

    public int ColorArgb { get; set; } = Color.DarkOrange.ToArgb();
}
