# 3D Model Generator

A Windows desktop app for designing simple 3D-printable models: pick a base shape,
add embossed custom text, SVG graphics, and photo bas-reliefs, preview it in 3D,
and export straight to STL.

Choose a shape — circle, rectangle, triangle, shield, or your own custom SVG
outline — with an independently-colored raised border, then add any number of
embossed text lines, SVG graphics, and JPG/PNG photos (each browsed from its own
built-in library with thumbnail previews). A photo is embossed as a grayscale
bas-relief — brightness becomes height, the same way a medallion turns a
portrait into a raised relief — and a transparent PNG background is clipped to
its actual silhouette rather than stamped down as a rectangle. Every item has
its own color, scale/size, and placement (auto-centered, manually positioned, or
positioned relative to the shape). Preview the composed model live in a 3D
viewport — drag any item directly in the viewport to reposition it — save/load
designs to a local SQLite database, and export the final mesh as a binary STL
file ready to slice and print.

## Status

Actively developed. Core geometry/STL export, text embossing, the WinForms UI,
save/load, the full shape set (including custom SVG outlines), an in-app Help
viewer, drag-and-drop positioning with a viewport selection indicator, full
undo/redo, unsaved-changes protection, SVG graphics and photo bas-relief
libraries (with search/tagging/delete) with per-item inserts, and independent
colors for the shape's floor/border and every inserted item are all done. See
[`docs/PLAN.md`](docs/PLAN.md) for the full architecture write-up and
phase-by-phase status, and [`docs/HOW_TO_USE.md`](docs/HOW_TO_USE.md) for a
walkthrough of using the app (also available in-app via Help → How to Use).

## Requirements

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 (17.13+) if you want to open `ModelGenerator.slnx`, or just the
  `dotnet` CLI

## Building and running

```
dotnet build
dotnet run --project src/ModelGenerator.UI
```

Run the test suite:

```
dotnet test
```

## Building a release

```
.\build-release.ps1
```

Runs the tests, then produces a Release, **self-contained single-file** win-x64
build (`dist/publish/ModelGenerator.UI.exe`) and zips that one exe to
`dist/ModelGenerator-v<version>-win-x64.zip`. The .NET runtime, native libraries
(e.g. SQLite), and Help content are packed into the executable — no separate
runtime install is required on the target machine. Pass `-SkipTests` to skip the
test run, or `-Runtime <RID>` to target a different platform (e.g. `win-arm64`).

## Project layout

```
src/
  ModelGenerator.Core   Shape generation (including custom SVG outlines),
                         text-, SVG-, and photo-to-mesh conversion, positioning,
                         mesh composition, and STL export — no UI or DB
                         dependency.
  ModelGenerator.Data   SQLite persistence (models + cached mesh geometry).
  ModelGenerator.UI     Windows Forms app: shape/text/SVG-insert/image-insert
                         editors, SVG and photo library browsers, a Helix
                         Toolkit 3D viewport (hosted via WPF interop), and
                         New/Open/Save/Export STL menus.
tests/
  ModelGenerator.Tests  Unit tests for Core and Data.
```

`ModelGenerator.Core` and `ModelGenerator.Data` have no Windows Forms dependency
and no GDI+ dependency — geometry, text/SVG/image conversion, and SQLite all run
on macOS as well as Windows — so a Mac (SwiftUI) UI can share the same Core/Data
via a host process without rewriting mesh math.

## Tech stack

- .NET 10, C#
- Windows Forms (UI) + WPF interop for the 3D viewport
- [Helix Toolkit](https://github.com/helix-toolkit/helix-toolkit) for 3D rendering
- [LibTessDotNet](https://github.com/speps/LibTessDotNet) for tessellating glyph
  and SVG outlines (including holes, e.g. the counter of a letter "O" or an SVG
  cutout)
- [SkiaSharp](https://github.com/mono/SkiaSharp) + [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia)
  for portable text/SVG/image conversion and library thumbnails (Core runs on
  Windows and macOS)
- SQLite via `Microsoft.Data.Sqlite`
- xUnit for tests

Core and Data are UI-toolkit-agnostic and portable. On macOS you can build and
run the test suite without Windows targeting:

```
dotnet test tests/ModelGenerator.Tests
```

(The WinForms project still requires Windows, or `EnableWindowsTargeting=true`
to cross-compile from another OS.)

## Copyright

Copyright © John Dovey <dovey.john@gmail.com>
