using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services.ProjectBundle;

public interface IProjectBundleService
{
    /// <summary>Writes a <c>.mgproj</c> zip containing the model parameters and asset files.</summary>
    void ExportBundle(Model model, string zipFilePath, string? appVersion = null);

    /// <summary>Reads a <c>.mgproj</c> zip, imports new assets into the local libraries (content-hash
    /// dedup), and returns a fully self-contained <see cref="Model"/> with <c>Id = 0</c>.</summary>
    Model ImportBundle(string zipFilePath);
}
