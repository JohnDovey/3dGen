# ModelGeneratorMac (Phase 2)

Native **SwiftUI + SceneKit** front end for macOS. Talks to `ModelGenerator.Host`
over a Unix domain socket (NDJSON RPC) — see [`docs/HOST_PROTOCOL.md`](../../docs/HOST_PROTOCOL.md).

## Requirements

- macOS 14+
- Xcode 16 / Swift 6
- [.NET 10 SDK](https://dotnet.microsoft.com/download) on `PATH` (dev mode launches the host via `dotnet run`)

Optional: publish a self-contained host and set:

```bash
export MODELGENERATOR_HOST=/path/to/ModelGenerator.Host
```

## Run (development)

From the **repository root**:

```bash
# Terminal A is optional — the app starts the host itself.
cd mac/ModelGeneratorMac
swift run
```

Or from the repo root:

```bash
swift run --package-path mac/ModelGeneratorMac
```

First launch may take a while while `dotnet run` builds the host.

## What works in Phase 2

- Left inspector: shape type (circle/triangle/shield/rectangle), size, thickness, border, colors
- Live SceneKit preview (top-down camera, orbit with trackpad/mouse)
- Status bar with vertex/triangle counts or errors
- **Export STL…** (toolbar menu ⌘⇧E or inspector button)

## Not yet (later phases)

- Text lines, SVG/image inserts, libraries, CustomSvg
- Save/Open/Undo, drag-to-reposition, selection outline
- Packaged `.app` with embedded host binary
