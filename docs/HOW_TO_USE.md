# How to Use 3D Model Generator

This guide walks through creating a simple embossed-text model and exporting it
for 3D printing.

## Launching the app

Run `ModelGenerator.UI.exe` (or `dotnet run --project src/ModelGenerator.UI`).
The main window opens with a default circular shape already previewed in the 3D
viewport on the right:

![Main window with the default circle shape](images/01-main-window.png)

The window has three parts:
- **Left panel** — shape parameters, text lines, and the Export STL button
- **3D viewport** — a live preview of the model, viewed from directly above
  (drag with the mouse to rotate/orbit; scroll to zoom)
- **Status bar** — shows the current vertex/triangle count, or an error message
  if the current settings can't be generated (e.g. a border thicker than the
  shape itself)

## 1. Choose a shape

Under **Shape**, pick one of:

- **Circle** — set its diameter under **Size (mm)**
- **Rectangle** — set both **Size (mm)** (width) and **Height (mm)**
- **Triangle** — an equilateral triangle; **Size (mm)** sets the side length
- **Shield** — a heraldic-style silhouette; **Size (mm)** sets the top-edge width
- **CustomSvg** — the shape's own outline comes from a library SVG instead of a
  built-in silhouette (see below); **Size (mm)** sets its longer bounding-box
  dimension

Then set:
- **Thickness (mm)** — how thick the flat base is
- **Border thickness (mm)** — how wide the raised rim around the edge is
- **Border height (mm)** — how far that rim is embossed above the base

The 3D preview regenerates automatically every time you change a value.

**Using a custom shape:** pick **CustomSvg** under **Shape**, then click
**Choose...** next to **Custom shape SVG** to browse the same SVG library used
for [SVG graphics](#4-inserting-svg-graphics) below and pick a file. The SVG's
largest contour becomes the shape's outer boundary — the same embossed border,
thickness, and floor/border colors apply as any other shape:

![A five-pointed star used as the base shape via CustomSvg](images/07-custom-shape-svg.png)

This only works cleanly for a single simple outline; if the SVG has multiple
separate shapes, the largest one is used and the rest is ignored. Highly
concave or self-intersecting outlines may not extrude cleanly — stick to a
single star-like or blob-like silhouette for best results.

## 2. Add text

Under **Text lines**, each row is one line of embossed text:

- **Text** — the line's content
- **Font** / **Size** — the font name (drawn in its own typeface in the
  dropdown) and point size
- **Emboss (mm)** — how far this line is raised above the shape's surface
- **Position** — how this line is placed:
  - **AutoCenter** — lines are stacked and centered automatically; this is the
    default and needs no further input
  - **Manual** — type absolute **X**, **Y**, **Z** (height above the base) and
    **Rot°** (rotation) coordinates directly
  - **Relative** — type **X**/**Y** as an offset from the shape's center, and
    **Z** as an air-gap above the shape's surface; recalculates if the shape is
    resized

Click **+ Add Text Line** for more lines — each line can use a different font,
size, and position mode. Click **Remove** on a row to delete it.

![A circle with "HELLO" embossed and auto-centered](images/02-text-added.png)

**Dragging text in the viewport:** click and drag any line of text directly in
the 3D preview to reposition it — this switches that line to **Manual** mode
and fills in its X/Y/Z automatically. Clicking an item (whether or not you
drag it) outlines it with a yellow selection box so it's always clear which
item you're about to move; clicking empty space deselects. The base shape
itself isn't draggable (dragging it just orbits the camera as usual).

## 3. Choosing colors

Everything in the model can be colored independently:

- **Base color** / **Border color** — under the shape parameters, each opens a
  color picker for the shape's floor and its raised rim
- **Color** on a text line — opens a color picker for just that line
- **Color** on an SVG insert — opens a color picker for just that insert

![Base/border color swatches and the SVG inserts panel](images/05-colors-and-svg-inserts-panel.png)

Colors are saved and reloaded with the model like everything else.

## 4. Inserting SVG graphics

Under **SVG inserts**, click **+ Insert SVG...** to open the library:

![SVG library with thumbnails](images/06-svg-library.png)

- **Import SVG...** copies one or more `.svg` files from disk into the app's
  library (`%LOCALAPPDATA%\ModelGenerator\SvgLibrary`) — they stay available
  for future models
- Select a file (its thumbnail previews the shape, including any holes/cutouts
  it contains) and click **Insert**
- **Search** filters the library by filename or tag as you type; **Tags...**
  lets you attach comma-separated keywords to the selected file (e.g. `logo,
  brand`) so it's easier to find later; **Delete** permanently removes the
  selected file (and its tags) from the library — this only affects the
  library, not any model that already has it inserted, since inserted content
  is copied into the model at insert time

The inserted graphic gets its own row, just like a text line:

- **Scale (mm)** — the size along its longer bounding-box dimension
- **Emboss (mm)** — how far it's raised above the shape's surface
- **Color** — its own color picker
- **Position** / **X**/**Y**/**Z**/**Rot°** — same AutoCenter/Manual/Relative
  modes as text lines

**Dragging an SVG insert in the viewport** works the same way as text: click
and drag it directly in the 3D preview to reposition it (switches it to
**Manual** mode). Text lines and SVG inserts drag independently of each other
and of the base shape.

## 5. Inserting a photo (bas-relief)

A JPG or PNG can be embossed onto the shape as a **bas-relief** — brightness
becomes height, the same way a coin or medallion turns a portrait into a
raised relief. Under **Image inserts**, click **+ Insert Image...** to open
the library (separate from the SVG library, since thumbnailing a photo and
thumbnailing an SVG are different operations under the hood):

![Image library with a gradient photo and a transparent-background logo](images/08-image-library.png)

- **Import Image...** copies one or more `.png`/`.jpg`/`.jpeg` files from disk
  into the app's library (`%LOCALAPPDATA%\ModelGenerator\ImageLibrary`)
- Select a file and click **Insert**
- **Search**, **Tags...**, and **Delete** work the same way as in the SVG
  library above

The inserted photo gets its own row:

- **Scale (mm)** — the size along its longer bounding-box dimension
- **Relief (mm)** — how far the brightest point is raised above the surface
- **Detail** — Low/Medium/High grid resolution; higher looks smoother but
  costs more triangles (and takes a moment longer to regenerate on every
  change), so start at Medium and only go higher if you need it
- **Invert** — flips which end is raised: unchecked, brighter pixels are
  higher; checked, darker pixels are
- **Color** — its own color picker
- **Position** / **X**/**Y**/**Z**/**Rot°** — same AutoCenter/Manual/Relative
  modes as text lines and SVG inserts

**Transparent PNGs are clipped to their actual shape**, not stamped down as a
rectangle — a logo cut out on a transparent background becomes a relief in
just that logo's silhouette:

![A gradient photo and a transparent-background logo, each as their own bas-relief](images/09-image-bas-relief.png)

**Dragging an image insert in the viewport** works the same way as text and
SVG inserts: click and drag it directly in the 3D preview to reposition it
(switches it to **Manual** mode).

## 6. Undo and redo

**Edit → Undo** (Ctrl+Z) and **Edit → Redo** (Ctrl+Y) step backward/forward
through your edits — every shape/color/text/insert change, and every drag in
the viewport, is undoable. A whole drag (from click to release) undoes as one
step rather than one step per pixel of mouse movement, and the same applies to
typing in a text field — pause for about half a second and the next edit
starts a fresh undo step.

## 7. Save, open, and start a new model

The **File** menu has:

![File menu](images/03-file-menu.png)

- **New** — resets everything back to the default circle with no text
- **Open...** — lists models previously saved to the local SQLite database;
  select one and click **Open**, or **Delete** to remove it
- **Save** — saves the current model; prompts for a name the first time
- **Save As...** — always prompts for a name and saves as a new model
- **Export STL...** — see below
- **Exit** — closes the app

If the current model has unsaved changes (the title bar shows a trailing
`*`), **New**, **Open...**, and closing the window all ask whether to save
first, discard the changes, or cancel — so a stray click never silently loses
work.

Saved models (including their generated mesh) live in a local SQLite database at
`%LOCALAPPDATA%\ModelGenerator\models.sqlite`.

## 8. Export to STL

Click **Export STL...** (left panel or File menu) and choose where to save the
`.stl` file. This exports the exact mesh currently shown in the preview, ready
to open in a slicer (PrusaSlicer, Cura, etc.) and print.

## Help and About

**Help → How to Use** opens this guide inside the app. **Help → About** shows
the app's version and copyright:

![About dialog](images/04-about.png)
