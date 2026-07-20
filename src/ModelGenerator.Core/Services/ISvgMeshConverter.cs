using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

public interface ISvgMeshConverter
{
    Mesh ConvertSvgToMesh(SvgInsert insert);
    IReadOnlyList<Mesh> ConvertMultipleSvgInserts(IReadOnlyList<SvgInsert> inserts);
}
