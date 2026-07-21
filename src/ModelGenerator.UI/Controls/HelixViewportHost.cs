using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CoreMesh = ModelGenerator.Core.Models.Mesh;
using WpfColor = System.Windows.Media.Color;

namespace ModelGenerator.UI.Controls;

/// <summary>A mesh with the color it should be rendered in — used for the shape's floor/border,
/// which are colored but not individually draggable.</summary>
public readonly record struct ColoredMesh(CoreMesh Mesh, WpfColor Color);

/// <summary>Which kind of item a draggable visual represents, so a drag event can be routed back
/// to the right panel (TextLinesPanel vs SvgInsertsPanel vs ImageInsertsPanel).</summary>
public enum DraggableItemKind
{
    TextLine,
    SvgInsert,
    ImageInsert
}

/// <summary>A mesh with its own color that can be picked up and dragged in the viewport — a text
/// line, SVG insert, or image insert. Index is that item's position within its own list (TextLines,
/// SvgInserts, or ImageInserts), not a global index across all three.</summary>
public readonly record struct DraggableMesh(CoreMesh Mesh, WpfColor Color, DraggableItemKind Kind, int Index);

/// <summary>Hosts a WPF HelixViewport3D inside the WinForms UI via ElementHost, converts Core
/// Meshes into WPF 3D geometry, and supports dragging individual text-line/SVG-insert visuals to
/// reposition them (switches that item to Manual position mode). The shape's floor and border are
/// rendered with their own colors but are never draggable.</summary>
public class HelixViewportHost : UserControl
{
    private readonly HelixViewport3D _viewport;
    private readonly Dictionary<ModelVisual3D, (DraggableItemKind Kind, int Index)> _draggableVisualToItem = new();

    private ModelVisual3D? _floorVisual;
    private ModelVisual3D? _borderVisual;
    private LinesVisual3D? _selectionVisual;
    private (DraggableItemKind Kind, int Index)? _selectedItem;
    private bool _isDragging;
    private DraggableItemKind _draggedKind;
    private int _draggedIndex = -1;
    private double _dragPlaneZ;

    /// <summary>Raised while dragging an item's visual, with its new world X/Y/Z (mm) — Z is the
    /// drag plane's Z (the shape's top surface), so callers can set an absolute position without
    /// needing to separately track shape thickness.</summary>
    public event Action<DraggableItemKind, int, float, float, float>? ItemDragged;

    public HelixViewportHost()
    {
        _viewport = new HelixViewport3D
        {
            ShowCoordinateSystem = true,
            ShowViewCube = true,
            ZoomExtentsWhenLoaded = true,
            // Top-down, looking straight along -Z: matches how the model reads on the print bed
            // (world +X -> screen right, world +Y -> screen up), removing any ambiguity an
            // isometric default camera angle would introduce.
            Camera = new PerspectiveCamera
            {
                Position = new Point3D(0, 0, 300),
                LookDirection = new Vector3D(0, 0, -300),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 45
            }
        };

        _viewport.Children.Add(new ModelVisual3D { Content = new AmbientLight(WpfColor.FromRgb(90, 90, 90)) });
        _viewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(Colors.White, new Vector3D(-1, -1, -3)) });
        _viewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(WpfColor.FromRgb(60, 60, 60), new Vector3D(1, 1, 2)) });

        _viewport.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        _viewport.PreviewMouseMove += OnPreviewMouseMove;
        _viewport.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;

        var elementHost = new ElementHost { Dock = DockStyle.Fill, Child = _viewport };
        Controls.Add(elementHost);
    }

    /// <summary>Renders the shape's floor and border (each their own color, never draggable) and
    /// every text/SVG/image item as a separate, individually pickable, individually colored visual.
    /// Optional non-draggable border-text meshes are rendered after the border.
    /// dragPlaneZ is the world Z of the shape's top surface, used when dragging an item.</summary>
    public void ShowModel(
        ColoredMesh floor,
        ColoredMesh border,
        IReadOnlyList<DraggableMesh> items,
        float dragPlaneZ,
        IReadOnlyList<ColoredMesh>? borderTextMeshes = null)
    {
        ClearVisuals();

        _floorVisual = new ModelVisual3D { Content = BuildModel(floor.Mesh, floor.Color) };
        _viewport.Children.Add(_floorVisual);

        _borderVisual = new ModelVisual3D { Content = BuildModel(border.Mesh, border.Color) };
        _viewport.Children.Add(_borderVisual);

        if (borderTextMeshes is not null)
        {
            foreach (var bt in borderTextMeshes)
            {
                if (bt.Mesh.Vertices.Count == 0)
                {
                    continue;
                }

                _viewport.Children.Add(new ModelVisual3D { Content = BuildModel(bt.Mesh, bt.Color) });
            }
        }

        foreach (var item in items)
        {
            var visual = new ModelVisual3D { Content = BuildModel(item.Mesh, item.Color) };
            _viewport.Children.Add(visual);
            _draggableVisualToItem[visual] = (item.Kind, item.Index);
        }

        _dragPlaneZ = dragPlaneZ;

        // ShowModel is called again after every edit (including on every mouse-move while
        // dragging), rebuilding every visual from scratch — re-apply the highlight to whichever
        // item is still selected so it keeps following the item instead of disappearing after
        // the very first regeneration.
        UpdateSelectionVisual();
    }

    public void Clear()
    {
        ClearVisuals();
        _selectedItem = null;
    }

    private void ClearVisuals()
    {
        if (_floorVisual is not null)
        {
            _viewport.Children.Remove(_floorVisual);
            _floorVisual = null;
        }
        if (_borderVisual is not null)
        {
            _viewport.Children.Remove(_borderVisual);
            _borderVisual = null;
        }
        foreach (var visual in _draggableVisualToItem.Keys)
        {
            _viewport.Children.Remove(visual);
        }
        _draggableVisualToItem.Clear();

        if (_selectionVisual is not null)
        {
            _viewport.Children.Remove(_selectionVisual);
            _selectionVisual = null;
        }
    }

    /// <summary>Rebuilds the selection highlight (a wireframe box slightly larger than the
    /// selected item's bounds) around whichever visual currently matches _selectedItem, or
    /// removes it if nothing is selected / the selected item no longer exists.</summary>
    private void UpdateSelectionVisual()
    {
        if (_selectionVisual is not null)
        {
            _viewport.Children.Remove(_selectionVisual);
            _selectionVisual = null;
        }

        if (_selectedItem is not { } selected)
        {
            return;
        }

        foreach (var (visual, item) in _draggableVisualToItem)
        {
            if (item != selected)
            {
                continue;
            }
            if (visual.Content is GeometryModel3D { Geometry: MeshGeometry3D geometry })
            {
                _selectionVisual = BuildSelectionBox(geometry.Bounds);
                _viewport.Children.Add(_selectionVisual);
            }
            break;
        }
    }

    private void SetSelection((DraggableItemKind Kind, int Index)? selection)
    {
        _selectedItem = selection;
        UpdateSelectionVisual();
    }

    /// <summary>Builds a wireframe box slightly larger than `bounds` (so it reads as an outline
    /// around the item rather than clipping through its surface) — the visual "what's currently
    /// selected" indicator.</summary>
    private static LinesVisual3D BuildSelectionBox(Rect3D bounds)
    {
        const double margin = 0.5;
        double x0 = bounds.X - margin, x1 = bounds.X + bounds.SizeX + margin;
        double y0 = bounds.Y - margin, y1 = bounds.Y + bounds.SizeY + margin;
        double z0 = bounds.Z - margin, z1 = bounds.Z + bounds.SizeZ + margin;

        var c000 = new Point3D(x0, y0, z0);
        var c100 = new Point3D(x1, y0, z0);
        var c110 = new Point3D(x1, y1, z0);
        var c010 = new Point3D(x0, y1, z0);
        var c001 = new Point3D(x0, y0, z1);
        var c101 = new Point3D(x1, y0, z1);
        var c111 = new Point3D(x1, y1, z1);
        var c011 = new Point3D(x0, y1, z1);

        var points = new Point3DCollection
        {
            c000, c100, c100, c110, c110, c010, c010, c000, // bottom face
            c001, c101, c101, c111, c111, c011, c011, c001, // top face
            c000, c001, c100, c101, c110, c111, c010, c011  // vertical edges
        };

        return new LinesVisual3D
        {
            Points = points,
            Color = Colors.Yellow,
            Thickness = 2
        };
    }

    private void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var position = e.GetPosition(_viewport.Viewport);
        if (VisualTreeHelper.HitTest(_viewport.Viewport, position) is RayMeshGeometry3DHitTestResult { VisualHit: ModelVisual3D visual }
            && _draggableVisualToItem.TryGetValue(visual, out var item))
        {
            _isDragging = true;
            _draggedKind = item.Kind;
            _draggedIndex = item.Index;
            SetSelection(item);
            _viewport.Viewport.CaptureMouse();
            e.Handled = true; // suppress HelixViewport3D's default click-drag camera rotation
        }
        else
        {
            // Clicked empty space / the (non-draggable) floor or border — deselect, matching
            // ordinary click-to-select-or-deselect behavior.
            SetSelection(null);
        }
    }

    private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = e.GetPosition(_viewport.Viewport);
        var planeOrigin = new Point3D(0, 0, _dragPlaneZ);
        var planeNormal = new Vector3D(0, 0, 1);
        if (Viewport3DHelper.UnProject(_viewport.Viewport, position, planeOrigin, planeNormal) is Point3D worldPoint)
        {
            ItemDragged?.Invoke(_draggedKind, _draggedIndex, (float)worldPoint.X, (float)worldPoint.Y, (float)_dragPlaneZ);
        }
        e.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _draggedIndex = -1;
        _viewport.Viewport.ReleaseMouseCapture();
        e.Handled = true;
    }

    private static Model3D BuildModel(CoreMesh mesh, WpfColor color)
    {
        var geometry = new MeshGeometry3D();
        foreach (var v in mesh.Vertices)
        {
            geometry.Positions.Add(new Point3D(v.X, v.Y, v.Z));
        }
        foreach (var n in mesh.Normals)
        {
            geometry.Normals.Add(new Vector3D(n.X, n.Y, n.Z));
        }
        foreach (var index in mesh.Indices)
        {
            geometry.TriangleIndices.Add(index);
        }

        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        material.Children.Add(new SpecularMaterial(System.Windows.Media.Brushes.White, 40));

        return new GeometryModel3D(geometry, material) { BackMaterial = material };
    }
}
