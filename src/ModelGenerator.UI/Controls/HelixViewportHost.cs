using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CoreMesh = ModelGenerator.Core.Models.Mesh;
using WpfColor = System.Windows.Media.Color;

namespace ModelGenerator.UI.Controls;

/// <summary>Hosts a WPF HelixViewport3D inside the WinForms UI via ElementHost, converts Core
/// Meshes into WPF 3D geometry, and supports dragging individual text-line visuals to
/// reposition them (switches that line to Manual position mode).</summary>
public class HelixViewportHost : UserControl
{
    private readonly HelixViewport3D _viewport;
    private readonly Dictionary<ModelVisual3D, int> _textVisualToLineIndex = new();

    private ModelVisual3D? _baseVisual;
    private bool _isDragging;
    private int _draggedLineIndex = -1;
    private double _dragPlaneZ;

    /// <summary>Raised while dragging a text line's visual, with its new world X/Y/Z (mm) — Z is
    /// the drag plane's Z (the shape's top surface), so callers can set an absolute position
    /// without needing to separately track shape thickness.</summary>
    public event Action<int, float, float, float>? TextLineDragged;

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

    /// <summary>Renders the base shape and each text line as separate, individually pickable
    /// visuals. dragPlaneZ is the world Z of the shape's top surface, used when dragging text.</summary>
    public void ShowModel(CoreMesh baseMesh, IReadOnlyList<CoreMesh> textMeshes, WpfColor baseColor, WpfColor textColor, float dragPlaneZ)
    {
        Clear();

        _baseVisual = new ModelVisual3D { Content = BuildModel(baseMesh, baseColor) };
        _viewport.Children.Add(_baseVisual);

        for (int i = 0; i < textMeshes.Count; i++)
        {
            var visual = new ModelVisual3D { Content = BuildModel(textMeshes[i], textColor) };
            _viewport.Children.Add(visual);
            _textVisualToLineIndex[visual] = i;
        }

        _dragPlaneZ = dragPlaneZ;
    }

    public void Clear()
    {
        if (_baseVisual is not null)
        {
            _viewport.Children.Remove(_baseVisual);
            _baseVisual = null;
        }
        foreach (var visual in _textVisualToLineIndex.Keys)
        {
            _viewport.Children.Remove(visual);
        }
        _textVisualToLineIndex.Clear();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var position = e.GetPosition(_viewport.Viewport);
        if (VisualTreeHelper.HitTest(_viewport.Viewport, position) is RayMeshGeometry3DHitTestResult { VisualHit: ModelVisual3D visual }
            && _textVisualToLineIndex.TryGetValue(visual, out int lineIndex))
        {
            _isDragging = true;
            _draggedLineIndex = lineIndex;
            _viewport.Viewport.CaptureMouse();
            e.Handled = true; // suppress HelixViewport3D's default click-drag camera rotation
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
            TextLineDragged?.Invoke(_draggedLineIndex, (float)worldPoint.X, (float)worldPoint.Y, (float)_dragPlaneZ);
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
        _draggedLineIndex = -1;
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
