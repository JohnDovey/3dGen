using ModelGenerator.Data.Database;
using Xunit;

namespace ModelGenerator.Tests;

public class DatabaseInitializerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ConnectionFactory _connectionFactory;

    public DatabaseInitializerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.sqlite");
        _connectionFactory = new ConnectionFactory(_dbPath);
    }

    [Fact]
    public void Initialize_OnFreshDatabase_CreatesAllTablesAndColumns()
    {
        new DatabaseInitializer(_connectionFactory).Initialize();

        Assert.True(ColumnExists("Models", "BaseColorArgb"));
        Assert.True(ColumnExists("Models", "BorderColorArgb"));
        Assert.True(ColumnExists("TextLines", "ColorArgb"));
        Assert.True(TableExists("SvgInserts"));
    }

    [Fact]
    public void Initialize_OnExistingPreColorDatabase_MigratesInPlaceWithoutDataLoss()
    {
        // Simulate a database created by a version of the app before the color columns existed:
        // the same Models/TextLines tables, minus BaseColorArgb/BorderColorArgb/ColorArgb.
        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE Models (
                    ModelId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    ShapeType INTEGER NOT NULL,
                    ShapeSize REAL NOT NULL,
                    ShapeHeight REAL NOT NULL DEFAULT 40,
                    ShapeThickness REAL NOT NULL DEFAULT 10,
                    BorderThickness REAL NOT NULL DEFAULT 5,
                    BorderHeight REAL NOT NULL DEFAULT 5,
                    CreatedDate TEXT NOT NULL DEFAULT (datetime('now')),
                    ModifiedDate TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE TextLines (
                    TextLineId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ModelId INTEGER NOT NULL,
                    LineNumber INTEGER NOT NULL,
                    Content TEXT NOT NULL,
                    FontName TEXT NOT NULL,
                    FontSize REAL NOT NULL,
                    TextHeight REAL NOT NULL DEFAULT 5,
                    PositionMode INTEGER NOT NULL DEFAULT 0,
                    PositionX REAL,
                    PositionY REAL,
                    PositionZ REAL,
                    RotationZ REAL NOT NULL DEFAULT 0,
                    FOREIGN KEY (ModelId) REFERENCES Models(ModelId) ON DELETE CASCADE
                );
                """;
            command.ExecuteNonQuery();
        }

        // Pre-existing data that must survive the migration.
        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO Models (Name, ShapeType, ShapeSize) VALUES ('Existing Model', 0, 60);";
            command.ExecuteNonQuery();
        }

        Assert.False(ColumnExists("Models", "BaseColorArgb"));

        new DatabaseInitializer(_connectionFactory).Initialize();

        Assert.True(ColumnExists("Models", "BaseColorArgb"));
        Assert.True(ColumnExists("Models", "BorderColorArgb"));
        Assert.True(ColumnExists("TextLines", "ColorArgb"));
        Assert.True(TableExists("SvgInserts"));

        using var verifyConnection = _connectionFactory.CreateConnection();
        using var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT Name FROM Models WHERE Name = 'Existing Model';";
        Assert.Equal("Existing Model", verifyCommand.ExecuteScalar());
    }

    [Fact]
    public void Initialize_CalledTwice_DoesNotThrow()
    {
        var initializer = new DatabaseInitializer(_connectionFactory);
        initializer.Initialize();
        initializer.Initialize();

        Assert.True(ColumnExists("Models", "BaseColorArgb"));
    }

    private bool ColumnExists(string table, string column)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private bool TableExists(string table)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name;";
        command.Parameters.AddWithValue("@name", table);
        return command.ExecuteScalar() is not null;
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
