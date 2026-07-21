using System.Text.Json;
using ModelGenerator.Core.Models;
using ModelGenerator.Host;
using ModelGenerator.Host.Protocol;

// Usage:
//   ModelGenerator.Host serve --socket <path>
//   ModelGenerator.Host stdio
//   ModelGenerator.Host export --model <path.json> --stl <path.stl>
//   ModelGenerator.Host generate-parts --model <path.json> [--out <path.json>]
//   ModelGenerator.Host ping

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return 0;
}

string command = args[0].ToLowerInvariant();
var rest = args.Skip(1).ToArray();

try
{
    return command switch
    {
        "serve" => await RunServeAsync(rest),
        "stdio" => await RunStdioAsync(),
        "export" => RunExport(rest),
        "generate-parts" or "generateparts" => RunGenerateParts(rest),
        "ping" => RunPing(),
        _ => Unknown(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 2;
}

static void PrintHelp()
{
    Console.Error.WriteLine("""
        ModelGenerator.Host — headless Core/Data bridge for the Mac (SwiftUI) UI

        Commands:
          serve --socket <path>              Listen for NDJSON RPC on a Unix domain socket
          stdio                              NDJSON RPC on stdin/stdout (one request/response per line)
          export --model <json> --stl <path> One-shot: generate model and write binary STL
          generate-parts --model <json> [--out <json>]
                                             One-shot: write GenerateParts result JSON
          ping                               Print host/protocol version as JSON

        RPC methods (protocol 1.0): ping, generateParts, exportStl
        See docs/HOST_PROTOCOL.md for the wire format.
        """);
}

static async Task<int> RunServeAsync(string[] args)
{
    string? socket = GetOption(args, "--socket", "-s");
    if (string.IsNullOrWhiteSpace(socket))
    {
        // Default under Application Support-style path when possible.
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModelGenerator");
        Directory.CreateDirectory(appData);
        socket = Path.Combine(appData, "host.sock");
    }

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var service = HostService.CreateDefault();
    var server = new UnixSocketServer(service, socket);
    await server.RunAsync(cts.Token).ConfigureAwait(false);
    return 0;
}

static async Task<int> RunStdioAsync()
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var service = HostService.CreateDefault();
    // Use OpenStandard so we don't dispose the process stdin/stdout.
    var session = new JsonRpcSession(service, Console.OpenStandardInput(), Console.OpenStandardOutput());
    await session.RunAsync(cts.Token).ConfigureAwait(false);
    return 0;
}

static int RunExport(string[] args)
{
    string modelPath = RequireOption(args, "--model", "-m");
    string stlPath = RequireOption(args, "--stl", "-o");
    var model = LoadModel(modelPath);
    var service = HostService.CreateDefault();
    var result = service.ExportStl(model, stlPath);
    Console.WriteLine(JsonSerializer.Serialize(result, HostProtocol.PrettyJsonOptions));
    return 0;
}

static int RunGenerateParts(string[] args)
{
    string modelPath = RequireOption(args, "--model", "-m");
    string? outPath = GetOption(args, "--out", "-o");
    var model = LoadModel(modelPath);
    var service = HostService.CreateDefault();
    var result = service.GenerateParts(model);
    string json = JsonSerializer.Serialize(result, HostProtocol.PrettyJsonOptions);
    if (!string.IsNullOrWhiteSpace(outPath))
    {
        string? dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(outPath, json);
        Console.Error.WriteLine($"Wrote {outPath} ({result.VertexCount} verts, {result.TriangleCount} tris)");
    }
    else
    {
        Console.WriteLine(json);
    }
    return 0;
}

static int RunPing()
{
    var service = HostService.CreateDefault();
    Console.WriteLine(JsonSerializer.Serialize(service.Ping(), HostProtocol.PrettyJsonOptions));
    return 0;
}

static Model LoadModel(string path)
{
    string json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<Model>(json, HostProtocol.JsonOptions)
        ?? throw new InvalidOperationException($"Could not deserialize model from {path}");
}

static string? GetOption(string[] args, params string[] names)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (names.Contains(args[i], StringComparer.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }
    return null;
}

static string RequireOption(string[] args, params string[] names) =>
    GetOption(args, names) ?? throw new InvalidOperationException($"Missing required option {names[0]}");
