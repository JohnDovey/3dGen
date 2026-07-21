# ModelGenerator.Host protocol

Headless bridge so a **SwiftUI** Mac app (or any client) can use portable
`ModelGenerator.Core` / `Data` without reimplementing geometry.

**Decision (Phase 1):** Host process + **NDJSON RPC** over a Unix domain socket
(or stdio). NativeAOT / in-process C ABI is deferred unless process overhead
becomes a product issue.

## Running

```bash
# Default socket: ~/Library/Application Support/ModelGenerator/host.sock  (macOS)
dotnet run --project src/ModelGenerator.Host -- serve

# Explicit socket
dotnet run --project src/ModelGenerator.Host -- serve --socket /tmp/modelgen.sock

# NDJSON on stdin/stdout
dotnet run --project src/ModelGenerator.Host -- stdio

# One-shot (no long-running process)
dotnet run --project src/ModelGenerator.Host -- ping
dotnet run --project src/ModelGenerator.Host -- export --model model.json --stl out.stl
dotnet run --project src/ModelGenerator.Host -- generate-parts --model model.json --out parts.json
```

Self-contained publish (Apple Silicon example):

```bash
dotnet publish src/ModelGenerator.Host -c Release -r osx-arm64 --self-contained true -o dist/host-osx-arm64
```

## Wire format

- **Transport:** one JSON object per line (UTF-8, `\n`-terminated).
- **Protocol version:** `1.0` (returned by `ping`).
- **Property names:** camelCase.
- **Enums:** numbers or camelCase strings (`shapeType`, `positionMode`, `detail`).
- **Images:** `imageData` is standard JSON base64 for `byte[]`.

### Request

```json
{"id":"1","method":"ping","params":{}}
```

### Response (success)

```json
{"id":"1","result":{"ok":true,"version":"0.8.0","protocol":"1.0"}}
```

### Response (error)

```json
{"id":"1","error":{"code":-32601,"message":"Unknown method: foo"}}
```

Error codes follow JSON-RPC style: `-32700` parse, `-32600` invalid request,
`-32601` method not found, `-32602` invalid params, `-32603` internal.

## Methods

### `ping`

No params. Returns host assembly version + protocol version.

### `generateParts`

```json
{"id":"2","method":"generateParts","params":{"model":{ /* Model */ }}}
```

Returns:

```json
{
  "floor": { "vertices":[...], "normals":[...], "indices":[...], "colorArgb":0 },
  "border": { ... },
  "textMeshes": [ { "index":0, "colorArgb":..., "mesh":{...} } ],
  "svgMeshes": [ ... ],
  "imageMeshes": [ ... ],
  "vertexCount": 1234,
  "triangleCount": 567
}
```

Meshes use **flat float arrays** for vertices/normals (`[x,y,z, x,y,z, ...]`).

### `exportStl`

```json
{"id":"3","method":"exportStl","params":{"model":{ /* Model */ },"path":"/tmp/out.stl"}}
```

Generates the merged mesh and writes a **binary STL**. Returns path, byte size,
vertex/triangle counts.

### `listModels`

No required params. Returns summaries for the Open dialog:

```json
{"models":[{"id":1,"name":"Badge","shapeType":0,"modifiedDate":"2026-07-21T12:00:00Z"}]}
```

### `getModel`

```json
{"id":"4","method":"getModel","params":{"id":1}}
```

Returns `{ "model": { /* full Model including text/svg/image inserts */ } }`.

### `saveModel`

```json
{"id":"5","method":"saveModel","params":{"model":{ /* Model with name, id 0 = insert */ },"saveMesh":true}}
```

Persists parameters to SQLite (same DB as WinForms:
`~/Library/Application Support/ModelGenerator/models.sqlite` on macOS) and, when
`saveMesh` is true (default), caches the generated mesh. Returns
`{ "id": 1, "name": "Badge" }`.

### `deleteModel`

```json
{"id":"6","method":"deleteModel","params":{"id":1}}
```

Returns `{ "id": 1, "deleted": true }`.

## Example model JSON

```json
{
  "name": "Demo",
  "shapeType": 0,
  "shapeSize": 60,
  "shapeHeight": 40,
  "shapeThickness": 10,
  "borderThickness": 5,
  "borderHeight": 5,
  "baseColorArgb": -5192482,
  "borderColorArgb": -5192482,
  "textLines": [],
  "svgInserts": [],
  "imageInserts": []
}
```

`shapeType`: 0=Circle, 1=Triangle, 2=Shield, 3=Rectangle, 4=CustomSvg.
