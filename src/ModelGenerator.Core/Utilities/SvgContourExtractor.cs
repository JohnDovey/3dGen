using System.Numerics;
using SkiaSharp;
using Svg.Model.Drawables;
using Svg.Skia;

namespace ModelGenerator.Core.Utilities;

/// <summary>Extracts flattened 2D contours (document space, Y-up) from every path-bearing drawable
/// of an SVG document — shared by SvgMeshConverter (every contour, so embossed inserts keep their
/// holes/cutouts) and ShapeGenerator's CustomSvg shape (the single largest contour, used as the
/// shape's outer boundary). Uses Svg.Skia (cross-platform) instead of GDI+ GraphicsPath.</summary>
public static class SvgContourExtractor
{
    public static List<List<Vector2>> ExtractContours(string svgContent)
    {
        using var skSvg = new SKSvg();
        skSvg.FromSvg(svgContent);
        if (skSvg.Drawable is not DrawableBase root)
        {
            return new List<List<Vector2>>();
        }

        var contours = new List<List<Vector2>>();
        CollectContours(root, skSvg.SkiaModel, contours);
        return contours;
    }

    private static void CollectContours(DrawableBase drawable, SkiaModel skiaModel, List<List<Vector2>> contours)
    {
        if (drawable is DrawablePath { Path: not null } pathDrawable)
        {
            using var skPath = skiaModel.ToSKPath(pathDrawable.Path);
            skPath.Transform(skiaModel.ToSKMatrix(pathDrawable.TotalTransform));
            contours.AddRange(SkiaPathContours.ExtractContours(skPath));
        }

        if (drawable is DrawableContainer container)
        {
            foreach (var child in container.ChildrenDrawables)
            {
                CollectContours(child, skiaModel, contours);
            }
        }
    }
}
