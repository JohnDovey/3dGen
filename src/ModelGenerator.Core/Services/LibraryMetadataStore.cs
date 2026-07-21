using System.Text.Json;

namespace ModelGenerator.Core.Services;

/// <summary>Stores search keywords per library file in a JSON sidecar (`_metadata.json`) inside
/// the library folder — shared by SvgLibraryService and ImageLibraryService so both libraries get
/// identical tagging/search/delete-cleanup behavior without duplicating the bookkeeping twice.
/// No Windows-specific dependency (plain file + JSON I/O), unlike the services that use it.</summary>
public class LibraryMetadataStore
{
    private readonly string _metadataFilePath;

    public LibraryMetadataStore(string libraryDirectory)
    {
        _metadataFilePath = Path.Combine(libraryDirectory, "_metadata.json");
    }

    public IReadOnlyList<string> GetKeywords(string fileName)
    {
        var all = Load();
        return all.TryGetValue(fileName, out var keywords) ? keywords : Array.Empty<string>();
    }

    public void SetKeywords(string fileName, IReadOnlyList<string> keywords)
    {
        var all = Load();
        var cleaned = keywords
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            all.Remove(fileName);
        }
        else
        {
            all[fileName] = cleaned;
        }
        Save(all);
    }

    /// <summary>Called when a library file is deleted, so its stale keywords don't linger.</summary>
    public void RemoveEntry(string fileName)
    {
        var all = Load();
        if (all.Remove(fileName))
        {
            Save(all);
        }
    }

    /// <summary>Returns the subset of fileNames whose own name or keywords contain `query`
    /// (case-insensitive) — a blank query matches everything.</summary>
    public IReadOnlyList<string> Filter(IEnumerable<string> fileNames, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return fileNames.ToList();
        }

        var all = Load();
        return fileNames
            .Where(f => f.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (all.TryGetValue(f, out var keywords) && keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    private Dictionary<string, List<string>> Load()
    {
        if (!File.Exists(_metadataFilePath))
        {
            return new Dictionary<string, List<string>>();
        }

        try
        {
            string json = File.ReadAllText(_metadataFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                ?? new Dictionary<string, List<string>>();
        }
        catch (Exception)
        {
            // A corrupt/hand-edited metadata file must not break browsing the library — just
            // behave as if no keywords have been set yet.
            return new Dictionary<string, List<string>>();
        }
    }

    private void Save(Dictionary<string, List<string>> all)
    {
        string json = JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_metadataFilePath, json);
    }
}
