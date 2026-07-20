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

    public List<TextLine> TextLines { get; set; } = new();

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
