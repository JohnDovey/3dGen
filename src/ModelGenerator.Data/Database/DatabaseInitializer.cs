using Microsoft.Data.Sqlite;

namespace ModelGenerator.Data.Database;

public class DatabaseInitializer
{
    // System.Drawing.Color.LightSteelBlue/.DarkOrange.ToArgb() — computed once via .NET itself
    // (not hand-typed) and hardcoded here since these are embedded directly in SQL DEFAULT
    // clauses, which can't evaluate a C# expression.
    private const int LightSteelBlueArgb = -5192482;
    private const int DarkOrangeArgb = -29696;

    private readonly ConnectionFactory _connectionFactory;

    public DatabaseInitializer(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateConnection();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS Models (
                    ModelId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    ShapeType INTEGER NOT NULL,
                    ShapeSize REAL NOT NULL,
                    ShapeHeight REAL NOT NULL DEFAULT 40,
                    ShapeThickness REAL NOT NULL DEFAULT 10,
                    BorderThickness REAL NOT NULL DEFAULT 5,
                    BorderHeight REAL NOT NULL DEFAULT 5,
                    BaseColorArgb INTEGER NOT NULL DEFAULT {LightSteelBlueArgb},
                    BorderColorArgb INTEGER NOT NULL DEFAULT {LightSteelBlueArgb},
                    CustomShapeSvgContent TEXT,
                    CustomShapeSourceFileName TEXT,
                    CreatedDate TEXT NOT NULL DEFAULT (datetime('now')),
                    ModifiedDate TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS TextLines (
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
                    ColorArgb INTEGER NOT NULL DEFAULT {DarkOrangeArgb},
                    FOREIGN KEY (ModelId) REFERENCES Models(ModelId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS SvgInserts (
                    SvgInsertId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ModelId INTEGER NOT NULL,
                    LineNumber INTEGER NOT NULL,
                    SourceFileName TEXT,
                    SvgContent TEXT NOT NULL,
                    Scale REAL NOT NULL DEFAULT 40,
                    EmbossHeight REAL NOT NULL DEFAULT 5,
                    PositionMode INTEGER NOT NULL DEFAULT 0,
                    PositionX REAL,
                    PositionY REAL,
                    PositionZ REAL,
                    RotationZ REAL NOT NULL DEFAULT 0,
                    ColorArgb INTEGER NOT NULL DEFAULT {DarkOrangeArgb},
                    FOREIGN KEY (ModelId) REFERENCES Models(ModelId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ImageInserts (
                    ImageInsertId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ModelId INTEGER NOT NULL,
                    LineNumber INTEGER NOT NULL,
                    SourceFileName TEXT,
                    ImageData BLOB NOT NULL,
                    Scale REAL NOT NULL DEFAULT 40,
                    ReliefHeight REAL NOT NULL DEFAULT 3,
                    Detail INTEGER NOT NULL DEFAULT 1,
                    Invert INTEGER NOT NULL DEFAULT 0,
                    PositionMode INTEGER NOT NULL DEFAULT 0,
                    PositionX REAL,
                    PositionY REAL,
                    PositionZ REAL,
                    RotationZ REAL NOT NULL DEFAULT 0,
                    ColorArgb INTEGER NOT NULL DEFAULT {DarkOrangeArgb},
                    FOREIGN KEY (ModelId) REFERENCES Models(ModelId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS BorderTextLines (
                    BorderTextLineId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ModelId INTEGER NOT NULL,
                    LineNumber INTEGER NOT NULL,
                    Content TEXT NOT NULL,
                    FontName TEXT NOT NULL,
                    FontSize REAL NOT NULL DEFAULT 8,
                    Height REAL NOT NULL DEFAULT 1.5,
                    Mode INTEGER NOT NULL DEFAULT 0,
                    AnchorAngleDegrees REAL NOT NULL DEFAULT 90,
                    AnchorMode INTEGER NOT NULL DEFAULT 0,
                    ColorArgb INTEGER NOT NULL DEFAULT {DarkOrangeArgb},
                    FOREIGN KEY (ModelId) REFERENCES Models(ModelId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS MeshCache (
                    MeshCacheId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ModelId INTEGER NOT NULL UNIQUE,
                    VerticesJson TEXT NOT NULL,
                    IndicesJson TEXT NOT NULL,
                    NormalsJson TEXT NOT NULL,
                    GeneratedDate TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY (ModelId) REFERENCES Models(ModelId) ON DELETE CASCADE
                );
                """;
            command.ExecuteNonQuery();
        }

        // CREATE TABLE IF NOT EXISTS is a no-op against tables that already existed before this
        // version added new columns — on an already-deployed models.sqlite, Models/TextLines
        // exist without BaseColorArgb/BorderColorArgb/ColorArgb, so those columns must be added
        // explicitly or every subsequent insert referencing them fails with "no such column".
        AddColumnIfMissing(connection, "Models", "BaseColorArgb", $"INTEGER NOT NULL DEFAULT {LightSteelBlueArgb}");
        AddColumnIfMissing(connection, "Models", "BorderColorArgb", $"INTEGER NOT NULL DEFAULT {LightSteelBlueArgb}");
        AddColumnIfMissing(connection, "TextLines", "ColorArgb", $"INTEGER NOT NULL DEFAULT {DarkOrangeArgb}");
        AddColumnIfMissing(connection, "Models", "CustomShapeSvgContent", "TEXT");
        AddColumnIfMissing(connection, "Models", "CustomShapeSourceFileName", "TEXT");
        AddColumnIfMissing(connection, "BorderTextLines", "AnchorMode", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string table, string column, string columnDefinition)
    {
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = $"PRAGMA table_info({table});";
            using var reader = checkCommand.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }
}
