using ModelGenerator.Core.Models;

namespace ModelGenerator.Data.Repository;

public interface IModelRepository
{
    Task<Model?> GetModelByIdAsync(int modelId);
    Task<List<Model>> ListModelsAsync();

    /// <summary>Inserts or updates the model (and its text lines). Returns the ModelId.</summary>
    Task<int> SaveModelAsync(Model model);

    Task DeleteModelAsync(int modelId);

    Task SaveMeshAsync(int modelId, Mesh mesh);
    Task<Mesh?> GetMeshAsync(int modelId);
}
