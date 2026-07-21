# Project Export/Import Bundles + Border-Following Text

## Context

Two independent feature requests, planned together:

**A. Export/Import a project as a portable bundle.** Today a model only lives in
the local SQLite DB (`%LocalAppData%\ModelGenerator\models.sqlite`), and its SVG/
image inserts are already fully self-contained on the `Model` object
(`SvgInsert.SvgContent`, `ImageInsert.ImageData`, `Model.CustomShapeSvgContent`
store full content, not just library references — confirmed in
`SvgMeshConverter`/`ImageMeshConverter`/`ShapeGenerator` doc comments). There's no
way to hand a project to someone else or move it between machines. Export should
bundle the model's settings *and* its graphics into one portable zip; Import
should reverse that, adding the graphics to the local SVG/Image libraries
(without creating duplicates) and loading the full project back into the editor.

**B. Text on the raised border, following its curve.** Every insert today
(`TextLine`/`SvgInsert`/`ImageInsert`) sits on the flat top surface at a rigid
X/Y/Z position. There's no way to put text on the border itself, wrapping around
it like lettering on a coin's rim, in either raised (embossed) or truly recessed
(engraved) form.

## Assumptions to confirm as work proceeds

1. **Border text uses natural size, shrinking only if it would overlap itself**
   going all the way around the loop (confirmed by user over "always wrap full
   circumference" and "user picks an arc span") — a `FontSize` field behaves like
   normal text; if the string's rendered width at that size would exceed the
   border's total circumference, it's scaled down just enough to fit exactly
   once around. Short text leaves a visible gap. An `AnchorAngleDegrees` field
   (default 90°, i.e. the top of the shape) controls where the text is centered.
2. **Engraved border text is a true geometric recess** (confirmed by user) — a
   real cut into the border's top surface (its own floor + walls, with a
   matching hole in the border's own top-cap tessellation), not a visual-only
   trick. This is the one piece with no existing precedent in the codebase (no
   CSG/boolean-subtraction capability exists or is being added) — implemented
   via tessellating the border's top cap *with the glyph contours as holes*
   (LibTessDotNet nonzero winding, the same mechanism `ExtrudeContours` already
   uses for glyph counters), not by subtracting from an already-built mesh.
3. **Border text is not draggable in the viewport** — its only "position" control
   is `AnchorAngleDegrees`, edited as a numeric field like any other setting, not
   a viewport drag gesture. (Possible future enhancement, not v1.)
4. **If multiple engraved border text lines exist on the same model, they share
   one recess depth** (the maximum of their individual `Height` values) — the
   border's top-cap tessellation can only be cut once per generation pass;
   supporting independently-deep simultaneous engravings is out of scope.
5. **Import loads into the editor only** (confirmed by user) — same as File >
   Open: populates the current editing session (after the usual unsaved-changes
   prompt), the user decides whether/when to Save. `model.Id` is reset to 0 (a
   new, unsaved model) even if a same-named model already exists locally.
6. **Bundle dedup is content-based, not name-based** — importing a bundle whose
   graphics are byte-for-byte/text-for-text identical to something already in
   the local library reuses the existing library file instead of creating a
   near-duplicate; only genuinely new content gets a new library entry (which
   still goes through the existing name-collision suffixing if needed).
7. **`ProjectBundleService` lives in `ModelGenerator.Core`**, not tied to
   WinForms — same reasoning as the library services: keeps it reusable if
   `ModelGenerator.Host` (the Mac RPC bridge) wants project bundling later. This
   pass does **not** add a Host RPC method for it — out of scope, no Mac UI asked
   for it yet.
8. Bundle file extension: `.mgproj` (still a plain zip under the hood, same idea
   as `.docx`) — easy to bikeshed, low cost to rename later if the user prefers
   something else (e.g. plain `.zip`).
9. Two new top-level features — bump the minor version once on completion, per
   the established convention (both ship together, matching how prior
   multi-part sessions were bumped once at the end).

## A. Project export/import bundle

### A1. Bundle format

A `.mgproj` file is a zip:
```
manifest.json                 (BundleManifest — see below)
assets/svg/{index}_{name}.svg          (one per SvgInsert; also for CustomShapeSvgContent)
assets/images/{index}_{name}.{ext}     (one per ImageInsert)
```
`manifest.json` is a **dedicated DTO**, not a raw dump of `Model` — the point of
separate asset files is to avoid ALSO base64-embedding the same bytes inline in
JSON. New `src/ModelGenerator.Core/Services/ProjectBundle/BundleManifest.cs`:
```csharp
public record BundleManifest(
    int FormatVersion,             // 1
    string? AppVersion,
    string ModelName,
    ShapeType ShapeType, float ShapeSize, float ShapeHeight, float ShapeThickness,
    float BorderThickness, float BorderHeight, int BaseColorArgb, int BorderColorArgb,
    string? CustomShapeAssetPath, string? CustomShapeSourceFileName,
    List<BundleTextLine> TextLines,
    List<BundleSvgInsert> SvgInserts,
    List<BundleImageInsert> ImageInserts,
    List<BundleBorderTextLine> BorderTextLines);   // present once Feature B ships; empty list otherwise

public record BundleTextLine(int LineNumber, string Content, string FontName, float FontSize,
    float TextHeight, TextPositionMode PositionMode, float PositionX, float PositionY,
    float PositionZ, float RotationZ, int ColorArgb);

public record BundleSvgInsert(int LineNumber, string? SourceFileName, string AssetPath,
    float Scale, float EmbossHeight, TextPositionMode PositionMode, float PositionX,
    float PositionY, float PositionZ, float RotationZ, int ColorArgb);

public record BundleImageInsert(int LineNumber, string? SourceFileName, string AssetPath,
    float Scale, float ReliefHeight, ImageDetail Detail, bool Invert, TextPositionMode PositionMode,
    float PositionX, float PositionY, float PositionZ, float RotationZ, int ColorArgb);
```
Serialize with plain `System.Text.Json` — reuse `ModelGenerator.Host.Protocol.HostProtocol.JsonOptions`'s
settings (camelCase, ignore-null, string enums) as a precedent, but Core shouldn't
depend on Host, so define an equivalent small `JsonSerializerOptions` locally
(or move that options-builder into Core and have Host reuse it — prefer this:
add `ModelGenerator.Core.Utilities.CoreJsonOptions` and have Host's
`HostProtocol.JsonOptions` delegate to it, so there's one definition, not two).
Uses `System.IO.Compression.ZipArchive` (BCL, no new package, portable).

### A2. `IProjectBundleService` / `ProjectBundleService`

New `Services/ProjectBundle/IProjectBundleService.cs` / `ProjectBundleService.cs`,
constructed with `ISvgLibraryService, IImageLibraryService` (same pattern as
`ModelOrchestrator`'s hand-wired dependencies):
```csharp
public interface IProjectBundleService
{
    void ExportBundle(Model model, string zipFilePath, string? appVersion = null);
    Model ImportBundle(string zipFilePath);
}
```
- `ExportBundle`: builds the `BundleManifest` from `model`, writing each
  `SvgInsert.SvgContent`/`ImageInsert.ImageData`/`CustomShapeSvgContent` out as
  its own zip entry under `assets/...` (`ZipArchive.CreateEntry` + write), then
  writes `manifest.json`.
- `ImportBundle`: reads+deserializes `manifest.json`; for each asset, computes a
  content hash (`SHA256`) and compares against every existing library file's
  hash (read via `ISvgLibraryService.ListSvgFiles()`/`ReadSvgContent` or
  `IImageLibraryService.ListImageFiles()`/`ReadImageBytes` — libraries are small,
  hashing on the fly per import is cheap, no persistent index needed) — reuses
  the existing file if a match is found, otherwise imports as new. Requires a
  new **`ImportContent`** method on both library service interfaces (existing
  `ImportFile(sourceFilePath)` needs an on-disk file; bundle assets come from an
  in-memory zip entry stream) — add to `ISvgLibraryService`/`SvgLibraryService`
  and `IImageLibraryService`/`ImageLibraryService`, mirroring `ImportFile`'s
  name-collision-suffix loop but writing the given content/bytes directly
  instead of `File.Copy`. Reassembles a fully self-contained `Model` (content
  embedded, exactly like today) with `Id = 0`.
- New `tests/ModelGenerator.Tests/ProjectBundleServiceTests.cs`: round-trip a
  model with text/svg/image/custom-shape through export→import (temp library
  dirs) and assert every field matches; assert re-importing the *same* bundle
  twice does not duplicate library files (dedup-by-content); assert an
  unsupported `FormatVersion` throws a clear exception.
- New `tests/ModelGenerator.Tests/{Svg,Image}LibraryServiceTests.cs` additions
  for `ImportContent`.

### A3. Data layer — no schema change needed

Bundles don't touch SQLite at all (export reads an in-memory `Model`; import
populates the editor, not the DB) — no `DatabaseInitializer`/repository changes
for Feature A itself (Feature B's `BorderTextLines` table is separate, below).

### A4. UI wiring (`MainForm.cs`)

- File menu gains **Export Project...** (after Export STL) and **Import
  Project...** (after Open...).
- `ExportProjectAsync()`: `SaveFileDialog` (filter `3D Model Project
  (*.mgproj)|*.mgproj`, suggested name from `_currentModelName`) →
  `_projectBundleService.ExportBundle(BuildModelFromControls(), path, AppVersion)`
  → status label confirmation; broad try/catch → `MessageBox` on failure
  (matches `SaveModelAsync`'s error-handling style).
- `ImportProjectAsync()`: `ConfirmDiscardUnsavedChangesAsync()` guard (same as
  New/Open) → `OpenFileDialog` (`*.mgproj`) →
  `_projectBundleService.ImportBundle(path)` → `_currentModelId = null`,
  `_currentModelName = model.Name` → same `RunGuardedFromUndoTracking` load
  pattern `OpenModelAsync` already uses (`LoadFrom`/`LoadLines`/`LoadInserts`) →
  clear undo stack, reset dirty/title, regenerate preview.
- `Program.cs`: construct `new ProjectBundleService(svgLibraryService,
  imageLibraryService)`, thread into `MainForm`'s constructor (new parameter).

## B. Border-following text (embossed or engraved)

### B1. `BorderTextLine` model

New `src/ModelGenerator.Core/Models/BorderTextLine.cs`:
```csharp
public enum BorderTextMode { Embossed, Engraved }

public class BorderTextLine
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public float FontSize { get; set; } = 8f;
    public float Height { get; set; } = 1.5f;          // emboss protrusion OR engrave depth, mm
    public BorderTextMode Mode { get; set; } = BorderTextMode.Embossed;
    public float AnchorAngleDegrees { get; set; } = 90f; // center of the text span; 0=+X axis, CCW; 90=top
    public int ColorArgb { get; set; } = ArgbColors.DarkOrange;
}
```
Deliberately **not** `IPositionable` — its placement model (anchored to a closed
curve, not a free X/Y/Z) doesn't fit `PositionItems<T>`/`ApplyManualOffset`, and
the converter builds already-positioned world-space geometry directly rather
than a local-origin mesh needing a rigid-body `Transform`.

`Model.cs`: add `public List<BorderTextLine> BorderTextLines { get; set; } = new();`.

### B2. Expose the border's 2D outline (`ShapeGenerator`)

Every `Build*Parts` (Circle/Rectangle/Triangle/Shield/CustomSvg) already computes
matching `outer`/`inner` `List<Vector2>` polygons before calling
`MeshMath.ExtrudeSolid`/`ExtrudeRing` — extract the common tail into one shared
helper so cutout support (B4) only needs to exist in one place, and so the
outline can be exposed publicly for border-text layout:
```csharp
// IShapeGenerator additions:
(IReadOnlyList<Vector2> Outer, IReadOnlyList<Vector2> Inner) GenerateBorderOutline(Model model);
(Mesh Floor, Mesh Border) GenerateParts(Model model, IReadOnlyList<IReadOnlyList<Vector2>> borderTopCutouts, float cutoutDepth);
// existing GenerateParts(model) becomes a thin wrapper: GenerateParts(model, [], 0)
```
`ShapeGenerator`: each `Build*Parts` keeps computing its own `outer`/`inner`, then
delegates to one new private `BuildFloorAndBorder(outer, inner, thickness,
borderHeight, cutouts, cutoutDepth)` that does
`ExtrudeSolid(outer, 0, thickness)` for the floor and picks
`MeshMath.ExtrudeRing` vs `MeshMath.ExtrudeRingWithTopCutouts` (B3) based on
whether `cutouts` is empty — mechanical, behavior-preserving refactor for the
existing no-cutout path (covered by the existing `GenerateParts_*` tests, which
must stay green unmodified).
`GenerateBorderOutline(model)` reuses the same per-shape outer/inner computation
(factor each `Build*Parts`'s outline math into a small private `Get*Outline`
paired with the shape) without needing to run the extrusion at all.

### B3. `MeshMath.ExtrudeRingWithTopCutouts` — the new geometry primitive

```csharp
public static Mesh ExtrudeRingWithTopCutouts(
    IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner,
    IReadOnlyList<IReadOnlyList<Vector2>> cutoutContours,
    float zBottom, float zTop, float cutoutDepth);
```
- **Top cap**: one `LibTessDotNet.Tess` pass over `[outer, inner, ...cutoutContours]`
  (`WindingRule.NonZero`, same as `ExtrudeContours`) — `inner` and each cutout
  wound oppositely from `outer` so they're holes; emits triangles at `zTop`
  genuinely excluding the cutout areas (real hole, not a visual trick).
- **Bottom cap**: unchanged from `ExtrudeRing` (`outer`+`inner` only, no
  cutouts — the recess doesn't go all the way through, the underside stays solid).
- **Outer/inner walls**: unchanged from `ExtrudeRing`.
- **Cutout walls + floor**: per cutout contour, a wall from `zTop` down to
  `zTop - cutoutDepth` facing *into* the cavity (`AddSideWall(..., outward:
  false)`), plus a flat floor cap at `zTop - cutoutDepth` tessellated via
  LibTessDotNet (glyph shapes like "S"/"B" aren't guaranteed star-shaped from
  centroid, so reuse the tessellation approach, not the centroid-fan one).
- New `tests/ModelGenerator.Tests/MeshMathTests.cs` cases: watertight + positive
  volume with one rectangular cutout; volume is strictly less than the same ring
  with no cutouts (proves material was actually removed, not just painted a
  different color); a cutout resembling a glyph with its own hole (e.g. "O") to
  confirm nested nonzero-winding still works when combined with the ring's own
  hole.

### B4. `IBorderTextMeshConverter` / `BorderTextMeshConverter`

New `Services/IBorderTextMeshConverter.cs` / `BorderTextMeshConverter.cs`. Needs
per-glyph outlines + advance widths, which nothing in the codebase exposes today
(`TextMeshConverter`/`SkiaPathContours` only do whole-string-at-once). New shared
`Core/Utilities/SkiaFontResolver.cs` (tiny, extracted from
`TextMeshConverter`'s private `ResolveTypeface` so both converters share one
fallback-to-default implementation instead of duplicating it).
```csharp
public interface IBorderTextMeshConverter
{
    Mesh ConvertBorderTextToMesh(BorderTextLine borderText, IReadOnlyList<Vector2> borderOuter,
        IReadOnlyList<Vector2> borderInner, float borderTopZ);
    IReadOnlyList<Mesh> ConvertMultipleBorderTextLines(IReadOnlyList<BorderTextLine> borderTextLines,
        IReadOnlyList<Vector2> borderOuter, IReadOnlyList<Vector2> borderInner, float borderTopZ);

    // Exposed separately so ModelOrchestrator can collect engraved lines' glyph contours
    // BEFORE generating the border (needed to cut the holes — see B5), without generating
    // full embossed-style meshes for them.
    IReadOnlyList<IReadOnlyList<Vector2>> LayoutGlyphContours(BorderTextLine borderText,
        IReadOnlyList<Vector2> borderMidline, float totalMidlineLength);
}
```
`ConvertBorderTextToMesh` (embossed): midline = `outer.Zip(inner, (o,i) => (o+i)/2)`;
arc-length table over the midline (cumulative segment lengths, closed loop);
`LayoutGlyphContours` — per glyph: `SKFont.GetGlyphs`/`GetGlyphPath`/advance
widths (verify exact SkiaSharp API at implementation time, same "spike it first"
caution this project used for `SvgRenderer`/`Svg.Skia` originally) → contours via
existing `SkiaPathContours.ExtractContours` → natural total width at `FontSize`;
if it exceeds `totalMidlineLength`, scale font down so it exactly fits (**never
scales up** — "natural size, shrink only if needed"); center the (possibly
shrunk) span on `AnchorAngleDegrees`; for each glyph, sample the midline at its
cumulative-advance arc-length position for `(Position, Tangent)`, rotate the
glyph's local contours to align with the tangent (+ outward normal as "up") and
translate to `Position`. `ConvertBorderTextToMesh` calls `LayoutGlyphContours`
then `MeshMath.ExtrudeContours(allGlyphContours, borderTopZ, borderTopZ +
borderText.Height)` for `Mode == Embossed`; for `Mode == Engraved` it still
returns *some* mesh for API symmetry (e.g. `new Mesh()`, since engraved
material is actually cut from the border itself, not added — see B5) but real
callers branch on `Mode` before calling it.
- New `tests/ModelGenerator.Tests/BorderTextMeshConverterTests.cs`: short
  embossed string on a circle's border → watertight, positive volume; a string
  too long for the circumference shrinks (assert computed span ≤
  `totalMidlineLength`); `AnchorAngleDegrees` changes the mesh's centroid angle;
  a letter with a counter (e.g. "O") still tessellates correctly on the curve.

### B5. Orchestrator wiring (`ModelOrchestrator`)

`IModelOrchestrator.cs`: add
`public readonly record struct RenderedBorderTextMesh(BorderTextLine Line, Mesh Mesh);`
and grow `GenerateModelParts`'s tuple with
`IReadOnlyList<RenderedBorderTextMesh> BorderTextMeshes`.

`ModelOrchestrator.GenerateModelParts`:
1. `var (outer, inner) = _shapeGenerator.GenerateBorderOutline(model);`
2. Split `model.BorderTextLines` into embossed vs. engraved.
3. For engraved lines: compute their glyph contours via
   `_borderTextMeshConverter.LayoutGlyphContours(...)` for each, concatenate,
   and take `cutoutDepth = engravedLines.Max(l => l.Height)` (assumption 4).
4. `var (floor, border) = _shapeGenerator.GenerateParts(model, engravedCutoutContours, cutoutDepth);`
   (empty list/0 depth when there are no engraved lines — identical output to
   today).
5. Embossed border text: `_borderTextMeshConverter.ConvertMultipleBorderTextLines(...)`
   for `Mode == Embossed` lines only, each becoming its own `RenderedBorderTextMesh`
   (own color, per `BorderTextLine.ColorArgb`).
6. Engraved lines still get a `RenderedBorderTextMesh` entry too, but with an
   **empty mesh** (`new Mesh()`) — their "geometry" is the hole now baked into
   `border`, not a separate positive mesh; kept in the list purely so the UI
   layer has one item per `BorderTextLine` to pair with its editor row (matches
   the `Positioned*Mesh` pattern's "one entry per model item" contract).
`GenerateModel` (merged single mesh) appends all non-empty border-text meshes.
Update `ModelOrchestratorTests.cs` for the 6-tuple + a mixed embossed/engraved test.

### B6. Data layer

New `BorderTextLines` table in `DatabaseInitializer.cs` (brand-new table, no
migration needed, same as when `SvgInserts`/`ImageInserts` were added):
```sql
CREATE TABLE IF NOT EXISTS BorderTextLines (
    BorderTextLineId INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelId INTEGER NOT NULL,
    LineNumber INTEGER NOT NULL,
    Content TEXT NOT NULL,
    FontName TEXT NOT NULL,
    FontSize REAL NOT NULL DEFAULT 8,
    Height REAL NOT NULL DEFAULT 1.5,
    Mode INTEGER NOT NULL DEFAULT 0,
    AnchorAngleDegrees REAL NOT NULL DEFAULT 90,
    ColorArgb INTEGER NOT NULL DEFAULT -29696,
    FOREIGN KEY (ModelId) REFERENCES Models(ModelId) ON DELETE CASCADE
);
```
`SqliteModelRepository.cs`: `LoadBorderTextLinesAsync` (mirrors
`LoadTextLinesAsync`), delete-and-reinsert in `SaveModelAsync` (mirrors the
existing three), populate in `GetModelByIdAsync`/`ListModelsAsync`. Extend
`DatabaseInitializerTests`/`SqliteModelRepositoryTests` to match.

### B7. UI layer

- New `Controls/BorderTextLineEditorControl.cs` (mirrors `TextLineEditorControl.cs`):
  Content, Font, FontSize, Height, Mode combo (Embossed/Engraved), Anchor angle
  (°), Color button, Remove — no Position-mode cluster (assumption 3).
- New `Controls/BorderTextLinesPanel.cs` (mirrors `TextLinesPanel.cs` exactly,
  including the manual-row-layout workaround for the reverse-Dock-add-order bug):
  `+ Add Border Text` button, `Lines`, `LoadLines`, `Clear`, `LinesChanged` event.
  (No `UpdateLinePosition` — nothing to update from a drag.)
- `HelixViewportHost.cs`: `ShowModel(...)` gains an `IReadOnlyList<ColoredMesh>
  borderTextMeshes` parameter (non-interactive, individually colored — same
  rendering as floor/border, just N of them instead of one); no hit-testing/drag
  changes needed (assumption 3).
- `MainForm.cs`: new `_borderTextPanel` field wired into the left panel
  (reverse-add-order convention, section after Image inserts);
  `BuildModelFromControls()` adds `model.BorderTextLines =
  _borderTextPanel.Lines;`; `NewModel`/`OpenModelAsync`/`RestoreModelIntoControls`
  clear/load it like the other three; `OnEditableStateChanged` wiring for its
  `LinesChanged` event (same funnel as the other three panels — dirty
  tracking/undo/redo "just work" for border text with zero extra code, since
  it's the same event-routing pattern).
- `Program.cs`: no new service to wire (border text uses the existing
  `TextMeshConverter`'s font-resolution sibling logic + `MeshMath`, no separate
  library/service dependency) beyond constructing `new
  BorderTextMeshConverter()` and threading it into `ModelOrchestrator`'s
  constructor (which gains one more parameter).

## Docs

- `docs/PLAN.md`: new phase entry covering both features (what shipped, what was
  verified live).
- `docs/HOW_TO_USE.md`: new "Export and import a project" section (File menu,
  what's bundled, how dedup works) and new "Text on the border" section
  (Embossed vs Engraved, natural-size-shrinks-to-fit behavior, anchor angle)
  with live-captured screenshots.
- `README.md`: one-paragraph mention of both new capabilities.

## Verification

1. Unit tests at every layer above (`dotnet test`) — bundle round-trip +
   dedup-on-reimport, `ExtrudeRingWithTopCutouts` watertightness/volume-loss,
   `BorderTextMeshConverter` shrink-to-fit and anchor-angle behavior, repository/
   DB migration coverage for `BorderTextLines`.
2. **Live verification**: export a model with a text line, an SVG insert, an
   image insert, and a custom-shape outline to a `.mgproj`, confirm the zip's
   contents look right (manifest + asset files), then New → Import it back and
   confirm every field/graphic matches, including that re-importing the same
   file again doesn't duplicate library entries (check the library folder file
   count before/after). Add a short embossed border-text line on a Circle,
   confirm it renders wrapping the rim; add a long one and confirm it visibly
   shrinks to fit; switch a line to Engraved and confirm the viewport shows a
   genuine recess (not just a color change) — rotate the camera underneath if
   needed to sanity-check there's no floating/inverted geometry; export STL and
   confirm the triangle count is sane for both modes.
3. Full `dotnet build` + `dotnet test` clean (0 warnings), matching the
   project's established bar.

## Critical files

- `src/ModelGenerator.Core/Services/ProjectBundle/BundleManifest.cs` (new)
- `src/ModelGenerator.Core/Services/ProjectBundle/ProjectBundleService.cs` (new)
- `src/ModelGenerator.Core/Services/{Svg,Image}LibraryService.cs` (`ImportContent` addition)
- `src/ModelGenerator.Core/Models/BorderTextLine.cs` (new)
- `src/ModelGenerator.Core/Utilities/MeshMath.cs` (`ExtrudeRingWithTopCutouts`)
- `src/ModelGenerator.Core/Utilities/SkiaFontResolver.cs` (new, extracted)
- `src/ModelGenerator.Core/Services/ShapeGenerator.cs` (`GenerateBorderOutline`, cutout-aware `GenerateParts` overload)
- `src/ModelGenerator.Core/Services/BorderTextMeshConverter.cs` (new)
- `src/ModelGenerator.Core/Services/ModelOrchestrator.cs`
- `src/ModelGenerator.Data/Database/DatabaseInitializer.cs`
- `src/ModelGenerator.Data/Repository/SqliteModelRepository.cs`
- `src/ModelGenerator.UI/Controls/BorderTextLineEditorControl.cs` (new)
- `src/ModelGenerator.UI/Controls/BorderTextLinesPanel.cs` (new)
- `src/ModelGenerator.UI/Controls/HelixViewportHost.cs`
- `src/ModelGenerator.UI/MainForm.cs`
- `src/ModelGenerator.UI/Program.cs`
