using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Data.Database;
using ModelGenerator.Data.Repository;
using Xunit;

namespace ModelGenerator.Tests;

public class SqliteModelRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteModelRepository _repository;

    public SqliteModelRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.sqlite");
        var connectionFactory = new ConnectionFactory(_dbPath);
        new DatabaseInitializer(connectionFactory).Initialize();
        _repository = new SqliteModelRepository(connectionFactory);
    }

    [Fact]
    public async Task SaveAndRetrieveModel_RoundTripsShapeAndTextLines()
    {
        var model = new Model
        {
            Name = "Test Tag",
            ShapeType = ShapeType.Rectangle,
            ShapeSize = 80,
            ShapeHeight = 50,
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "HELLO", FontName = "Arial", FontSize = 14 },
                new TextLine { LineNumber = 1, Content = "WORLD", FontName = "Consolas", FontSize = 10, PositionMode = TextPositionMode.Relative, PositionX = 5, PositionY = -3 }
            }
        };

        int id = await _repository.SaveModelAsync(model);
        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal("Test Tag", loaded!.Name);
        Assert.Equal(ShapeType.Rectangle, loaded.ShapeType);
        Assert.Equal(2, loaded.TextLines.Count);
        Assert.Equal("HELLO", loaded.TextLines[0].Content);
        Assert.Equal(TextPositionMode.Relative, loaded.TextLines[1].PositionMode);
    }

    [Fact]
    public async Task SaveAndRetrieveModel_RoundTripsColorsAndSvgInserts()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
                <rect x="0" y="0" width="10" height="10" />
            </svg>
            """;
        var model = new Model
        {
            Name = "Colorful Model",
            ShapeType = ShapeType.Circle,
            ShapeSize = 60,
            BaseColorArgb = -2354116,
            BorderColorArgb = -10496,
            TextLines =
            {
                new TextLine { LineNumber = 0, Content = "HI", ColorArgb = -16776961 }
            },
            SvgInserts =
            {
                new SvgInsert
                {
                    LineNumber = 0,
                    SourceFileName = "logo.svg",
                    SvgContent = svg,
                    Scale = 25,
                    EmbossHeight = 6,
                    PositionMode = TextPositionMode.Manual,
                    PositionX = 10,
                    PositionY = 20,
                    PositionZ = 5,
                    RotationZ = 45,
                    ColorArgb = -16744448
                }
            }
        };

        int id = await _repository.SaveModelAsync(model);
        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(-2354116, loaded!.BaseColorArgb);
        Assert.Equal(-10496, loaded.BorderColorArgb);
        Assert.Equal(-16776961, loaded.TextLines[0].ColorArgb);

        Assert.Single(loaded.SvgInserts);
        var svgInsert = loaded.SvgInserts[0];
        Assert.Equal("logo.svg", svgInsert.SourceFileName);
        Assert.Equal(svg, svgInsert.SvgContent);
        Assert.Equal(25, svgInsert.Scale);
        Assert.Equal(6, svgInsert.EmbossHeight);
        Assert.Equal(TextPositionMode.Manual, svgInsert.PositionMode);
        Assert.Equal(10, svgInsert.PositionX);
        Assert.Equal(20, svgInsert.PositionY);
        Assert.Equal(5, svgInsert.PositionZ);
        Assert.Equal(45, svgInsert.RotationZ);
        Assert.Equal(-16744448, svgInsert.ColorArgb);
    }

    [Fact]
    public async Task SaveAndRetrieveModel_RoundTripsCustomShapeSvg()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
                <rect x="0" y="0" width="10" height="10" />
            </svg>
            """;
        var model = new Model
        {
            Name = "Custom Shape Model",
            ShapeType = ShapeType.CustomSvg,
            ShapeSize = 60,
            CustomShapeSvgContent = svg,
            CustomShapeSourceFileName = "outline.svg"
        };

        int id = await _repository.SaveModelAsync(model);
        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(ShapeType.CustomSvg, loaded!.ShapeType);
        Assert.Equal(svg, loaded.CustomShapeSvgContent);
        Assert.Equal("outline.svg", loaded.CustomShapeSourceFileName);
    }

    [Fact]
    public async Task SaveAndRetrieveModel_WithoutCustomShapeSvg_RoundTripsAsNull()
    {
        var model = new Model { Name = "Plain Circle", ShapeType = ShapeType.Circle, ShapeSize = 60 };

        int id = await _repository.SaveModelAsync(model);
        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.CustomShapeSvgContent);
        Assert.Null(loaded.CustomShapeSourceFileName);
    }

    [Fact]
    public async Task SaveAndRetrieveModel_RoundTripsImageInsertByteForByte()
    {
        byte[] imageData = CreateSampleImageBytes();
        var model = new Model
        {
            Name = "Photo Model",
            ShapeType = ShapeType.Circle,
            ShapeSize = 60,
            ImageInserts =
            {
                new ImageInsert
                {
                    LineNumber = 0,
                    SourceFileName = "photo.png",
                    ImageData = imageData,
                    Scale = 30,
                    ReliefHeight = 4,
                    Detail = ImageDetail.High,
                    Invert = true,
                    PositionMode = TextPositionMode.Relative,
                    PositionX = 5,
                    PositionY = -8,
                    PositionZ = 3,
                    RotationZ = 90,
                    ColorArgb = -8388480
                }
            }
        };

        int id = await _repository.SaveModelAsync(model);
        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.ImageInserts);
        var imageInsert = loaded.ImageInserts[0];
        Assert.Equal("photo.png", imageInsert.SourceFileName);
        Assert.Equal(imageData, imageInsert.ImageData);
        Assert.Equal(30, imageInsert.Scale);
        Assert.Equal(4, imageInsert.ReliefHeight);
        Assert.Equal(ImageDetail.High, imageInsert.Detail);
        Assert.True(imageInsert.Invert);
        Assert.Equal(TextPositionMode.Relative, imageInsert.PositionMode);
        Assert.Equal(5, imageInsert.PositionX);
        Assert.Equal(-8, imageInsert.PositionY);
        Assert.Equal(3, imageInsert.PositionZ);
        Assert.Equal(90, imageInsert.RotationZ);
        Assert.Equal(-8388480, imageInsert.ColorArgb);
    }

    [Fact]
    public async Task SaveAndRetrieveModel_RoundTripsBorderTextLines()
    {
        var model = new Model
        {
            Name = "Rim Text Model",
            ShapeType = ShapeType.Circle,
            ShapeSize = 60,
            BorderTextLines =
            {
                new BorderTextLine
                {
                    LineNumber = 0,
                    Content = "ENGRAVED",
                    FontName = "Arial",
                    FontSize = 9,
                    Height = 2,
                    Mode = BorderTextMode.Engraved,
                    AnchorAngleDegrees = 180,
                    AnchorMode = BorderTextAnchorMode.Start,
                    ColorArgb = -8388480
                }
            }
        };

        int id = await _repository.SaveModelAsync(model);
        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.BorderTextLines);
        var borderText = loaded.BorderTextLines[0];
        Assert.Equal("ENGRAVED", borderText.Content);
        Assert.Equal("Arial", borderText.FontName);
        Assert.Equal(9, borderText.FontSize);
        Assert.Equal(2, borderText.Height);
        Assert.Equal(BorderTextMode.Engraved, borderText.Mode);
        Assert.Equal(180, borderText.AnchorAngleDegrees);
        Assert.Equal(BorderTextAnchorMode.Start, borderText.AnchorMode);
        Assert.Equal(-8388480, borderText.ColorArgb);
    }

    [Fact]
    public async Task SaveModelAsync_OnUpdate_ReplacesBorderTextLinesLikeSvgInserts()
    {
        var model = new Model
        {
            Name = "Updatable Rim Text",
            ShapeType = ShapeType.Circle,
            ShapeSize = 60,
            BorderTextLines = { new BorderTextLine { LineNumber = 0, Content = "OLD" } }
        };
        int id = await _repository.SaveModelAsync(model);

        model.BorderTextLines.Clear();
        model.BorderTextLines.Add(new BorderTextLine { LineNumber = 0, Content = "NEW" });
        await _repository.SaveModelAsync(model);

        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.BorderTextLines);
        Assert.Equal("NEW", loaded.BorderTextLines[0].Content);
    }

    [Fact]
    public async Task SaveModelAsync_OnUpdate_ReplacesImageInsertsLikeSvgInserts()
    {
        byte[] imageData = CreateSampleImageBytes();
        var model = new Model
        {
            Name = "Updatable Photo",
            ShapeType = ShapeType.Circle,
            ShapeSize = 60,
            ImageInserts = { new ImageInsert { LineNumber = 0, ImageData = imageData } }
        };
        int id = await _repository.SaveModelAsync(model);

        model.ImageInserts.Clear();
        model.ImageInserts.Add(new ImageInsert { LineNumber = 0, ImageData = imageData, Scale = 77 });
        await _repository.SaveModelAsync(model);

        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.ImageInserts);
        Assert.Equal(77, loaded.ImageInserts[0].Scale);
    }

    private static byte[] CreateSampleImageBytes() => TestPng.Solid(10, 10, SkiaSharp.SKColors.Gray);

    [Fact]
    public async Task SaveModelAsync_OnUpdate_ReplacesSvgInsertsLikeTextLines()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10" /></svg>""";
        var model = new Model
        {
            Name = "Updatable",
            ShapeType = ShapeType.Circle,
            ShapeSize = 60,
            SvgInserts = { new SvgInsert { LineNumber = 0, SvgContent = svg } }
        };
        int id = await _repository.SaveModelAsync(model);

        model.SvgInserts.Clear();
        model.SvgInserts.Add(new SvgInsert { LineNumber = 0, SvgContent = svg, Scale = 99 });
        await _repository.SaveModelAsync(model);

        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.SvgInserts);
        Assert.Equal(99, loaded.SvgInserts[0].Scale);
    }

    [Fact]
    public async Task SaveAndRetrieveMesh_RoundTripsGeometry()
    {
        var model = new Model { Name = "Mesh Round Trip", ShapeType = ShapeType.Circle, ShapeSize = 60 };
        int id = await _repository.SaveModelAsync(model);

        var mesh = new ShapeGenerator().GenerateCircle(diameter: 60, thickness: 10, borderThickness: 5, borderHeight: 5);
        await _repository.SaveMeshAsync(id, mesh);

        var loadedMesh = await _repository.GetMeshAsync(id);

        Assert.NotNull(loadedMesh);
        Assert.Equal(mesh.Vertices.Count, loadedMesh!.Vertices.Count);
        Assert.Equal(mesh.Indices.Count, loadedMesh.Indices.Count);
    }

    [Fact]
    public async Task DeleteModel_RemovesModelAndCascadesTextLines()
    {
        var model = new Model { Name = "To Delete", ShapeType = ShapeType.Circle, ShapeSize = 60 };
        int id = await _repository.SaveModelAsync(model);

        await _repository.DeleteModelAsync(id);
        var loaded = await _repository.GetModelByIdAsync(id);

        Assert.Null(loaded);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
