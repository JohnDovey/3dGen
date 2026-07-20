using ModelGenerator.Core.Services;
using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class STLExporterTests
{
    [Fact]
    public void ExportToSTL_WritesValidBinaryHeaderAndTriangleCount()
    {
        var mesh = new ShapeGenerator().GenerateCircle(diameter: 60, thickness: 10, borderThickness: 5, borderHeight: 5);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.stl");

        try
        {
            STLExporter.ExportToSTL(mesh, path);

            using var reader = new BinaryReader(File.OpenRead(path));
            reader.ReadBytes(80); // header
            uint triangleCount = reader.ReadUInt32();

            Assert.Equal((uint)(mesh.Indices.Count / 3), triangleCount);

            var fileInfo = new FileInfo(path);
            long expectedSize = 84 + triangleCount * 50L;
            Assert.Equal(expectedSize, fileInfo.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
