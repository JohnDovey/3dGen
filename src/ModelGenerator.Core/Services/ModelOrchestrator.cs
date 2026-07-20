using System.Runtime.Versioning;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>Wires shape generation, text/SVG-to-mesh conversion, positioning, and composition
/// into the full parameters -> mesh workflow. Windows-only for v1 (depends on
/// TextMeshConverter/SvgMeshConverter).</summary>
[SupportedOSPlatform("windows")]
public class ModelOrchestrator : IModelOrchestrator
{
    private readonly IShapeGenerator _shapeGenerator;
    private readonly ITextMeshConverter _textMeshConverter;
    private readonly ISvgMeshConverter _svgMeshConverter;
    private readonly ITextPositioner _textPositioner;
    private readonly IMeshComposer _meshComposer;

    public ModelOrchestrator(
        IShapeGenerator shapeGenerator,
        ITextMeshConverter textMeshConverter,
        ISvgMeshConverter svgMeshConverter,
        ITextPositioner textPositioner,
        IMeshComposer meshComposer)
    {
        _shapeGenerator = shapeGenerator;
        _textMeshConverter = textMeshConverter;
        _svgMeshConverter = svgMeshConverter;
        _textPositioner = textPositioner;
        _meshComposer = meshComposer;
    }

    public Mesh GenerateModel(Model model)
    {
        var (floor, border, textMeshes, svgMeshes) = GenerateModelParts(model);

        // Every mesh already has its transform baked in, so this is a plain merge. Color is
        // irrelevant for STL export, so floor/border don't need to stay separate here.
        var allMeshes = new List<Mesh> { floor, border };
        allMeshes.AddRange(textMeshes.Select(t => t.Mesh));
        allMeshes.AddRange(svgMeshes.Select(s => s.Mesh));
        return _meshComposer.MergeMeshes(allMeshes);
    }

    public (Mesh Floor, Mesh Border, IReadOnlyList<PositionedTextMesh> TextMeshes, IReadOnlyList<PositionedSvgMesh> SvgMeshes) GenerateModelParts(Model model)
    {
        var (floor, border) = _shapeGenerator.GenerateParts(model);

        var rawTextMeshes = _textMeshConverter.ConvertMultilineText(model.TextLines);
        var positionedTextMeshes = PositionItems(model.TextLines, rawTextMeshes, model, _textPositioner);
        var textResults = model.TextLines
            .Zip(positionedTextMeshes, (line, mesh) => new PositionedTextMesh(line, mesh))
            .ToList();

        var rawSvgMeshes = _svgMeshConverter.ConvertMultipleSvgInserts(model.SvgInserts);
        var positionedSvgMeshes = PositionItems(model.SvgInserts, rawSvgMeshes, model, _textPositioner);
        var svgResults = model.SvgInserts
            .Zip(positionedSvgMeshes, (insert, mesh) => new PositionedSvgMesh(insert, mesh))
            .ToList();

        return (floor, border, textResults, svgResults);
    }

    /// <summary>Positions a set of items (text lines or SVG inserts) that each carry a position
    /// mode: all AutoCenter-mode items are batched through one ITextPositioner.AutoCenter call so
    /// they stack/center together as a group, while Manual/Relative items are positioned
    /// individually. Returns each item's raw mesh with its transform applied.</summary>
    private static IReadOnlyList<Mesh> PositionItems<T>(
        IReadOnlyList<T> items, IReadOnlyList<Mesh> rawMeshes, Model model, ITextPositioner positioner)
        where T : IPositionable
    {
        var autoCenterMeshes = items
            .Select((item, index) => (item, index))
            .Where(x => x.item.PositionMode == TextPositionMode.AutoCenter)
            .Select(x => rawMeshes[x.index])
            .ToList();

        var autoCenterTransforms = autoCenterMeshes.Count > 0
            ? positioner.AutoCenter(autoCenterMeshes, model)
            : Array.Empty<Transform>();

        var result = new Mesh[items.Count];
        int autoCursor = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var transform = item.PositionMode switch
            {
                TextPositionMode.AutoCenter => autoCenterTransforms[autoCursor++],
                TextPositionMode.Manual => positioner.ApplyManualOffset(item),
                TextPositionMode.Relative => positioner.CalculateRelativeCoords(item, model),
                _ => throw new ArgumentOutOfRangeException(nameof(items), item.PositionMode, "Unknown position mode.")
            };
            result[i] = rawMeshes[i].Transformed(transform.Position, transform.RotationZ);
        }
        return result;
    }

    public void ExportSTL(Mesh mesh, string filePath) => STLExporter.ExportToSTL(mesh, filePath);
}
