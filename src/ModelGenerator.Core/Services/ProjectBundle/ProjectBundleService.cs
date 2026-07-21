using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Utilities;

namespace ModelGenerator.Core.Services.ProjectBundle;

public sealed class ProjectBundleService : IProjectBundleService
{
    public const int SupportedFormatVersion = 1;

    private readonly ISvgLibraryService _svgLibrary;
    private readonly IImageLibraryService _imageLibrary;

    public ProjectBundleService(ISvgLibraryService svgLibrary, IImageLibraryService imageLibrary)
    {
        _svgLibrary = svgLibrary;
        _imageLibrary = imageLibrary;
    }

    public void ExportBundle(Model model, string zipFilePath, string? appVersion = null)
    {
        string? dir = Path.GetDirectoryName(zipFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        using var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
        var manifest = new BundleManifest
        {
            FormatVersion = SupportedFormatVersion,
            AppVersion = appVersion,
            ModelName = model.Name,
            ShapeType = model.ShapeType,
            ShapeSize = model.ShapeSize,
            ShapeHeight = model.ShapeHeight,
            ShapeThickness = model.ShapeThickness,
            BorderThickness = model.BorderThickness,
            BorderHeight = model.BorderHeight,
            BaseColorArgb = model.BaseColorArgb,
            BorderColorArgb = model.BorderColorArgb,
            CustomShapeSourceFileName = model.CustomShapeSourceFileName
        };

        if (!string.IsNullOrEmpty(model.CustomShapeSvgContent))
        {
            string assetPath = "assets/svg/custom_shape.svg";
            WriteTextEntry(archive, assetPath, model.CustomShapeSvgContent);
            manifest.CustomShapeAssetPath = assetPath;
        }

        foreach (var svg in model.SvgInserts.OrderBy(s => s.LineNumber))
        {
            string safe = SanitizeFileName(svg.SourceFileName ?? $"insert_{svg.LineNumber}.svg", ".svg");
            string assetPath = $"assets/svg/{svg.LineNumber}_{safe}";
            WriteTextEntry(archive, assetPath, svg.SvgContent ?? "");
            manifest.SvgInserts.Add(new BundleSvgInsert
            {
                LineNumber = svg.LineNumber,
                SourceFileName = svg.SourceFileName,
                AssetPath = assetPath,
                Scale = svg.Scale,
                EmbossHeight = svg.EmbossHeight,
                PositionMode = svg.PositionMode,
                PositionX = svg.PositionX,
                PositionY = svg.PositionY,
                PositionZ = svg.PositionZ,
                RotationZ = svg.RotationZ,
                ColorArgb = svg.ColorArgb
            });
        }

        foreach (var img in model.ImageInserts.OrderBy(i => i.LineNumber))
        {
            string ext = GuessImageExtension(img.SourceFileName, img.ImageData);
            string safe = SanitizeFileName(img.SourceFileName ?? $"insert_{img.LineNumber}{ext}", ext);
            string assetPath = $"assets/images/{img.LineNumber}_{safe}";
            WriteBytesEntry(archive, assetPath, img.ImageData ?? Array.Empty<byte>());
            manifest.ImageInserts.Add(new BundleImageInsert
            {
                LineNumber = img.LineNumber,
                SourceFileName = img.SourceFileName,
                AssetPath = assetPath,
                Scale = img.Scale,
                ReliefHeight = img.ReliefHeight,
                Detail = img.Detail,
                Invert = img.Invert,
                PositionMode = img.PositionMode,
                PositionX = img.PositionX,
                PositionY = img.PositionY,
                PositionZ = img.PositionZ,
                RotationZ = img.RotationZ,
                ColorArgb = img.ColorArgb
            });
        }

        foreach (var line in model.TextLines.OrderBy(t => t.LineNumber))
        {
            manifest.TextLines.Add(new BundleTextLine
            {
                LineNumber = line.LineNumber,
                Content = line.Content,
                FontName = line.FontName,
                FontSize = line.FontSize,
                TextHeight = line.TextHeight,
                PositionMode = line.PositionMode,
                PositionX = line.PositionX,
                PositionY = line.PositionY,
                PositionZ = line.PositionZ,
                RotationZ = line.RotationZ,
                ColorArgb = line.ColorArgb
            });
        }

        foreach (var line in model.BorderTextLines.OrderBy(t => t.LineNumber))
        {
            manifest.BorderTextLines.Add(new BundleBorderTextLine
            {
                LineNumber = line.LineNumber,
                Content = line.Content,
                FontName = line.FontName,
                FontSize = line.FontSize,
                Height = line.Height,
                Mode = line.Mode,
                AnchorAngleDegrees = line.AnchorAngleDegrees,
                AnchorMode = line.AnchorMode,
                ColorArgb = line.ColorArgb
            });
        }

        string json = JsonSerializer.Serialize(manifest, CoreJsonOptions.Pretty);
        WriteTextEntry(archive, "manifest.json", json);
    }

    public Model ImportBundle(string zipFilePath)
    {
        using var archive = ZipFile.OpenRead(zipFilePath);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("Not a valid project bundle: missing manifest.json.");

        BundleManifest manifest;
        using (var stream = manifestEntry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            string json = reader.ReadToEnd();
            manifest = JsonSerializer.Deserialize<BundleManifest>(json, CoreJsonOptions.Default)
                ?? throw new InvalidOperationException("Could not deserialize project bundle manifest.");
        }

        if (manifest.FormatVersion != SupportedFormatVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported project bundle format version {manifest.FormatVersion} (expected {SupportedFormatVersion}).");
        }

        var model = new Model
        {
            Id = 0,
            Name = manifest.ModelName,
            ShapeType = manifest.ShapeType,
            ShapeSize = manifest.ShapeSize,
            ShapeHeight = manifest.ShapeHeight,
            ShapeThickness = manifest.ShapeThickness,
            BorderThickness = manifest.BorderThickness,
            BorderHeight = manifest.BorderHeight,
            BaseColorArgb = manifest.BaseColorArgb,
            BorderColorArgb = manifest.BorderColorArgb,
            CustomShapeSourceFileName = manifest.CustomShapeSourceFileName
        };

        if (!string.IsNullOrEmpty(manifest.CustomShapeAssetPath))
        {
            string svg = ReadTextEntry(archive, manifest.CustomShapeAssetPath);
            string preferred = manifest.CustomShapeSourceFileName ?? "custom_shape.svg";
            string libraryName = ImportSvgDeduped(preferred, svg);
            model.CustomShapeSvgContent = svg;
            model.CustomShapeSourceFileName = libraryName;
        }

        foreach (var t in manifest.TextLines.OrderBy(x => x.LineNumber))
        {
            model.TextLines.Add(new TextLine
            {
                LineNumber = t.LineNumber,
                Content = t.Content,
                FontName = t.FontName,
                FontSize = t.FontSize,
                TextHeight = t.TextHeight,
                PositionMode = t.PositionMode,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                PositionZ = t.PositionZ,
                RotationZ = t.RotationZ,
                ColorArgb = t.ColorArgb
            });
        }

        foreach (var s in manifest.SvgInserts.OrderBy(x => x.LineNumber))
        {
            string svg = ReadTextEntry(archive, s.AssetPath);
            string preferred = s.SourceFileName ?? Path.GetFileName(s.AssetPath);
            string libraryName = ImportSvgDeduped(preferred, svg);
            model.SvgInserts.Add(new SvgInsert
            {
                LineNumber = s.LineNumber,
                SourceFileName = libraryName,
                SvgContent = svg,
                Scale = s.Scale,
                EmbossHeight = s.EmbossHeight,
                PositionMode = s.PositionMode,
                PositionX = s.PositionX,
                PositionY = s.PositionY,
                PositionZ = s.PositionZ,
                RotationZ = s.RotationZ,
                ColorArgb = s.ColorArgb
            });
        }

        foreach (var img in manifest.ImageInserts.OrderBy(x => x.LineNumber))
        {
            byte[] data = ReadBytesEntry(archive, img.AssetPath);
            string preferred = img.SourceFileName ?? Path.GetFileName(img.AssetPath);
            string libraryName = ImportImageDeduped(preferred, data);
            model.ImageInserts.Add(new ImageInsert
            {
                LineNumber = img.LineNumber,
                SourceFileName = libraryName,
                ImageData = data,
                Scale = img.Scale,
                ReliefHeight = img.ReliefHeight,
                Detail = img.Detail,
                Invert = img.Invert,
                PositionMode = img.PositionMode,
                PositionX = img.PositionX,
                PositionY = img.PositionY,
                PositionZ = img.PositionZ,
                RotationZ = img.RotationZ,
                ColorArgb = img.ColorArgb
            });
        }

        foreach (var b in manifest.BorderTextLines.OrderBy(x => x.LineNumber))
        {
            model.BorderTextLines.Add(new BorderTextLine
            {
                LineNumber = b.LineNumber,
                Content = b.Content,
                FontName = b.FontName,
                FontSize = b.FontSize,
                Height = b.Height,
                Mode = b.Mode,
                AnchorAngleDegrees = b.AnchorAngleDegrees,
                AnchorMode = b.AnchorMode,
                ColorArgb = b.ColorArgb
            });
        }

        return model;
    }

    private string ImportSvgDeduped(string preferredFileName, string content)
    {
        string hash = Sha256Hex(Encoding.UTF8.GetBytes(content));
        foreach (var existing in _svgLibrary.ListSvgFiles())
        {
            string existingContent = _svgLibrary.ReadSvgContent(existing);
            if (Sha256Hex(Encoding.UTF8.GetBytes(existingContent)) == hash)
            {
                return existing;
            }
        }

        return _svgLibrary.ImportContent(preferredFileName, content);
    }

    private string ImportImageDeduped(string preferredFileName, byte[] data)
    {
        string hash = Sha256Hex(data);
        foreach (var existing in _imageLibrary.ListImageFiles())
        {
            byte[] existingData = _imageLibrary.ReadImageBytes(existing);
            if (Sha256Hex(existingData) == hash)
            {
                return existing;
            }
        }

        return _imageLibrary.ImportContent(preferredFileName, data);
    }

    private static string Sha256Hex(byte[] data)
    {
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private static void WriteBytesEntry(ZipArchive archive, string entryName, byte[] data)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static string ReadTextEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"Bundle is missing asset '{entryName}'.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytesEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"Bundle is missing asset '{entryName}'.");
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string SanitizeFileName(string name, string defaultExt)
    {
        string file = Path.GetFileName(name);
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            file = file.Replace(c, '_');
        }

        if (string.IsNullOrWhiteSpace(file))
        {
            file = "asset" + defaultExt;
        }

        return file;
    }

    private static string GuessImageExtension(string? sourceFileName, byte[]? data)
    {
        string? ext = Path.GetExtension(sourceFileName ?? "");
        if (!string.IsNullOrEmpty(ext))
        {
            return ext;
        }

        if (data is { Length: >= 3 } && data[0] == 0xFF && data[1] == 0xD8)
        {
            return ".jpg";
        }

        return ".png";
    }
}
