using WpfColor = System.Windows.Media.Color;

namespace ModelGenerator.UI.Controls;

/// <summary>Converts between the ARGB int stored on Core models (TextLine/SvgInsert/Model) and
/// the color types the UI actually renders/edits with (WPF for the viewport, WinForms for color
/// pickers) — kept in one place instead of duplicating the bit-shuffling per control.</summary>
public static class ColorArgbExtensions
{
    public static WpfColor ToWpfColor(this int argb)
    {
        var color = System.Drawing.Color.FromArgb(argb);
        return WpfColor.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static int ToArgb(this WpfColor color) =>
        System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B).ToArgb();

    public static int ToArgb(this System.Drawing.Color color) => color.ToArgb();
}
