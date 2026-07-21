using Microsoft.Data.Sqlite;
using ModelGenerator.Core.Models;
using ModelGenerator.Data.Database;

namespace ModelGenerator.Data.Repository;

public class SqliteModelRepository : IModelRepository
{
    // Fallback defaults if a column is unexpectedly NULL — matches DatabaseInitializer's SQL
    // DEFAULT values (System.Drawing.Color.LightSteelBlue/.DarkOrange.ToArgb()).
    private const int LightSteelBlueArgb = -5192482;
    private const int DarkOrangeArgb = -29696;

    private readonly ConnectionFactory _connectionFactory;

    public SqliteModelRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Model?> GetModelByIdAsync(int modelId)
    {
        using var connection = _connectionFactory.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Models WHERE ModelId = @id;";
        command.Parameters.AddWithValue("@id", modelId);

        Model? model = null;
        using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                model = ReadModel(reader);
            }
        }

        if (model is null)
        {
            return null;
        }

        model.TextLines = await LoadTextLinesAsync(connection, modelId);
        model.SvgInserts = await LoadSvgInsertsAsync(connection, modelId);
        model.ImageInserts = await LoadImageInsertsAsync(connection, modelId);
        model.BorderTextLines = await LoadBorderTextLinesAsync(connection, modelId);
        return model;
    }

    public async Task<List<Model>> ListModelsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var models = new List<Model>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Models ORDER BY ModifiedDate DESC;";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                models.Add(ReadModel(reader));
            }
        }

        foreach (var model in models)
        {
            model.TextLines = await LoadTextLinesAsync(connection, model.Id);
            model.SvgInserts = await LoadSvgInsertsAsync(connection, model.Id);
            model.ImageInserts = await LoadImageInsertsAsync(connection, model.Id);
            model.BorderTextLines = await LoadBorderTextLinesAsync(connection, model.Id);
        }

        return models;
    }

    public async Task<int> SaveModelAsync(Model model)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        if (model.Id == 0)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Models (Name, ShapeType, ShapeSize, ShapeHeight, ShapeThickness, BorderThickness, BorderHeight, BaseColorArgb, BorderColorArgb, CustomShapeSvgContent, CustomShapeSourceFileName, ModifiedDate)
                VALUES (@name, @shapeType, @shapeSize, @shapeHeight, @shapeThickness, @borderThickness, @borderHeight, @baseColorArgb, @borderColorArgb, @customShapeSvgContent, @customShapeSourceFileName, datetime('now'));
                SELECT last_insert_rowid();
                """;
            AddModelParameters(insert, model);
            model.Id = Convert.ToInt32((long)(await insert.ExecuteScalarAsync())!);
        }
        else
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE Models SET
                    Name = @name,
                    ShapeType = @shapeType,
                    ShapeSize = @shapeSize,
                    ShapeHeight = @shapeHeight,
                    ShapeThickness = @shapeThickness,
                    BorderThickness = @borderThickness,
                    BorderHeight = @borderHeight,
                    BaseColorArgb = @baseColorArgb,
                    BorderColorArgb = @borderColorArgb,
                    CustomShapeSvgContent = @customShapeSvgContent,
                    CustomShapeSourceFileName = @customShapeSourceFileName,
                    ModifiedDate = datetime('now')
                WHERE ModelId = @id;
                """;
            AddModelParameters(update, model);
            update.Parameters.AddWithValue("@id", model.Id);
            await update.ExecuteNonQueryAsync();

            using var deleteTextLines = connection.CreateCommand();
            deleteTextLines.Transaction = transaction;
            deleteTextLines.CommandText = "DELETE FROM TextLines WHERE ModelId = @id;";
            deleteTextLines.Parameters.AddWithValue("@id", model.Id);
            await deleteTextLines.ExecuteNonQueryAsync();

            using var deleteSvgInserts = connection.CreateCommand();
            deleteSvgInserts.Transaction = transaction;
            deleteSvgInserts.CommandText = "DELETE FROM SvgInserts WHERE ModelId = @id;";
            deleteSvgInserts.Parameters.AddWithValue("@id", model.Id);
            await deleteSvgInserts.ExecuteNonQueryAsync();

            using var deleteImageInserts = connection.CreateCommand();
            deleteImageInserts.Transaction = transaction;
            deleteImageInserts.CommandText = "DELETE FROM ImageInserts WHERE ModelId = @id;";
            deleteImageInserts.Parameters.AddWithValue("@id", model.Id);
            await deleteImageInserts.ExecuteNonQueryAsync();

            using var deleteBorderText = connection.CreateCommand();
            deleteBorderText.Transaction = transaction;
            deleteBorderText.CommandText = "DELETE FROM BorderTextLines WHERE ModelId = @id;";
            deleteBorderText.Parameters.AddWithValue("@id", model.Id);
            await deleteBorderText.ExecuteNonQueryAsync();
        }

        foreach (var textLine in model.TextLines)
        {
            using var insertLine = connection.CreateCommand();
            insertLine.Transaction = transaction;
            insertLine.CommandText = """
                INSERT INTO TextLines
                    (ModelId, LineNumber, Content, FontName, FontSize, TextHeight, PositionMode, PositionX, PositionY, PositionZ, RotationZ, ColorArgb)
                VALUES
                    (@modelId, @lineNumber, @content, @fontName, @fontSize, @textHeight, @positionMode, @positionX, @positionY, @positionZ, @rotationZ, @colorArgb);
                """;
            insertLine.Parameters.AddWithValue("@modelId", model.Id);
            insertLine.Parameters.AddWithValue("@lineNumber", textLine.LineNumber);
            insertLine.Parameters.AddWithValue("@content", textLine.Content);
            insertLine.Parameters.AddWithValue("@fontName", textLine.FontName);
            insertLine.Parameters.AddWithValue("@fontSize", textLine.FontSize);
            insertLine.Parameters.AddWithValue("@textHeight", textLine.TextHeight);
            insertLine.Parameters.AddWithValue("@positionMode", (int)textLine.PositionMode);
            insertLine.Parameters.AddWithValue("@positionX", textLine.PositionX);
            insertLine.Parameters.AddWithValue("@positionY", textLine.PositionY);
            insertLine.Parameters.AddWithValue("@positionZ", textLine.PositionZ);
            insertLine.Parameters.AddWithValue("@rotationZ", textLine.RotationZ);
            insertLine.Parameters.AddWithValue("@colorArgb", textLine.ColorArgb);
            await insertLine.ExecuteNonQueryAsync();
        }

        foreach (var svgInsert in model.SvgInserts)
        {
            using var insertSvg = connection.CreateCommand();
            insertSvg.Transaction = transaction;
            insertSvg.CommandText = """
                INSERT INTO SvgInserts
                    (ModelId, LineNumber, SourceFileName, SvgContent, Scale, EmbossHeight, PositionMode, PositionX, PositionY, PositionZ, RotationZ, ColorArgb)
                VALUES
                    (@modelId, @lineNumber, @sourceFileName, @svgContent, @scale, @embossHeight, @positionMode, @positionX, @positionY, @positionZ, @rotationZ, @colorArgb);
                """;
            insertSvg.Parameters.AddWithValue("@modelId", model.Id);
            insertSvg.Parameters.AddWithValue("@lineNumber", svgInsert.LineNumber);
            insertSvg.Parameters.AddWithValue("@sourceFileName", (object?)svgInsert.SourceFileName ?? DBNull.Value);
            insertSvg.Parameters.AddWithValue("@svgContent", svgInsert.SvgContent);
            insertSvg.Parameters.AddWithValue("@scale", svgInsert.Scale);
            insertSvg.Parameters.AddWithValue("@embossHeight", svgInsert.EmbossHeight);
            insertSvg.Parameters.AddWithValue("@positionMode", (int)svgInsert.PositionMode);
            insertSvg.Parameters.AddWithValue("@positionX", svgInsert.PositionX);
            insertSvg.Parameters.AddWithValue("@positionY", svgInsert.PositionY);
            insertSvg.Parameters.AddWithValue("@positionZ", svgInsert.PositionZ);
            insertSvg.Parameters.AddWithValue("@rotationZ", svgInsert.RotationZ);
            insertSvg.Parameters.AddWithValue("@colorArgb", svgInsert.ColorArgb);
            await insertSvg.ExecuteNonQueryAsync();
        }

        foreach (var imageInsert in model.ImageInserts)
        {
            using var insertImage = connection.CreateCommand();
            insertImage.Transaction = transaction;
            insertImage.CommandText = """
                INSERT INTO ImageInserts
                    (ModelId, LineNumber, SourceFileName, ImageData, Scale, ReliefHeight, Detail, Invert, PositionMode, PositionX, PositionY, PositionZ, RotationZ, ColorArgb)
                VALUES
                    (@modelId, @lineNumber, @sourceFileName, @imageData, @scale, @reliefHeight, @detail, @invert, @positionMode, @positionX, @positionY, @positionZ, @rotationZ, @colorArgb);
                """;
            insertImage.Parameters.AddWithValue("@modelId", model.Id);
            insertImage.Parameters.AddWithValue("@lineNumber", imageInsert.LineNumber);
            insertImage.Parameters.AddWithValue("@sourceFileName", (object?)imageInsert.SourceFileName ?? DBNull.Value);
            insertImage.Parameters.AddWithValue("@imageData", imageInsert.ImageData);
            insertImage.Parameters.AddWithValue("@scale", imageInsert.Scale);
            insertImage.Parameters.AddWithValue("@reliefHeight", imageInsert.ReliefHeight);
            insertImage.Parameters.AddWithValue("@detail", (int)imageInsert.Detail);
            insertImage.Parameters.AddWithValue("@invert", imageInsert.Invert ? 1 : 0);
            insertImage.Parameters.AddWithValue("@positionMode", (int)imageInsert.PositionMode);
            insertImage.Parameters.AddWithValue("@positionX", imageInsert.PositionX);
            insertImage.Parameters.AddWithValue("@positionY", imageInsert.PositionY);
            insertImage.Parameters.AddWithValue("@positionZ", imageInsert.PositionZ);
            insertImage.Parameters.AddWithValue("@rotationZ", imageInsert.RotationZ);
            insertImage.Parameters.AddWithValue("@colorArgb", imageInsert.ColorArgb);
            await insertImage.ExecuteNonQueryAsync();
        }

        foreach (var borderText in model.BorderTextLines)
        {
            using var insertBorder = connection.CreateCommand();
            insertBorder.Transaction = transaction;
            insertBorder.CommandText = """
                INSERT INTO BorderTextLines
                    (ModelId, LineNumber, Content, FontName, FontSize, Height, Mode, AnchorAngleDegrees, ColorArgb)
                VALUES
                    (@modelId, @lineNumber, @content, @fontName, @fontSize, @height, @mode, @anchorAngleDegrees, @colorArgb);
                """;
            insertBorder.Parameters.AddWithValue("@modelId", model.Id);
            insertBorder.Parameters.AddWithValue("@lineNumber", borderText.LineNumber);
            insertBorder.Parameters.AddWithValue("@content", borderText.Content);
            insertBorder.Parameters.AddWithValue("@fontName", borderText.FontName);
            insertBorder.Parameters.AddWithValue("@fontSize", borderText.FontSize);
            insertBorder.Parameters.AddWithValue("@height", borderText.Height);
            insertBorder.Parameters.AddWithValue("@mode", (int)borderText.Mode);
            insertBorder.Parameters.AddWithValue("@anchorAngleDegrees", borderText.AnchorAngleDegrees);
            insertBorder.Parameters.AddWithValue("@colorArgb", borderText.ColorArgb);
            await insertBorder.ExecuteNonQueryAsync();
        }

        transaction.Commit();
        return model.Id;
    }

    public async Task DeleteModelAsync(int modelId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Models WHERE ModelId = @id;";
        command.Parameters.AddWithValue("@id", modelId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveMeshAsync(int modelId, Mesh mesh)
    {
        var (verticesJson, indicesJson, normalsJson) = MeshJson.Serialize(mesh);

        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO MeshCache (ModelId, VerticesJson, IndicesJson, NormalsJson, GeneratedDate)
            VALUES (@modelId, @vertices, @indices, @normals, datetime('now'))
            ON CONFLICT(ModelId) DO UPDATE SET
                VerticesJson = excluded.VerticesJson,
                IndicesJson = excluded.IndicesJson,
                NormalsJson = excluded.NormalsJson,
                GeneratedDate = excluded.GeneratedDate;
            """;
        command.Parameters.AddWithValue("@modelId", modelId);
        command.Parameters.AddWithValue("@vertices", verticesJson);
        command.Parameters.AddWithValue("@indices", indicesJson);
        command.Parameters.AddWithValue("@normals", normalsJson);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Mesh?> GetMeshAsync(int modelId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT VerticesJson, IndicesJson, NormalsJson FROM MeshCache WHERE ModelId = @id;";
        command.Parameters.AddWithValue("@id", modelId);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MeshJson.Deserialize(reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static void AddModelParameters(SqliteCommand command, Model model)
    {
        command.Parameters.AddWithValue("@name", model.Name);
        command.Parameters.AddWithValue("@shapeType", (int)model.ShapeType);
        command.Parameters.AddWithValue("@shapeSize", model.ShapeSize);
        command.Parameters.AddWithValue("@shapeHeight", model.ShapeHeight);
        command.Parameters.AddWithValue("@shapeThickness", model.ShapeThickness);
        command.Parameters.AddWithValue("@borderThickness", model.BorderThickness);
        command.Parameters.AddWithValue("@borderHeight", model.BorderHeight);
        command.Parameters.AddWithValue("@baseColorArgb", model.BaseColorArgb);
        command.Parameters.AddWithValue("@borderColorArgb", model.BorderColorArgb);
        command.Parameters.AddWithValue("@customShapeSvgContent", (object?)model.CustomShapeSvgContent ?? DBNull.Value);
        command.Parameters.AddWithValue("@customShapeSourceFileName", (object?)model.CustomShapeSourceFileName ?? DBNull.Value);
    }

    private static Model ReadModel(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("ModelId")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        ShapeType = (ShapeType)reader.GetInt32(reader.GetOrdinal("ShapeType")),
        ShapeSize = (float)reader.GetDouble(reader.GetOrdinal("ShapeSize")),
        ShapeHeight = (float)reader.GetDouble(reader.GetOrdinal("ShapeHeight")),
        ShapeThickness = (float)reader.GetDouble(reader.GetOrdinal("ShapeThickness")),
        BorderThickness = (float)reader.GetDouble(reader.GetOrdinal("BorderThickness")),
        BorderHeight = (float)reader.GetDouble(reader.GetOrdinal("BorderHeight")),
        BaseColorArgb = GetInt32OrDefault(reader, "BaseColorArgb", LightSteelBlueArgb),
        BorderColorArgb = GetInt32OrDefault(reader, "BorderColorArgb", LightSteelBlueArgb),
        CustomShapeSvgContent = GetStringOrNull(reader, "CustomShapeSvgContent"),
        CustomShapeSourceFileName = GetStringOrNull(reader, "CustomShapeSourceFileName"),
        CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
        ModifiedDate = reader.GetDateTime(reader.GetOrdinal("ModifiedDate"))
    };

    private static async Task<List<TextLine>> LoadTextLinesAsync(SqliteConnection connection, int modelId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM TextLines WHERE ModelId = @id ORDER BY LineNumber;";
        command.Parameters.AddWithValue("@id", modelId);

        var lines = new List<TextLine>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(new TextLine
            {
                Id = reader.GetInt32(reader.GetOrdinal("TextLineId")),
                LineNumber = reader.GetInt32(reader.GetOrdinal("LineNumber")),
                Content = reader.GetString(reader.GetOrdinal("Content")),
                FontName = reader.GetString(reader.GetOrdinal("FontName")),
                FontSize = (float)reader.GetDouble(reader.GetOrdinal("FontSize")),
                TextHeight = (float)reader.GetDouble(reader.GetOrdinal("TextHeight")),
                PositionMode = (TextPositionMode)reader.GetInt32(reader.GetOrdinal("PositionMode")),
                PositionX = reader.IsDBNull(reader.GetOrdinal("PositionX")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionX")),
                PositionY = reader.IsDBNull(reader.GetOrdinal("PositionY")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionY")),
                PositionZ = reader.IsDBNull(reader.GetOrdinal("PositionZ")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionZ")),
                RotationZ = (float)reader.GetDouble(reader.GetOrdinal("RotationZ")),
                ColorArgb = GetInt32OrDefault(reader, "ColorArgb", DarkOrangeArgb)
            });
        }
        return lines;
    }

    private static async Task<List<SvgInsert>> LoadSvgInsertsAsync(SqliteConnection connection, int modelId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM SvgInserts WHERE ModelId = @id ORDER BY LineNumber;";
        command.Parameters.AddWithValue("@id", modelId);

        var inserts = new List<SvgInsert>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            inserts.Add(new SvgInsert
            {
                Id = reader.GetInt32(reader.GetOrdinal("SvgInsertId")),
                LineNumber = reader.GetInt32(reader.GetOrdinal("LineNumber")),
                SourceFileName = reader.IsDBNull(reader.GetOrdinal("SourceFileName")) ? null : reader.GetString(reader.GetOrdinal("SourceFileName")),
                SvgContent = reader.GetString(reader.GetOrdinal("SvgContent")),
                Scale = (float)reader.GetDouble(reader.GetOrdinal("Scale")),
                EmbossHeight = (float)reader.GetDouble(reader.GetOrdinal("EmbossHeight")),
                PositionMode = (TextPositionMode)reader.GetInt32(reader.GetOrdinal("PositionMode")),
                PositionX = reader.IsDBNull(reader.GetOrdinal("PositionX")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionX")),
                PositionY = reader.IsDBNull(reader.GetOrdinal("PositionY")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionY")),
                PositionZ = reader.IsDBNull(reader.GetOrdinal("PositionZ")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionZ")),
                RotationZ = (float)reader.GetDouble(reader.GetOrdinal("RotationZ")),
                ColorArgb = GetInt32OrDefault(reader, "ColorArgb", DarkOrangeArgb)
            });
        }
        return inserts;
    }

    private static async Task<List<ImageInsert>> LoadImageInsertsAsync(SqliteConnection connection, int modelId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ImageInserts WHERE ModelId = @id ORDER BY LineNumber;";
        command.Parameters.AddWithValue("@id", modelId);

        var inserts = new List<ImageInsert>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            inserts.Add(new ImageInsert
            {
                Id = reader.GetInt32(reader.GetOrdinal("ImageInsertId")),
                LineNumber = reader.GetInt32(reader.GetOrdinal("LineNumber")),
                SourceFileName = reader.IsDBNull(reader.GetOrdinal("SourceFileName")) ? null : reader.GetString(reader.GetOrdinal("SourceFileName")),
                ImageData = (byte[])reader["ImageData"],
                Scale = (float)reader.GetDouble(reader.GetOrdinal("Scale")),
                ReliefHeight = (float)reader.GetDouble(reader.GetOrdinal("ReliefHeight")),
                Detail = (ImageDetail)reader.GetInt32(reader.GetOrdinal("Detail")),
                Invert = reader.GetInt32(reader.GetOrdinal("Invert")) != 0,
                PositionMode = (TextPositionMode)reader.GetInt32(reader.GetOrdinal("PositionMode")),
                PositionX = reader.IsDBNull(reader.GetOrdinal("PositionX")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionX")),
                PositionY = reader.IsDBNull(reader.GetOrdinal("PositionY")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionY")),
                PositionZ = reader.IsDBNull(reader.GetOrdinal("PositionZ")) ? 0 : (float)reader.GetDouble(reader.GetOrdinal("PositionZ")),
                RotationZ = (float)reader.GetDouble(reader.GetOrdinal("RotationZ")),
                ColorArgb = GetInt32OrDefault(reader, "ColorArgb", DarkOrangeArgb)
            });
        }
        return inserts;
    }

    private static async Task<List<BorderTextLine>> LoadBorderTextLinesAsync(SqliteConnection connection, int modelId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM BorderTextLines WHERE ModelId = @id ORDER BY LineNumber;";
        command.Parameters.AddWithValue("@id", modelId);

        var lines = new List<BorderTextLine>();
        try
        {
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lines.Add(new BorderTextLine
                {
                    Id = reader.GetInt32(reader.GetOrdinal("BorderTextLineId")),
                    LineNumber = reader.GetInt32(reader.GetOrdinal("LineNumber")),
                    Content = reader.GetString(reader.GetOrdinal("Content")),
                    FontName = reader.GetString(reader.GetOrdinal("FontName")),
                    FontSize = (float)reader.GetDouble(reader.GetOrdinal("FontSize")),
                    Height = (float)reader.GetDouble(reader.GetOrdinal("Height")),
                    Mode = (BorderTextMode)reader.GetInt32(reader.GetOrdinal("Mode")),
                    AnchorAngleDegrees = (float)reader.GetDouble(reader.GetOrdinal("AnchorAngleDegrees")),
                    ColorArgb = GetInt32OrDefault(reader, "ColorArgb", DarkOrangeArgb)
                });
            }
        }
        catch (SqliteException)
        {
            // Older DBs without the table — treat as empty until Initialize recreates schema.
        }

        return lines;
    }

    /// <summary>Defensive fallback for a NULL color column — the DB migration backfills existing
    /// rows via ALTER TABLE ... DEFAULT, so this should only matter for hand-edited databases.</summary>
    private static int GetInt32OrDefault(SqliteDataReader reader, string column, int defaultValue)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
    }

    private static string? GetStringOrNull(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
