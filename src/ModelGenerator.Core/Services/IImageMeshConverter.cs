using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public interface IImageMeshConverter
{
    Mesh ConvertImageToMesh(ImageInsert insert);
    IReadOnlyList<Mesh> ConvertMultipleImageInserts(IReadOnlyList<ImageInsert> inserts);
}
