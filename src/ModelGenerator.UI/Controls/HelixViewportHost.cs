using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CoreMesh = ModelGenerator.Core.Models.Mesh;
using WpfColor = System.Windows.Media.Color;

namespace ModelGenerator.UI.Controls;

/// <summary>Hosts a WPF HelixViewport3D inside the WinForms UI via ElementHost, and knows how
/// to convert a Core Mesh into WPF 3D geometry for display.</summary>
public class HelixViewportHost : UserControl
{
    private readonly HelixViewport3D _viewport;
    private readonly ModelVisual3D _modelVisual = new();

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
        _viewport.Children.Add(_modelVisual);

        var elementHost = new ElementHost { Dock = DockStyle.Fill, Child = _viewport };
        Controls.Add(elementHost);
    }

    public void ShowMesh(CoreMesh mesh, WpfColor color)
    {
        _modelVisual.Content = BuildModel(mesh, color);
    }

    public void Clear()
    {
        _modelVisual.Content = null;
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
