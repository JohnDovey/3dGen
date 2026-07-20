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

Remaining ideas (not currently planned as a phase): richer validation feedback in dialogs,
drag-and-drop text positioning in the viewport.


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
