namespace ModelGenerator.Core.Models;

public enum BorderTextMode
{
    Embossed = 0,
    Engraved = 1
}

/// <summary>Text laid out along the raised border's midline (coin-rim lettering).
/// Not <see cref="IPositionable"/> — placement is anchor angle + natural/shrink-to-fit arc length.</summary>
public class BorderTextLine
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public float FontSize { get; set; } = 8f;

    /// <summary>Emboss protrusion above the border top, or engrave depth into it (mm).</summary>
    public float Height { get; set; } = 1.5f;

    public BorderTextMode Mode { get; set; } = BorderTextMode.Embossed;

    /// <summary>Center of the text span on the border; 0° = +X, CCW; 90° = top of shape.</summary>
    public float AnchorAngleDegrees { get; set; } = 90f;

    public int ColorArgb { get; set; } = ArgbColors.DarkOrange;
}
