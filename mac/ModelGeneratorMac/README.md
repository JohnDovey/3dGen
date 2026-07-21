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

## Release package

From the repository root:

```bash
./build-release-mac.sh
open dist/ModelGenerator.app
```

Produces `dist/ModelGenerator.app` and `dist/ModelGenerator-v*-osx-*.zip`.

## Features (Phases 2–7)

- Shapes: circle / triangle / shield / rectangle / **Custom SVG**
- Text lines, SVG inserts, image bas-reliefs (libraries with search/tags/import)
- Live SceneKit preview; drag items to reposition; selection outline
- New / Open / Save / Save As, undo/redo, dirty prompts
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
