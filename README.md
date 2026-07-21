# 3D Model Generator

Desktop apps (Windows and macOS) for designing simple 3D-printable models: pick a
base shape, add embossed custom text, SVG graphics, and photo bas-reliefs,
preview them in 3D, and export straight to STL.

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

Windows (WinForms) and macOS (SwiftUI) front ends share portable Core/Data
geometry and SQLite persistence. Full feature set includes custom SVG shapes,
text/SVG/image inserts, libraries with search/tags, undo/redo, unsaved-changes
protection, drag-to-reposition, and STL export. See
[`docs/PLAN.md`](docs/PLAN.md) for architecture and phase history, and
[`docs/HOW_TO_USE.md`](docs/HOW_TO_USE.md) for a walkthrough (also in-app via
**Help → How to Use**).

## Requirements

### Windows app
- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build
- Visual Studio 2022 (17.13+) optional for `ModelGenerator.slnx`

### Mac app
- macOS 14+
- Xcode 16 / Swift 6 to build
- .NET 10 SDK on `PATH` for **development** (`swift run` launches the host via `dotnet run`)
- **Release** builds embed a self-contained Host — end users do **not** need the .NET SDK

## Building and running

### Windows (development)

```
dotnet build
dotnet run --project src/ModelGenerator.UI
```

### macOS (development)

```
dotnet test tests/ModelGenerator.Tests   # portable Core/Data/Host tests
cd mac/ModelGeneratorMac && swift run
```

See [`mac/ModelGeneratorMac/README.md`](mac/ModelGeneratorMac/README.md).

### Tests

```
dotnet test tests/ModelGenerator.Tests
```

(The WinForms project targets Windows; on macOS build/test Core, Data, and Host
only, or pass `-p:EnableWindowsTargeting=true` to restore the UI project.)

## Building a release

### Windows

```
.\build-release.ps1
```

Runs tests, then produces a self-contained single-file win-x64 build and zip:
`dist/ModelGenerator-v<version>-win-x64.zip`. Pass `-SkipTests` or
`-Runtime win-arm64` as needed.

### macOS

```
./build-release-mac.sh            # picks osx-arm64 or osx-x64 from the machine
./build-release-mac.sh osx-arm64
SKIP_TESTS=1 ./build-release-mac.sh
```

Produces:

- `dist/ModelGenerator.app` — SwiftUI UI + embedded `ModelGenerator.Host` + Help
- `dist/ModelGenerator-v<version>-<rid>.zip`

Open with `open dist/ModelGenerator.app`. The app is **not** notarized by the
script; for Gatekeeper-friendly distribution, codesign with a Developer ID and
submit via `notarytool` (commands printed at the end of the script).

Host protocol docs: [`docs/HOST_PROTOCOL.md`](docs/HOST_PROTOCOL.md).

## Project layout

```
src/
  ModelGenerator.Core   Shape generation, text/SVG/image→mesh, STL — portable
  ModelGenerator.Data   SQLite persistence
  ModelGenerator.UI     Windows Forms + Helix Toolkit viewport
  ModelGenerator.Host   Headless NDJSON RPC bridge for the Mac app
mac/
  ModelGeneratorMac     SwiftUI + SceneKit Mac app
docs/                   HOW_TO_USE.md (shared Help), PLAN, protocol
tests/
  ModelGenerator.Tests  Core, Data, Host unit tests
```

## Tech stack

- .NET 10, C# — Core/Data/Host/Windows UI
- SwiftUI + SceneKit — macOS UI
- [Helix Toolkit](https://github.com/helix-toolkit/helix-toolkit) (Windows 3D)
- [LibTessDotNet](https://github.com/speps/LibTessDotNet) tessellation
- [SkiaSharp](https://github.com/mono/SkiaSharp) + [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia)
- SQLite via `Microsoft.Data.Sqlite`
- xUnit

## Copyright

Copyright © John Dovey <dovey.john@gmail.com>
