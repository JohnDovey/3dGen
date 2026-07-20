# 3D Model Generator

A Windows desktop app for designing simple 3D-printable models: pick a base shape,
add embossed custom text, preview it in 3D, and export straight to STL.

Choose a shape — circle, rectangle, triangle, or shield — with a raised border,
then add one or more lines of embossed text with per-line font, size, and
placement (auto-centered, manually positioned, or positioned relative to the
shape). Preview the composed model live in a 3D viewport, save/load designs to a
local SQLite database, and export the final mesh as a binary STL file ready to
slice and print.

## Status

Actively developed. All five initial phases (core geometry/STL export, text
embossing, WinForms UI, save/load, and the full shape set) are done. See
[`docs/PLAN.md`](docs/PLAN.md) for the full architecture write-up and
phase-by-phase status, and [`docs/HOW_TO_USE.md`](docs/HOW_TO_USE.md) for a
walkthrough of using the app.

Known gaps:
- No drag-and-drop text positioning in the viewport yet (Manual/Relative position
  modes take typed X/Y/Z/rotation values instead).

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

## Project layout

```
src/
  ModelGenerator.Core   Shape generation, text-to-mesh conversion, positioning,
                         mesh composition, and STL export — no UI or DB dependency.
  ModelGenerator.Data   SQLite persistence (models + cached mesh geometry).
  ModelGenerator.UI     Windows Forms app: shape/text editors, a Helix Toolkit 3D
                         viewport (hosted via WPF interop), and New/Open/Save/
                         Export STL menus.
tests/
  ModelGenerator.Tests  Unit tests for Core and Data.
```

`ModelGenerator.Core` and `ModelGenerator.Data` have no Windows Forms dependency,
so the UI layer can be swapped out (e.g. for a Mac/Linux front end) without
touching the geometry or persistence logic.

## Tech stack

- .NET 10, C#
- Windows Forms (UI) + WPF interop for the 3D viewport
- [Helix Toolkit](https://github.com/helix-toolkit/helix-toolkit) for 3D rendering
- [LibTessDotNet](https://github.com/speps/LibTessDotNet) for tessellating glyph
  outlines (including holes, e.g. the counter of a letter "O")
- SQLite via `Microsoft.Data.Sqlite`
- xUnit for tests

## Copyright

Copyright © John Dovey <dovey.john@gmail.com>
