namespace ModelGenerator.Data.Database;

public class DatabaseInitializer
{
    private readonly ConnectionFactory _connectionFactory;

    public DatabaseInitializer(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Models (
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
}
