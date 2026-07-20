using System.Numerics;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using Xunit;

namespace ModelGenerator.Tests;

public class TextPositionerTests
{
    private readonly TextPositioner _positioner = new();

    [Fact]
    public void AutoCenter_SingleLine_CentersOnOriginAtShapeSurface()
    {
        var mesh = new Mesh();
        mesh.AddTriangle(new Vector3(-5, -2, 0), new Vector3(5, -2, 0), new Vector3(5, 2, 0));

        var model = new Model { ShapeThickness = 10 };
        var transforms = _positioner.AutoCenter(new[] { mesh }, model);

        Assert.Single(transforms);
        Assert.Equal(0f, transforms[0].Position.X, precision: 3);
        Assert.Equal(0f, transforms[0].Position.Y, precision: 3);
        Assert.Equal(10f, transforms[0].Position.Z, precision: 3);
    }

    [Fact]
    public void AutoCenter_MultipleLines_StacksTopToBottomWithoutOverlap()
    {
        var line1 = new Mesh();
        line1.AddTriangle(new Vector3(-5, -2, 0), new Vector3(5, -2, 0), new Vector3(5, 2, 0));
        var line2 = new Mesh();
        line2.AddTriangle(new Vector3(-3, -1, 0), new Vector3(3, -1, 0), new Vector3(3, 1, 0));

        var model = new Model { ShapeThickness = 10 };
        var transforms = _positioner.AutoCenter(new[] { line1, line2 }, model);

        Assert.Equal(2, transforms.Count);
        // First line should sit above the second (larger Y).
        Assert.True(transforms[0].Position.Y > transforms[1].Position.Y);
    }

    [Fact]
    public void ApplyManualOffset_UsesStoredAbsoluteCoordinates()
    {
        var line = new TextLine { PositionX = 12, PositionY = -4, PositionZ = 15, RotationZ = 90 };
        var transform = _positioner.ApplyManualOffset(line);

        Assert.Equal(new Vector3(12, -4, 15), transform.Position);
        Assert.Equal(MathF.PI / 2f, transform.RotationZ, precision: 4);
    }

    [Fact]
    public void CalculateRelativeCoords_AddsShapeThicknessToZ()
    {
        var line = new TextLine { PositionX = 3, PositionY = 2, PositionZ = 1 };
        var model = new Model { ShapeThickness = 10 };

        var transform = _positioner.CalculateRelativeCoords(line, model);

        Assert.Equal(11f, transform.Position.Z, precision: 3);
    }
}
