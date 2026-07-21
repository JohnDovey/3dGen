using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services;

/// <summary>Wires shape generation, text/SVG/image/border-text conversion, positioning, and
/// composition into the full parameters → mesh workflow.</summary>
public class ModelOrchestrator : IModelOrchestrator
{
    private readonly IShapeGenerator _shapeGenerator;
    private readonly ITextMeshConverter _textMeshConverter;
    private readonly ISvgMeshConverter _svgMeshConverter;
    private readonly IImageMeshConverter _imageMeshConverter;
    private readonly IBorderTextMeshConverter _borderTextMeshConverter;
    private readonly ITextPositioner _textPositioner;
    private readonly IMeshComposer _meshComposer;

    public ModelOrchestrator(
        IShapeGenerator shapeGenerator,
        ITextMeshConverter textMeshConverter,
        ISvgMeshConverter svgMeshConverter,
        IImageMeshConverter imageMeshConverter,
        IBorderTextMeshConverter borderTextMeshConverter,
        ITextPositioner textPositioner,
        IMeshComposer meshComposer)
    {
        _shapeGenerator = shapeGenerator;
        _textMeshConverter = textMeshConverter;
        _svgMeshConverter = svgMeshConverter;
        _imageMeshConverter = imageMeshConverter;
        _borderTextMeshConverter = borderTextMeshConverter;
        _textPositioner = textPositioner;
        _meshComposer = meshComposer;
    }

    public Mesh GenerateModel(Model model)
    {
        var (floor, border, textMeshes, svgMeshes, imageMeshes, borderTextMeshes) = GenerateModelParts(model);

        var allMeshes = new List<Mesh> { floor, border };
        allMeshes.AddRange(textMeshes.Select(t => t.Mesh));
        allMeshes.AddRange(svgMeshes.Select(s => s.Mesh));
        allMeshes.AddRange(imageMeshes.Select(i => i.Mesh));
        allMeshes.AddRange(borderTextMeshes.Where(b => b.Mesh.Vertices.Count > 0).Select(b => b.Mesh));
        return _meshComposer.MergeMeshes(allMeshes);
    }

    public (Mesh Floor, Mesh Border, IReadOnlyList<PositionedTextMesh> TextMeshes, IReadOnlyList<PositionedSvgMesh> SvgMeshes, IReadOnlyList<PositionedImageMesh> ImageMeshes, IReadOnlyList<RenderedBorderTextMesh> BorderTextMeshes) GenerateModelParts(Model model)
    {
        var (outer, inner) = _shapeGenerator.GenerateBorderOutline(model);
        var (midline, midlineLength) = BorderTextMeshConverter.BuildMidline(outer, inner);

        var engraved = model.BorderTextLines.Where(l => l.Mode == BorderTextMode.Engraved && !string.IsNullOrEmpty(l.Content)).ToList();
        var cutouts = new List<IReadOnlyList<Vector2>>();
        float cutoutDepth = 0;
        if (engraved.Count > 0)
        {
            cutoutDepth = engraved.Max(l => l.Height);
            foreach (var line in engraved)
            {
                cutouts.AddRange(_borderTextMeshConverter.LayoutGlyphContours(line, midline, midlineLength));
            }
        }

        var (floor, border) = _shapeGenerator.GenerateParts(model, cutouts, cutoutDepth);
        float borderTopZ = model.ShapeThickness + model.BorderHeight;

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

        var rawImageMeshes = _imageMeshConverter.ConvertMultipleImageInserts(model.ImageInserts);
        var positionedImageMeshes = PositionItems(model.ImageInserts, rawImageMeshes, model, _textPositioner);
        var imageResults = model.ImageInserts
            .Zip(positionedImageMeshes, (insert, mesh) => new PositionedImageMesh(insert, mesh))
            .ToList();

        var borderTextResults = new List<RenderedBorderTextMesh>();
        foreach (var line in model.BorderTextLines)
        {
            if (line.Mode == BorderTextMode.Engraved)
            {
                borderTextResults.Add(new RenderedBorderTextMesh(line, new Mesh()));
            }
            else
            {
                var mesh = _borderTextMeshConverter.ConvertBorderTextToMesh(line, outer, inner, borderTopZ);
                borderTextResults.Add(new RenderedBorderTextMesh(line, mesh));
            }
        }

        return (floor, border, textResults, svgResults, imageResults, borderTextResults);
    }

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
