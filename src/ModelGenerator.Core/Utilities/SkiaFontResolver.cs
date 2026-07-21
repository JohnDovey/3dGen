using SkiaSharp;

namespace ModelGenerator.Core.Utilities;

/// <summary>Shared typeface resolution for text and border-text converters.</summary>
public static class SkiaFontResolver
{
    public static SKTypeface ResolveTypeface(string fontName)
    {
        var typeface = SKTypeface.FromFamilyName(fontName);
        if (typeface is not null && !string.IsNullOrEmpty(typeface.FamilyName))
        {
            return typeface;
        }

        return SKTypeface.Default;
    }
}
