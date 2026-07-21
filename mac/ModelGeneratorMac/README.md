# ModelGeneratorMac

Native **SwiftUI + SceneKit** front end for macOS. Talks to `ModelGenerator.Host`
over a Unix domain socket (NDJSON RPC) — see [`docs/HOST_PROTOCOL.md`](../../docs/HOST_PROTOCOL.md).

## Requirements

- macOS 14+
- Xcode 16 / Swift 6
- [.NET 10 SDK](https://dotnet.microsoft.com/download) on `PATH` for **development** only

Release builds embed a self-contained Host; end users do not need .NET.

Optional override:

```bash
export MODELGENERATOR_HOST=/path/to/ModelGenerator.Host
```

## Run (development)

From the **repository root** (so `docs/` Help content is discoverable):

```bash
cd mac/ModelGeneratorMac
swift run
```

First launch may take a while while `dotnet run` builds the host.

The app sets `NSApp` activation policy to **regular** so keystrokes go to TextFields
(not the launching terminal). If the Dock icon does not appear or typing still
echoes in the terminal, click the app window once to make it key.

## Release package

From the repository root:

```bash
./build-release-mac.sh
open dist/ModelGenerator.app
```

Produces `dist/ModelGenerator.app` and `dist/ModelGenerator-v*-osx-*.zip`.

## Features (Phases 2–7 + v0.9)

- Shapes: circle / triangle / shield / rectangle / **Custom SVG**
- Text lines, SVG inserts, image bas-reliefs (libraries with search/tags/import)
- **Border text** (coin-rim lettering: embossed or engraved, anchor angle)
- Live SceneKit preview; drag items to reposition; selection outline
- New / Open / Save / Save As, undo/redo, dirty prompts
- **Export / Import Project** (`.mgproj` portable bundles)
- Export STL
- **Help → How to Use** (shared markdown), **About**

## Layout

```
Sources/ModelGeneratorMac/
  ModelGeneratorMacApp.swift   menus, sheets
  ContentView.swift
  Host/                        process + NDJSON client
  Model/                       AppModel, wire types, undo
  Views/                       inspectors, libraries, SceneKit
  Help/                        Help + About
```
