using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelGenerator.Host.Protocol;

/// <summary>Versioned NDJSON request/response envelope for the Mac host process.
/// One JSON object per line; client sends requests, server replies with matching id.</summary>
public static class HostProtocol
{
    public const string Version = "1.0";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        // Model enums (ShapeType, TextPositionMode, ImageDetail) travel as numbers — stable wire format.
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };
}

/// <summary>Incoming request line.</summary>
public sealed class RpcRequest
{
    public string? Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public JsonElement? Params { get; set; }
}

/// <summary>Outgoing response line — either Result or Error is set.</summary>
public sealed class RpcResponse
{
    public string? Id { get; set; }
    public object? Result { get; set; }
    public RpcError? Error { get; set; }
}

public sealed class RpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class RpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
}
