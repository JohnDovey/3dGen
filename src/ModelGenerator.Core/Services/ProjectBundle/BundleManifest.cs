using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services.ProjectBundle;

/// <summary>Format version 1 project bundle manifest (zip entry <c>manifest.json</c>).
/// Asset paths are relative inside the zip; binary/text content is NOT base64-embedded here.</summary>
public sealed class BundleManifest
{
    public int FormatVersion { get; set; } = 1;
    public string? AppVersion { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public ShapeType ShapeType { get; set; }
    public float ShapeSize { get; set; }
    public float ShapeHeight { get; set; }
    public float ShapeThickness { get; set; }
    public float BorderThickness { get; set; }
    public float BorderHeight { get; set; }
    public int BaseColorArgb { get; set; }
    public int BorderColorArgb { get; set; }
    public string? CustomShapeAssetPath { get; set; }
    public string? CustomShapeSourceFileName { get; set; }
    public List<BundleTextLine> TextLines { get; set; } = new();
    public List<BundleSvgInsert> SvgInserts { get; set; } = new();
    public List<BundleImageInsert> ImageInserts { get; set; } = new();
    public List<BundleBorderTextLine> BorderTextLines { get; set; } = new();
}

public sealed class BundleTextLine
{
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public float FontSize { get; set; } = 12;
    public float TextHeight { get; set; } = 5;
    public TextPositionMode PositionMode { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationZ { get; set; }
    public int ColorArgb { get; set; }
}

public sealed class BundleSvgInsert
{
    public int LineNumber { get; set; }
    public string? SourceFileName { get; set; }
    public string AssetPath { get; set; } = string.Empty;
    public float Scale { get; set; }
    public float EmbossHeight { get; set; }
    public TextPositionMode PositionMode { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationZ { get; set; }
    public int ColorArgb { get; set; }
}

public sealed class BundleImageInsert
{
    public int LineNumber { get; set; }
    public string? SourceFileName { get; set; }
    public string AssetPath { get; set; } = string.Empty;
    public float Scale { get; set; }
    public float ReliefHeight { get; set; }
    public ImageDetail Detail { get; set; }
    public bool Invert { get; set; }
    public TextPositionMode PositionMode { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationZ { get; set; }
    public int ColorArgb { get; set; }
}

public sealed class BundleBorderTextLine
{
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public float FontSize { get; set; } = 8;
    public float Height { get; set; } = 1.5f;
    public BorderTextMode Mode { get; set; }
    public float AnchorAngleDegrees { get; set; } = 90f;
    public int ColorArgb { get; set; }
}
