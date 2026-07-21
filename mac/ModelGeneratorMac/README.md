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

## What works (through Phase 5)

- Left inspector: shape type (circle/triangle/shield/rectangle/**Custom SVG**), size, thickness, border, colors
- **Text lines:** add/remove; content, system fonts, size, emboss, color, AutoCenter/Manual/Relative + X/Y/Z/Rot
- **SVG inserts:** library browser (search, tags, import, delete, thumbnails), insert params, live preview
- **CustomSvg** base shape from the same library
- Live SceneKit preview (top-down camera, orbit) including text + SVG meshes
- Status bar with vertex/triangle counts or errors
- **File:** New / Open / Save / Save As (same SQLite DB as Windows under Application Support)
- **Edit:** Undo / Redo (⌘Z / ⇧⌘Z), dirty `*` in title, discard prompts
- **Export STL…** (⌘⇧E or inspector button)

## Not yet (later phases)

- Image bas-relief inserts + image library
- Drag-to-reposition, selection outline
- Packaged `.app` with embedded host binary
