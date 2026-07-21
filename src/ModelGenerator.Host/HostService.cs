using System.Reflection;
using System.Text.Json;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Data.Database;
using ModelGenerator.Data.Repository;
using ModelGenerator.Host.Protocol;

namespace ModelGenerator.Host;

/// <summary>Pure method handlers for the host protocol — no transport. Used by the NDJSON
/// server and by one-shot CLI commands / unit tests.</summary>
public sealed class HostService
{
    private readonly IModelOrchestrator _orchestrator;
    private readonly IModelRepository _repository;
    private readonly string _appDataDir;

    public HostService(IModelOrchestrator orchestrator, IModelRepository repository, string appDataDir)
    {
        _orchestrator = orchestrator;
        _repository = repository;
        _appDataDir = appDataDir;
    }

    public string AppDataDir => _appDataDir;

    /// <summary>Production layout under LocalApplicationData/ModelGenerator (same as WinForms).</summary>
    public static HostService CreateDefault() => Create(DefaultAppDataDir());

    /// <summary>Isolated instance for tests (temp directory + fresh SQLite).</summary>
    public static HostService CreateForTesting(string appDataDir) => Create(appDataDir);

    public static string DefaultAppDataDir()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModelGenerator");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static HostService Create(string appDataDir)
    {
        Directory.CreateDirectory(appDataDir);
        Directory.CreateDirectory(Path.Combine(appDataDir, "SvgLibrary"));
        Directory.CreateDirectory(Path.Combine(appDataDir, "ImageLibrary"));

        var orchestrator = new ModelOrchestrator(
            new ShapeGenerator(),
            new TextMeshConverter(),
            new SvgMeshConverter(),
            new ImageMeshConverter(),
            new TextPositioner(),
            new MeshComposer());

        string dbPath = Path.Combine(appDataDir, "models.sqlite");
        var connectionFactory = new ConnectionFactory(dbPath);
        new DatabaseInitializer(connectionFactory).Initialize();
        var repository = new SqliteModelRepository(connectionFactory);

        return new HostService(orchestrator, repository, appDataDir);
    }

    public PingResult Ping() => new()
    {
        Ok = true,
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0",
        Protocol = HostProtocol.Version
    };

    public GeneratePartsResult GenerateParts(Model model)
    {
        var (floor, border, textMeshes, svgMeshes, imageMeshes) = _orchestrator.GenerateModelParts(model);

        var result = new GeneratePartsResult
        {
            Floor = WireMesh.FromMesh(floor, model.BaseColorArgb),
            Border = WireMesh.FromMesh(border, model.BorderColorArgb),
            TextMeshes = textMeshes.Select((t, i) => new WirePositionedMesh
            {
                Index = i,
                ColorArgb = t.Line.ColorArgb,
                Mesh = WireMesh.FromMesh(t.Mesh, t.Line.ColorArgb)
            }).ToList(),
            SvgMeshes = svgMeshes.Select((s, i) => new WirePositionedMesh
            {
                Index = i,
                ColorArgb = s.Insert.ColorArgb,
                Mesh = WireMesh.FromMesh(s.Mesh, s.Insert.ColorArgb)
            }).ToList(),
            ImageMeshes = imageMeshes.Select((im, i) => new WirePositionedMesh
            {
                Index = i,
                ColorArgb = im.Insert.ColorArgb,
                Mesh = WireMesh.FromMesh(im.Mesh, im.Insert.ColorArgb)
            }).ToList()
        };

        int vertices = result.Floor.Vertices.Length / 3
            + result.Border.Vertices.Length / 3
            + result.TextMeshes.Sum(t => t.Mesh.Vertices.Length / 3)
            + result.SvgMeshes.Sum(s => s.Mesh.Vertices.Length / 3)
            + result.ImageMeshes.Sum(im => im.Mesh.Vertices.Length / 3);
        int indices = result.Floor.Indices.Length
            + result.Border.Indices.Length
            + result.TextMeshes.Sum(t => t.Mesh.Indices.Length)
            + result.SvgMeshes.Sum(s => s.Mesh.Indices.Length)
            + result.ImageMeshes.Sum(im => im.Mesh.Indices.Length);

        result.VertexCount = vertices;
        result.TriangleCount = indices / 3;
        return result;
    }

    public ExportStlResult ExportStl(Model model, string path)
    {
        var mesh = _orchestrator.GenerateModel(model);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _orchestrator.ExportSTL(mesh, path);
        var info = new FileInfo(path);
        return new ExportStlResult
        {
            Path = Path.GetFullPath(path),
            Bytes = info.Length,
            VertexCount = mesh.Vertices.Count,
            TriangleCount = mesh.Indices.Count / 3
        };
    }

    public ListModelsResult ListModels()
    {
        var models = _repository.ListModelsAsync().GetAwaiter().GetResult();
        return new ListModelsResult
        {
            Models = models.Select(m => new ModelSummaryDto
            {
                Id = m.Id,
                Name = m.Name,
                ShapeType = (int)m.ShapeType,
                ModifiedDate = m.ModifiedDate
            }).ToList()
        };
    }

    public GetModelResult GetModel(int id)
    {
        var model = _repository.GetModelByIdAsync(id).GetAwaiter().GetResult()
            ?? throw new HostInvalidParamsException($"Model id {id} was not found.");
        return new GetModelResult { Model = model };
    }

    /// <summary>Inserts or updates the model and optionally caches the generated mesh (like WinForms Save).</summary>
    public SaveModelResult SaveModel(Model model, bool saveMesh = true)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new HostInvalidParamsException("model.name is required.");
        }

        int id = _repository.SaveModelAsync(model).GetAwaiter().GetResult();
        model.Id = id;

        if (saveMesh)
        {
            var mesh = _orchestrator.GenerateModel(model);
            _repository.SaveMeshAsync(id, mesh).GetAwaiter().GetResult();
        }

        return new SaveModelResult { Id = id, Name = model.Name };
    }

    public DeleteModelResult DeleteModel(int id)
    {
        _repository.DeleteModelAsync(id).GetAwaiter().GetResult();
        return new DeleteModelResult { Id = id, Deleted = true };
    }

    public object Dispatch(string method, JsonElement? paramsElement)
    {
        return method.ToLowerInvariant() switch
        {
            "ping" => Ping(),
            "generateparts" or "generate_parts" => GenerateParts(RequireModel(paramsElement)),
            "exportstl" or "export_stl" => ExportStl(
                RequireModel(paramsElement),
                RequireString(paramsElement, "path", "filePath", "stlPath")),
            "listmodels" or "list_models" => ListModels(),
            "getmodel" or "get_model" => GetModel(RequireInt(paramsElement, "id", "modelId")),
            "savemodel" or "save_model" => SaveModel(
                RequireModel(paramsElement),
                OptionalBool(paramsElement, true, "saveMesh", "cacheMesh")),
            "deletemodel" or "delete_model" => DeleteModel(RequireInt(paramsElement, "id", "modelId")),
            _ => throw new HostMethodNotFoundException(method)
        };
    }

    private static Model RequireModel(JsonElement? paramsElement)
    {
        if (paramsElement is null || paramsElement.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new HostInvalidParamsException("params.model is required.");
        }

        var root = paramsElement.Value;
        JsonElement modelElement;
        if (root.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(root, "model", out modelElement))
        {
            // ok
        }
        else if (root.ValueKind == JsonValueKind.Object && LooksLikeModel(root))
        {
            modelElement = root;
        }
        else
        {
            throw new HostInvalidParamsException("params.model is required.");
        }

        var model = JsonSerializer.Deserialize<Model>(modelElement.GetRawText(), HostProtocol.JsonOptions)
            ?? throw new HostInvalidParamsException("params.model could not be deserialized.");
        return model;
    }

    private static bool LooksLikeModel(JsonElement obj) =>
        TryGetPropertyIgnoreCase(obj, "shapeType", out _) || TryGetPropertyIgnoreCase(obj, "name", out _);

    private static int RequireInt(JsonElement? paramsElement, params string[] names)
    {
        if (paramsElement is null || paramsElement.Value.ValueKind != JsonValueKind.Object)
        {
            throw new HostInvalidParamsException($"One of [{string.Join(", ", names)}] is required.");
        }

        foreach (var name in names)
        {
            if (TryGetPropertyIgnoreCase(paramsElement.Value, name, out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int n))
                {
                    return n;
                }
                if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out int parsed))
                {
                    return parsed;
                }
            }
        }

        throw new HostInvalidParamsException($"One of [{string.Join(", ", names)}] is required.");
    }

    private static bool OptionalBool(JsonElement? paramsElement, bool defaultValue, params string[] names)
    {
        if (paramsElement is null || paramsElement.Value.ValueKind != JsonValueKind.Object)
        {
            return defaultValue;
        }

        foreach (var name in names)
        {
            if (TryGetPropertyIgnoreCase(paramsElement.Value, name, out var el))
            {
                if (el.ValueKind is JsonValueKind.True) return true;
                if (el.ValueKind is JsonValueKind.False) return false;
            }
        }

        return defaultValue;
    }

    private static string RequireString(JsonElement? paramsElement, params string[] names)
    {
        if (paramsElement is null || paramsElement.Value.ValueKind != JsonValueKind.Object)
        {
            throw new HostInvalidParamsException($"One of [{string.Join(", ", names)}] is required.");
        }

        foreach (var name in names)
        {
            if (TryGetPropertyIgnoreCase(paramsElement.Value, name, out var el)
                && el.ValueKind == JsonValueKind.String)
            {
                string? s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }

        throw new HostInvalidParamsException($"One of [{string.Join(", ", names)}] is required.");
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

public sealed class HostMethodNotFoundException(string method)
    : Exception($"Unknown method: {method}")
{
    public string Method { get; } = method;
}

public sealed class HostInvalidParamsException(string message) : Exception(message);
