namespace ModelGenerator.Core.Models;

/// <summary>Well-known default colors as packed ARGB ints — keeps Core free of System.Drawing
/// while matching the historical WinForms defaults (LightSteelBlue / DarkOrange).</summary>
public static class ArgbColors
{
    /// <summary>System.Drawing.Color.LightSteelBlue.ToArgb() == unchecked((int)0xFFB0C4DE).</summary>
    public const int LightSteelBlue = unchecked((int)0xFFB0C4DE);

    /// <summary>System.Drawing.Color.DarkOrange.ToArgb() == unchecked((int)0xFFFF8C00).</summary>
    public const int DarkOrange = unchecked((int)0xFFFF8C00);
}
