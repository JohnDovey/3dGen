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
