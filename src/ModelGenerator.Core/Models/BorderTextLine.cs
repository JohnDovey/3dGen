namespace ModelGenerator.Core.Models;

public enum BorderTextMode
{
    Embossed = 0,
    Engraved = 1
}

/// <summary>Which point of the text span AnchorAngleDegrees marks.</summary>
public enum BorderTextAnchorMode
{
    /// <summary>AnchorAngleDegrees marks the center of the (possibly shrunk) text span.</summary>
    Center = 0,

    /// <summary>AnchorAngleDegrees marks where the first character begins; text then runs
    /// counter-clockwise from there.</summary>
    Start = 1
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

    /// <summary>Angle on the border where AnchorMode's reference point (span center, or span
    /// start) sits; 0° = +X, CCW; 90° = top of shape.</summary>
    public float AnchorAngleDegrees { get; set; } = 90f;

    /// <summary>Whether AnchorAngleDegrees marks the span's center or its starting point.</summary>
    public BorderTextAnchorMode AnchorMode { get; set; } = BorderTextAnchorMode.Center;

    public int ColorArgb { get; set; } = ArgbColors.DarkOrange;
}
