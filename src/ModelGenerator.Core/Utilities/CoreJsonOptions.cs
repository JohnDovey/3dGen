using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelGenerator.Core.Utilities;

/// <summary>Shared System.Text.Json settings for Core DTOs (bundles, etc.) and Host wire format.</summary>
public static class CoreJsonOptions
{
    public static JsonSerializerOptions Create(bool writeIndented = false) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = writeIndented,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public static readonly JsonSerializerOptions Default = Create(writeIndented: false);
    public static readonly JsonSerializerOptions Pretty = Create(writeIndented: true);
}
