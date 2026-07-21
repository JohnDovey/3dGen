# 3D Model Generator — Architecture Plan

## Status

- **Phase 1 (done):** Solution scaffolded (.NET 10). Core models, `ShapeGenerator`
  (Circle + Rectangle), `STLExporter`, SQLite schema + `SqliteModelRepository`. 10 tests passing.
- **Phase 2 (done):** `TextMeshConverter` (GDI+ glyph outlines + LibTessDotNet tessellation,
  including glyph counters/holes), `TextPositioner` (AutoCenter/Manual/Relative),
  `MeshComposer`, `ModelOrchestrator` tying it all together. 26 tests passing total.
- **Phase 3 (done):** WinForms `MainForm` with a Helix Toolkit viewport (hosted via WPF/
  ElementHost, explicit top-down camera), `ShapeSelectorControl`, `TextLinesPanel`/
  `TextLineEditorControl` (multi-line, per-line font/size/position mode), live preview that
  regenerates on every change, and an Export STL dialog. Verified by actually launching the
  app and driving it via UI Automation (not just compiling) — including a real WinForms
  layout bug (FlowLayoutPanel doesn't stretch Dock=Top children) that only showed up at
  runtime.
- **Phase 4 (done):** File menu (New/Open/Save/Save As/Export STL/Exit) wired to the existing
  `SqliteModelRepository` — `OpenModelDialog` (list/select/delete saved models) and
  `TextInputDialog` (name prompt) added; `ShapeSelectorControl`/`TextLinesPanel` gained
  `LoadFrom`/`LoadLines` to round-trip a `Model` back into the UI. Font combo box is now
  owner-drawn (each entry rendered in its own typeface). `RegeneratePreview`'s catch broadened
  to any `Exception` (it runs on every keystroke — must never crash the app). Verified live:
  New resets to defaults, Save persists to SQLite and updates the title, Open reloads a saved
  model's shape + text lines correctly.

- **Phase 5 (done):** Triangle and Shield shape generation. Triangle uses an exact
  incenter-based inset (constant-width border, same rigor as Circle/Rectangle — the incenter
  and centroid coincide for a regular polygon). Shield uses a 7-point heraldic silhouette
  (flat top, shoulder flare, tapering to a point) with a radial-from-centroid inset border —
  an approximation rather than a true constant-width offset, since the shape is irregular, but
  visually even and good enough for a decorative emboss. All four shapes from the original
  spec are now implemented. 28 tests passing total.

- **Phase 6 (done):** In-app Help viewer (Help → How to Use) rendering `docs/HOW_TO_USE.md` as
  HTML via Markdig + a WinForms `WebBrowser` control — the .md/images are copied into the build
  output (`Help\`) so `docs/` stays the single source of truth. Drag-and-drop text positioning
  in the 3D viewport: each text line now renders as its own pickable `ModelVisual3D` (added
  `IModelOrchestrator.GenerateModelParts` to get the base shape and positioned text meshes
  separately instead of pre-merged); dragging a line hit-tests it via `VisualTreeHelper.HitTest`,
  unprojects the mouse onto the shape's top-surface plane via `Viewport3DHelper.UnProject`, and
  switches that line to Manual mode with the dropped X/Y/Z. Found and fixed a real, previously-
  silent WinForms bug in the process: `Dock=Fill` must be added to `Controls` **first** (not
  last) — controls dock in *reverse* of add order, so a later-added Fill was silently claiming
  the full client rect and overlapping the Left-docked panel. It never caused a visible glitch
  (each control's own painting still looked right), but it threw off the viewport's hit-testing
  coordinates, which is what surfaced it. 29 tests passing total.

- **Phase 7 (done):** Fixed a real crash bug found along the way — `Mesh.AddTriangle` normalized
  a degenerate (zero-area) triangle's cross product unconditionally, producing NaN/Infinity
  vertex normals that then failed JSON serialization on Save (`Could not save the model: .NET
  number values such as positive and negative infinity cannot be written as valid JSON`); now
  guarded by a `LengthSquared() > 1e-12f` check. Added a library of SVG graphics the user can
  browse (with thumbnails), import from disk, and insert into the model — positioned/adjusted
  exactly like a text line (scale, emboss height, position mode, drag-to-reposition), built on
  the `Svg` NuGet package for geometry extraction and the *same* contour → tessellate → extrude
  pipeline already proven for text glyphs (including holes/cutouts, e.g. a ring shape). Every
  text line and every SVG insert now has its own color picker (`ColorArgb`, stored as a plain
  ARGB int to keep Core UI-toolkit-agnostic); the shape's floor and border are independently
  colorable too (`ShapeGenerator.GenerateParts` splits what used to be one merged mesh).
  `HelixViewportHost` was redesigned around `ColoredMesh` (floor/border, not draggable) and
  `DraggableMesh` (text/SVG items, individually pickable and colored) instead of parallel
  parameter lists. SQLite schema migrated via `PRAGMA table_info` + `ALTER TABLE ADD COLUMN`
  (new columns on `Models`/`TextLines` plus a new `SvgInserts` table), with a test proving
  existing data survives the migration. Verified live: the save-bug repro now succeeds; base/
  border colors and per-item colors all render independently in the viewport; the SVG library
  dialog renders correct thumbnails for both a solid shape and a shape with a hole. Full
  interactive click-through (Insert button, drag-in-viewport) hit a UI Automation limitation in
  this environment — this app's WinForms ListView/Button controls expose as generic panes
  rather than their real control types, so automated invoke/select calls on them are unreliable
  — recommend a quick manual pass. 53 tests passing total.

- **Phase 8 (done):** Implemented the previously-deferred `ShapeType.CustomSvg` — a library SVG
  can now define the base shape's own outline instead of one of the four built-ins.
  `ShapeGenerator.BuildCustomSvgParts` picks the SVG's largest-area contour as the outer
  boundary (via the shared `SvgContourExtractor`, refactored out of `SvgMeshConverter` so both
  share one SVG-tree-walking implementation), normalizes its winding to CCW, and fits/centers it
  so its longer bounding-box dimension equals `Model.ShapeSize` — then reuses the existing
  `RadialInset`/`ExtrudeSolid`/`ExtrudeRing` pipeline exactly like the Shield shape. Only cleanly
  supports a single simple closed path — a multi-path SVG uses "largest contour wins" — same
  documented limitation as Shield's border approximation. `ShapeSelectorControl` gained a
  "Custom shape SVG" thumbnail + **Choose...** button (reuses `SvgLibraryDialog`), enabled only
  when `CustomSvg` is selected. Verified live end-to-end: picked a 5-pointed star from the
  library, watched it become the model's actual base shape (embossed border correctly inset,
  floor/border colors applied, 360 vertices / 120 triangles), saved it, and confirmed via the
  Open dialog that `ShapeType.CustomSvg` and the SVG content round-trip through SQLite correctly
  (also covered by a repository unit test). 60 tests passing total.

- **Phase 9 (done):** Photo/image bas-relief inserts — a JPG or PNG can now be embossed onto the
  shape as a grayscale heightmap relief (`ImageInsert`, `ImageMeshConverter`), the same way a
  medallion turns a portrait into a raised relief, with its own `ImageLibrary`/
  `ImageLibraryService`/`ImageLibraryDialog` mirroring the SVG library exactly. New
  `MeshMath.ExtrudeMaskedHeightfield` extrudes a heightmap grid where each *cell* (not vertex) can
  be independently included or excluded — an included cell gets top/bottom faces from its
  corners' heights, and a wall is added wherever an included cell borders an excluded one (or the
  grid edge). `ImageMeshConverter` box-samples each cell's block of source pixels (via `LockBits`,
  not `GetPixel`, since this runs on every live-preview regeneration) for both luminance (→
  height, via `Detail`'s Low/Medium/High grid resolution and `Invert`) and alpha (→ inclusion,
  thresholded at 50%) — a JPG or fully-opaque PNG has no alpha variation so every cell is
  included, degenerating cleanly to a flat rectangular tile; a PNG with a transparent background
  gets genuinely clipped to its own silhouette instead of being flattened into a rectangle.
  Verified live end-to-end: inserted a radial-gradient JPG and watched it render as a visible
  raised relief (2,304 → 52,992 vertices just from the one insert at Medium detail); confirmed
  `Invert` visibly flips which side is raised; confirmed Low → Medium → High detail smoothly
  increases fidelity (52,992 → 201,984 vertices) with no crash or hang; inserted a
  transparent-background PNG logo and confirmed it rendered as a genuinely circular relief (not
  a rectangle) with jagged edges at the alpha boundary; Save → New → Open round-tripped both
  inserts with identical vertex/triangle counts and settings; Export STL produced a file whose
  byte size exactly matched the expected triangle count (75,956 × 50 + 84-byte header). 74 tests
  passing total.

- **Phase 10 (done):** Undo/redo, unsaved-changes protection, a viewport selection indicator, a
  real drag-positioning bug fix, and library management (delete/search/tags) — a cluster of
  editing-UX and reliability improvements requested together.
  - **Undo/redo**: `UndoManager<T>` (new, `Core/Utilities`) is a plain snapshot stack — push/pop
    full `Model` copies rather than reversible commands, since edits already mutate controls in
    place. `MainForm` routes every panel's change event through one `OnEditableStateChanged`,
    which records the *pre-edit* snapshot only for the first change in a burst; a 500ms idle
    debounce coalesces a whole viewport drag (dozens of mouse-move events) or a whole burst of
    typing into one undo step instead of one per event. New/Open/Undo/Redo all funnel through a
    shared `RunGuardedFromUndoTracking`/`RestoreModelIntoControls` path so loading a model back
    into the panels doesn't itself get recorded as a new edit. Edit menu gained Undo (Ctrl+Z) /
    Redo (Ctrl+Y).
  - **Unsaved-changes protection**: an `_isDirty` flag (set by the same `OnEditableStateChanged`
    hook, cleared on successful Save) drives a trailing `*` in the title bar and a
    save/discard/cancel prompt (`ConfirmDiscardUnsavedChangesAsync`) before New, Open, or closing
    the window would otherwise discard work. `SaveModelAsync` now returns whether the save
    actually succeeded, so an async `OnFormClosing` can cancel the close, prompt, save, and only
    then re-issue `Close()`.
  - **Viewport selection indicator**: `HelixViewportHost` tracks the last-clicked draggable item
    and outlines it with a yellow wireframe box (rebuilt from its mesh's `Rect3D` bounds on every
    `ShowModel` call, so it keeps following the item through a drag); clicking empty space
    deselects.
  - **Drag-positioning bug fix**: `SvgMeshConverter` (and, for the same reason,
    `ImageMeshConverter`) scaled contours without first centering them on their own bounding box
    — an SVG whose canvas/viewBox was bigger than the actual drawing (or a PNG with off-center
    transparent padding) put local `(0,0)`, the point dragging actually moves, well away from the
    artwork's visual center, so a drag appeared to yank the shape out from under the cursor the
    instant it started. Fixed by centering on the bounding box first (`ShapeGenerator`'s
    `CustomSvg` path already did this correctly — the bug was specific to inserts). Regression
    tests assert the resulting mesh's bounding-box center sits at the origin for off-center
    artwork.
  - **Library management**: new shared `LibraryMetadataStore` (JSON sidecar `_metadata.json` per
    library folder) backs `DeleteFile`/`SearchFiles`/`GetKeywords`/`SetKeywords` on both
    `ISvgLibraryService` and `IImageLibraryService`. Both library dialogs gained a live
    search-as-you-type box (thumbnails are rendered once and reused across filtering, not
    re-rendered per keystroke), a **Delete** button (with confirmation), and a **Tags...** button
    for comma-separated keywords, shown as the item's tooltip.
  - Verified live: fresh launch no longer shows a false "modified" title (an early bug caught
    during this pass — the constructor's initial blank text line was tripping the same dirty-flag
    hook a real edit would); library search-as-you-type correctly filtered a 14-file real library
    down to one match instantly, reusing cached thumbnails. 98 tests passing total.

- **Phase 0 / Mac portability foundation (done):** Core no longer depends on
  `System.Drawing.Common` / GDI+. Text, SVG, and image conversion plus library
  thumbnails use **SkiaSharp** + **Svg.Skia**; colors are plain ARGB ints
  (`ArgbColors`); thumbnail APIs return PNG `byte[]` (WinForms decodes via
  `PngThumbnail`). `[SupportedOSPlatform("windows")]` gates removed from Core
  services. Tests retargeted to `net10.0` and pass on macOS (`dotnet test
  tests/ModelGenerator.Tests`). WinForms still builds (thumbnail callers
  updated). This unblocks a SwiftUI Mac host against portable Core/Data.

- **Phase 1 / Host process + JSON-RPC (done):** New `ModelGenerator.Host`
  console app exposes Core via NDJSON RPC (`ping`, `generateParts`, `exportStl`)
  over Unix domain socket or stdio, plus one-shot CLI (`export`,
  `generate-parts`). Wire format documented in `docs/HOST_PROTOCOL.md`. Decision
  locked: **process + JSON-RPC** (not NativeAOT) for the Mac bridge. Integration
  tests cover service methods and session framing.

- **Phase 2 / SwiftUI + SceneKit shell (done):** New `mac/ModelGeneratorMac`
  Swift package (macOS 14+, SwiftUI App). Starts Host automatically (via
  `dotnet run` in dev, or `MODELGENERATOR_HOST` / bundled binary), connects with
  Unix-socket NDJSON client, shows shape inspector (circle/triangle/shield/
  rectangle, size/thickness/border/colors), live SceneKit preview (top-down
  camera + orbit), status bar, and Export STL (⌘⇧E). Text/SVG/image/CustomSvg
  deferred to later phases.

- **Phase 3 / Mac text lines (done):** Multi-line text editor in the Mac
  inspector — add/remove rows; content, system font picker (names drawn in
  their typeface), size, emboss height, color, position mode
  (AutoCenter/Manual/Relative) with X/Y/Z/Rot; live regenerate via Host;
  SceneKit already renders per-line `textMeshes`. SVG/image/CustomSvg still
  later.

- **Phase 4 / Mac persistence + undo (done):** Host gains `listModels` /
  `getModel` / `saveModel` / `deleteModel` against the same SQLite app-data
  DB as WinForms. Mac menus: New/Open/Save/Save As, Undo/Redo (⌘Z/⇧⌘Z),
  dirty `*` title, discard prompts on New/Open/Quit, Open sheet with
  delete. Snapshot undo stack (500ms burst coalescing) on the client.

- **Phase 5 / Mac SVG library + inserts + CustomSvg (done):** Host SVG library
  RPC (list/search/import/delete/tags/thumbnail/read). Mac inspector: SVG
  inserts panel (scale/emboss/color/position), library sheet with search and
  tags, CustomSvg shape type with Choose… from the same library. SceneKit
  already renders `svgMeshes`. Viewport drag-to-reposition deferred with
  image inserts (Phase 6).

- **Phase 6 / Mac image bas-relief + drag/select (done):** Host image library
  RPC (list/import/delete/tags/thumbnail/read). Mac image inserts panel
  (scale, relief, detail L/M/H, invert, position). SceneKit: click select
  (yellow wire box), drag text/SVG/image onto shape top plane → Manual
  X/Y/Z (camera orbit disabled while dragging).

- **Phase 7 / Mac Help, About, packaging (done):** In-app Help (shared
  `docs/HOW_TO_USE.md` via WKWebView), About sheet, `build-release-mac.sh`
  assembling `ModelGenerator.app` with embedded self-contained Host + Help
  resources and a versioned zip. Dual-platform README / launch notes.

- **Project bundles + border text (done, v0.9.0):** `.mgproj` zip export/import
  (`ProjectBundleService` in Core) with content-hash library dedup and
  self-contained assets. Border-following text (`BorderTextLine`) embossed or
  truly engraved along the border midline (shrink-to-fit, anchor angle);
  `MeshMath.ExtrudeRingWithTopCutouts` for recesses; WinForms File menu Import/
  Export Project and Border text panel. See
  `docs/EXPORT_IMPORT_AND_BORDER_TEXT_PLAN.md`.

Remaining ideas (not currently planned as a phase): richer validation feedback in dialogs; a
configurable alpha-inclusion threshold (currently a hardcoded 50%) if the default proves wrong
for some images in practice; the same off-center-artwork drag-jump class of bug likely also
affects `ShapeGenerator.CustomSvg` in one respect — its centering is correct, but this phase
didn't specifically re-verify dragging a `CustomSvg`-shaped model (the base shape itself isn't
draggable, so this is low-risk, but noted for completeness).


Windows desktop app (Visual Studio / Windows Forms) to generate 3D-printable models:
choose a basic shape (circle, triangle, shield, rectangle), enter multi-line text with
per-line font/size, and get an embossed 3D model previewable and exportable as STL.

## Requirements

- Shape flat base, ~10mm thick: circle, triangle, shield, rectangle
- 5mm embossed border around the shape
- Multi-line text, embossed proud of the surface, 5mm high
  - Each line can have its own font and size
  - Positioning: auto-center, manual drag-and-drop, or relative X/Y offset
- Live 3D preview before export
- Export to STL for 3D printing
- Windows-only for v1, but modularized so UI can be swapped for Mac/Linux later
- Visual Studio project, backend stores models in SQLite (both parameters and final mesh)

## 1. Layered Architecture

```
UI LAYER (Windows Forms)
  • Main window, shape selector, text editor
  • 3D viewport (Helix Toolkit viewer)
  • Text positioning controls (auto/drag/relative)
  • Project management (New/Open/Save)
        |
CORE LOGIC LAYER (Business Logic, UI-agnostic)
  • Model factory (shape generation)
  • Text-to-mesh converter
  • Mesh composition (shape + text + border)
  • Text positioning calculator
  • Model state manager
        |
PERSISTENCE LAYER (Data Access)
  • SQLite repository (CRUD for models)
  • Mesh serialization/deserialization
  • Query builders
        |
EXTERNAL SERVICES LAYER
  • Helix Toolkit 3D rendering
  • STL exporter
  • Font system (System.Drawing.Font)
  • SQLite data provider
```

Key principle: UI layer communicates ONLY with Core Logic via interfaces. Core Logic
is UI-agnostic, so Windows Forms can later be swapped for WPF/Mac/Linux without
touching business logic.

## 2. Project Structure (Visual Studio)

```
3DModelGenerator/
├── 3DModelGenerator.UI
│   ├── MainForm.cs
│   ├── Controls/
│   │   ├── ShapeSelector.cs
│   │   ├── TextEditor.cs
│   │   ├── TextPositioningPanel.cs
│   │   └── HelixViewport.cs
│   └── Resources/
│
├── 3DModelGenerator.Core
│   ├── Models/
│   │   ├── Shape.cs (enum: Circle, Triangle, Shield, Rectangle)
│   │   ├── TextLine.cs (font, size, content, position)
│   │   ├── Model.cs (shape, text lines, dimensions, border)
│   │   └── Mesh.cs (vertices, normals, triangles)
│   ├── Services/
│   │   ├── IShapeGenerator.cs / ShapeGenerator.cs
│   │   ├── ITextMeshConverter.cs / TextMeshConverter.cs
│   │   ├── ITextPositioner.cs / TextPositioner.cs
│   │   └── IMeshComposer.cs / MeshComposer.cs
│   └── Utilities/
│       ├── MeshMath.cs (geometric operations)
│       └── STLExporter.cs
│
├── 3DModelGenerator.Data
│   ├── Repository/
│   │   ├── IModelRepository.cs
│   │   └── SqliteModelRepository.cs
│   ├── Models/
│   │   └── ModelEntity.cs (DB schema)
│   └── Database/
│       ├── DatabaseInitializer.cs
│       └── ConnectionFactory.cs
│
├── 3DModelGenerator.Tests (Unit tests for Core & Data)
│
└── packages.config / .csproj (dependencies)
```

## 3. Data Model (SQLite Schema)

```sql
-- Main models table
CREATE TABLE Models (
    ModelId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    ShapeType INTEGER NOT NULL, -- 0=Circle, 1=Triangle, 2=Shield, 3=Rectangle
    ShapeSize REAL NOT NULL,    -- diameter/width in mm
    ShapeThickness REAL DEFAULT 10,
    BorderThickness REAL DEFAULT 5,
    BorderHeight REAL DEFAULT 5,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Text lines within a model
CREATE TABLE TextLines (
    TextLineId INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelId INTEGER NOT NULL,
    LineNumber INTEGER NOT NULL,
    Content TEXT NOT NULL,
    FontName TEXT NOT NULL,
    FontSize REAL NOT NULL,
    TextHeight REAL DEFAULT 5, -- emboss height in mm
    PositionX REAL,            -- relative coords for manual positioning
    PositionY REAL,
    PositionZ REAL DEFAULT 5,  -- height above base
    RotationZ REAL DEFAULT 0,
    AutoPlaced BOOLEAN DEFAULT 0,
    FOREIGN KEY (ModelId) REFERENCES Models(ModelId)
);

-- Mesh cache (final geometry)
CREATE TABLE MeshCache (
    MeshCacheId INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelId INTEGER NOT NULL UNIQUE,
    VerticesJson TEXT NOT NULL,        -- JSON array of [x,y,z] coords
    TriangleIndicesJson TEXT NOT NULL, -- JSON array of triangle indices
    NormalsJson TEXT NOT NULL,         -- pre-calculated normals
    GeneratedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ModelId) REFERENCES Models(ModelId)
);
```

Design rationale:
- Store parameters so models can regenerate/modify easily
- Cache final mesh to avoid re-computing on every load
- Separate `TextLines` table for 1:N multi-line support per model

## 4. Core Components & Responsibilities

**A. Shape Generation** — `ShapeGenerator : IShapeGenerator`
- `GenerateCircle(diameter, thickness, borderThickness) → Mesh`
- `GenerateTriangle(size, thickness, borderThickness) → Mesh`
- `GenerateShield(size, thickness, borderThickness) → Mesh`
- `GenerateRectangle(width, height, thickness, borderThickness) → Mesh`
- Outputs base 3D geometry (vertices + triangle indices); border is embossed as
  separate geometry added to base; all shapes centered at origin (0,0,0)

**B. Text-to-Mesh Conversion** — `TextMeshConverter : ITextMeshConverter`
- `ConvertTextToMesh(textLine, font, size, height) → Mesh`
- `ConvertMultilineText(textLines[]) → Mesh[]`
- Uses `System.Drawing.Font` + GDI+ to rasterize text, converts glyph outlines to 3D
  geometry (extrude height = 5mm); each line generates a separate mesh (enables
  per-line positioning); returns meshes centered at origin

**C. Text Positioning** — `TextPositioner : ITextPositioner`
- `AutoCenter(textMeshes[], shapeBounds) → positions[]`
- `ApplyManualOffset(textMesh, offsetX, offsetY) → position`
- `CalculateRelativeCoords(textMesh, shapeGeometry) → position`
- AutoCenter centers lines vertically & horizontally within shape bounds; manual
  accepts user drag-drop coords (screen → world coords); relative stores X/Y
  offsets and recalculates if shape changes; returns Transform (position +
  rotation) per text mesh

**D. Mesh Composition** — `MeshComposer : IMeshComposer`
- `ComposeModel(baseMesh, textMeshes[], transforms[]) → Mesh`
- `MergeMeshes(meshes[]) → Mesh`
- Combines base shape + border + all text meshes, applies transforms (translation,
  rotation) to position text, merges vertices/indices into single final mesh,
  recalculates normals for lighting

**E. STL Export** — `STLExporter`
- `ExportToSTL(mesh, filePath) → void`
- Converts Mesh to binary STL format; includes normal vectors for proper 3D
  printing orientation

## 5. Workflow (Data Flow)

```
USER INPUT
  -> [UI: Select shape, enter text]
  -> Core.ShapeGenerator
       -> GenerateCircle/Triangle/Shield/Rectangle (base + border)
       -> Mesh A
  -> Core.TextMeshConverter
       -> ConvertTextToMesh (per line, per font/size)
       -> Mesh B1, B2, ...  (one per text line)
  -> Core.TextPositioner
       -> AutoCenter / ApplyManualOffset / CalcRelative
       -> Transform[] (position/rotation per text mesh)
  -> Core.MeshComposer
       -> ComposeModel (merge all + apply transforms)
       -> Final Mesh C
  -> [UI: Display in Helix Viewport]
  -> [User clicks Export]
  -> Core.STLExporter -> FILE.stl
  -> Data.SqliteModelRepository -> Save to DB
```

## 6. Key Interfaces (for UI portability)

```csharp
// Core Logic (UI-agnostic)
namespace ModelGenerator.Core.Services
{
    public interface IShapeGenerator { /* ... */ }
    public interface ITextMeshConverter { /* ... */ }
    public interface ITextPositioner { /* ... */ }
    public interface IMeshComposer { /* ... */ }

    public interface IModelOrchestrator
    {
        // Orchestrates entire workflow
        Task<Mesh> GenerateModel(Model model);
        Task ExportSTL(Mesh mesh, string filePath);
    }
}

// Data Access (abstracted)
namespace ModelGenerator.Data
{
    public interface IModelRepository
    {
        Task<Model> GetModelByIdAsync(int modelId);
        Task SaveModelAsync(Model model, Mesh mesh);
        Task DeleteModelAsync(int modelId);
        Task<List<Model>> ListModelsAsync();
    }
}

// UI doesn't call Core directly; it injects & calls these interfaces.
// When building Mac/Linux versions, swap implementations but keep interfaces.
```

## 7. Technology Stack & Dependencies

| Component        | Library                                  | Why                                   |
|-------------------|-------------------------------------------|----------------------------------------|
| UI                | Windows Forms (.NET 10)                   | VS built-in, simple for desktop        |
| 3D Preview        | Helix Toolkit (WPF)                       | Free, mature, excellent 3D renderer    |
| 3D Export         | Custom + Helix                            | Binary STL writer                      |
| Text-to-3D        | `System.Drawing.Font` + custom tessellation | Built-in .NET, no external deps      |
| Database          | SQLite (`System.Data.SQLite` NuGet)       | Lightweight, per user preference       |
| Serialization     | Newtonsoft.Json (NuGet)                   | Mesh vertices/triangles as JSON in DB  |

NuGet packages: `HelixToolkit`, `System.Data.SQLite`, `Newtonsoft.Json`

## 8. Development Phases

**Phase 1: Core Foundation**
- Data model & SQLite setup
- Shape generator (Circle + Rectangle first, test)
- Mesh data structure + STL exporter
- Unit tests

**Phase 2: Text & Composition**
- Text-to-mesh converter (single line)
- Text positioner (auto-center)
- Mesh composer
- Multi-line text support

**Phase 3: UI (Windows Forms)**
- MainForm with Helix viewport
- Shape selector, text editor
- Live preview (regenerate on each change)
- Position controls (auto/manual/relative)

**Phase 4: Polish**
- Project save/load
- Font selector
- Export dialog
- Error handling

## 9. Design Considerations for Mac/Linux Porting

- Dependency Injection: wire up interfaces in `Program.cs` so swapping
  implementations is one line
- No Windows API calls in Core/Data (no P/Invoke)
- File paths: use `Path.Combine()`, not hardcoded backslashes
- Helix Toolkit -> cross-platform 3D: for Mac/Linux, consider OpenTK or a web
  version (Three.js) with a shared Core
- WinForms -> WPF/Web/Qt#: future UI layer replaces this entirely, Core/Data
  stay untouched
