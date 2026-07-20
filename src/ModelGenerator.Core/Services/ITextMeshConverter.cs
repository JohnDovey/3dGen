using ModelGenerator.Core.Models;

namespace ModelGenerator.Core.Services;

/// <summary>Converts text lines into embossed 3D geometry. Implemented in Phase 2.</summary>
public interface ITextMeshConverter
{
    Mesh ConvertTextToMesh(TextLine textLine);
    IReadOnlyList<Mesh> ConvertMultilineText(IReadOnlyList<TextLine> textLines);
}
