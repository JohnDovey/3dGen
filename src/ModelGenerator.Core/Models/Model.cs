namespace ModelGenerator.Core.Models;

public class Model
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ShapeType ShapeType { get; set; }

    /// <summary>Diameter (Circle) or width (Triangle/Shield/Rectangle), in mm.</summary>
    public float ShapeSize { get; set; } = 60f;

    /// <summary>Height, in mm. Only used by Rectangle; other shapes derive height from ShapeSize.</summary>
    public float ShapeHeight { get; set; } = 40f;

    public float ShapeThickness { get; set; } = 10f;
    public float BorderThickness { get; set; } = 5f;
    public float BorderHeight { get; set; } = 5f;

    public int BaseColorArgb { get; set; } = ArgbColors.LightSteelBlue;
    public int BorderColorArgb { get; set; } = ArgbColors.LightSteelBlue;

    /// <summary>Full SVG XML content defining the shape's outline, used only when ShapeType is
    /// CustomSvg — self-contained like SvgInsert.SvgContent, so the model stays valid even if the
    /// library file is later renamed or deleted.</summary>
    public string? CustomShapeSvgContent { get; set; }

    /// <summary>Library filename at selection time — display/reference only, not used for
    /// geometry.</summary>
    public string? CustomShapeSourceFileName { get; set; }

    public List<TextLine> TextLines { get; set; } = new();
    public List<SvgInsert> SvgInserts { get; set; } = new();
    public List<ImageInsert> ImageInserts { get; set; } = new();

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
