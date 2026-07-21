using System.Reflection;
using System.Text.Json;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Host.Protocol;

namespace ModelGenerator.Host;

/// <summary>Pure method handlers for the host protocol — no transport. Used by the NDJSON
/// server and by one-shot CLI commands / unit tests.</summary>
public sealed class HostService
{
    private readonly IModelOrchestrator _orchestrator;

    public HostService(IModelOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public static HostService CreateDefault()
    {
        var orchestrator = new ModelOrchestrator(
            new ShapeGenerator(),
            new TextMeshConverter(),
            new SvgMeshConverter(),
            new ImageMeshConverter(),
            new TextPositioner(),
            new MeshComposer());
        return new HostService(orchestrator);
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

    /// <summary>Builds the merged mesh from the model and writes a binary STL to <paramref name="path"/>.</summary>
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

    public object Dispatch(string method, JsonElement? paramsElement)
    {
        return method.ToLowerInvariant() switch
        {
            "ping" => Ping(),
            "generateparts" or "generate_parts" => GenerateParts(RequireModel(paramsElement)),
            "exportstl" or "export_stl" => ExportStl(
                RequireModel(paramsElement),
                RequireString(paramsElement, "path", "filePath", "stlPath")),
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
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("model", out modelElement))
        {
            // ok
        }
        else if (root.ValueKind == JsonValueKind.Object && LooksLikeModel(root))
        {
            // Allow bare model object as params for convenience in one-shot files.
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
        obj.TryGetProperty("shapeType", out _) || obj.TryGetProperty("ShapeType", out _);

    private static string RequireString(JsonElement? paramsElement, params string[] names)
    {
        if (paramsElement is null || paramsElement.Value.ValueKind != JsonValueKind.Object)
        {
            throw new HostInvalidParamsException($"One of [{string.Join(", ", names)}] is required.");
        }

        foreach (var name in names)
        {
            if (paramsElement.Value.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                string? s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }

            // Case-insensitive fallback
            foreach (var prop in paramsElement.Value.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)
                    && prop.Value.ValueKind == JsonValueKind.String)
                {
                    string? s = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                }
            }
        }

        throw new HostInvalidParamsException($"One of [{string.Join(", ", names)}] is required.");
    }
}

public sealed class HostMethodNotFoundException(string method)
    : Exception($"Unknown method: {method}")
{
    public string Method { get; } = method;
}

public sealed class HostInvalidParamsException(string message) : Exception(message);
