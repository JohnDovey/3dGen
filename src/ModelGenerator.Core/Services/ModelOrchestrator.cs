using System.Runtime.Versioning;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>Wires shape generation, text-to-mesh conversion, positioning, and composition into
/// the full parameters -> mesh workflow. Windows-only for v1 (depends on TextMeshConverter).</summary>
[SupportedOSPlatform("windows")]
public class ModelOrchestrator : IModelOrchestrator
{
    private readonly IShapeGenerator _shapeGenerator;
    private readonly ITextMeshConverter _textMeshConverter;
    private readonly ITextPositioner _textPositioner;
    private readonly IMeshComposer _meshComposer;

    public ModelOrchestrator(
        IShapeGenerator shapeGenerator,
        ITextMeshConverter textMeshConverter,
        ITextPositioner textPositioner,
        IMeshComposer meshComposer)
    {
        _shapeGenerator = shapeGenerator;
        _textMeshConverter = textMeshConverter;
        _textPositioner = textPositioner;
        _meshComposer = meshComposer;
    }

    public Mesh GenerateModel(Model model)
    {
        var baseMesh = _shapeGenerator.Generate(model);
        var textMeshes = _textMeshConverter.ConvertMultilineText(model.TextLines);

        var autoCenterMeshes = model.TextLines
            .Select((line, index) => (line, index))
            .Where(x => x.line.PositionMode == TextPositionMode.AutoCenter)
            .Select(x => textMeshes[x.index])
            .ToList();

        var autoCenterTransforms = autoCenterMeshes.Count > 0
            ? _textPositioner.AutoCenter(autoCenterMeshes, model)
            : Array.Empty<Transform>();

        var transforms = new Transform[model.TextLines.Count];
        int autoCursor = 0;
        for (int i = 0; i < model.TextLines.Count; i++)
        {
            var line = model.TextLines[i];
            transforms[i] = line.PositionMode switch
            {
                TextPositionMode.AutoCenter => autoCenterTransforms[autoCursor++],
                TextPositionMode.Manual => _textPositioner.ApplyManualOffset(line),
                TextPositionMode.Relative => _textPositioner.CalculateRelativeCoords(line, model),
                _ => throw new ArgumentOutOfRangeException(nameof(model), line.PositionMode, "Unknown text position mode.")
            };
        }

        return _meshComposer.ComposeModel(baseMesh, textMeshes, transforms);
    }

    public void ExportSTL(Mesh mesh, string filePath) => STLExporter.ExportToSTL(mesh, filePath);
}
