using ModelGenerator.Core.Services;
using Xunit;

namespace ModelGenerator.Tests;

public class LibraryMetadataStoreTests : IDisposable
{
    private readonly string _libraryDir;
    private readonly LibraryMetadataStore _store;

    public LibraryMetadataStoreTests()
    {
        _libraryDir = Path.Combine(Path.GetTempPath(), $"libmeta-{Guid.NewGuid()}");
        Directory.CreateDirectory(_libraryDir);
        _store = new LibraryMetadataStore(_libraryDir);
    }

    [Fact]
    public void GetKeywords_ForUnknownFile_ReturnsEmpty()
    {
        Assert.Empty(_store.GetKeywords("missing.svg"));
    }

    [Fact]
    public void SetKeywords_ThenGetKeywords_RoundTrips()
    {
        _store.SetKeywords("logo.svg", new[] { "Logo", "Brand" });

        Assert.Equal(new[] { "Logo", "Brand" }, _store.GetKeywords("logo.svg"));
    }

    [Fact]
    public void SetKeywords_PersistsAcrossNewStoreInstance()
    {
        _store.SetKeywords("logo.svg", new[] { "Logo" });

        var reloaded = new LibraryMetadataStore(_libraryDir);

        Assert.Equal(new[] { "Logo" }, reloaded.GetKeywords("logo.svg"));
    }

    [Fact]
    public void SetKeywords_TrimsBlankAndDuplicateEntries()
    {
        _store.SetKeywords("logo.svg", new[] { " Logo ", "logo", "", "  ", "Brand" });

        var keywords = _store.GetKeywords("logo.svg");
        Assert.Equal(2, keywords.Count);
        Assert.Contains("Logo", keywords);
        Assert.Contains("Brand", keywords);
    }

    [Fact]
    public void SetKeywords_ToEmptyList_RemovesTheEntry()
    {
        _store.SetKeywords("logo.svg", new[] { "Logo" });
        _store.SetKeywords("logo.svg", Array.Empty<string>());

        Assert.Empty(_store.GetKeywords("logo.svg"));
    }

    [Fact]
    public void RemoveEntry_DeletesStoredKeywords()
    {
        _store.SetKeywords("logo.svg", new[] { "Logo" });

        _store.RemoveEntry("logo.svg");

        Assert.Empty(_store.GetKeywords("logo.svg"));
    }

    [Fact]
    public void Filter_BlankQuery_ReturnsEverything()
    {
        var fileNames = new[] { "logo.svg", "icon.svg" };

        Assert.Equal(fileNames, _store.Filter(fileNames, ""));
    }

    [Fact]
    public void Filter_MatchesByFileName()
    {
        var fileNames = new[] { "logo.svg", "icon.svg" };

        Assert.Equal(new[] { "logo.svg" }, _store.Filter(fileNames, "LOGO"));
    }

    [Fact]
    public void Filter_MatchesByKeyword()
    {
        _store.SetKeywords("icon.svg", new[] { "Star" });
        var fileNames = new[] { "logo.svg", "icon.svg" };

        Assert.Equal(new[] { "icon.svg" }, _store.Filter(fileNames, "star"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_libraryDir))
        {
            Directory.Delete(_libraryDir, recursive: true);
        }
    }
}
