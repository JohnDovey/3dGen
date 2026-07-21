import Foundation

// MARK: - Wire model (matches ModelGenerator.Core JSON / Host protocol)

struct WireModel: Codable, Equatable {
    var id: Int = 0
    var name: String = "Untitled"
    var shapeType: Int = 0
    var shapeSize: Float = 60
    var shapeHeight: Float = 40
    var shapeThickness: Float = 10
    var borderThickness: Float = 5
    var borderHeight: Float = 5
    var baseColorArgb: Int = -5_192_482 // LightSteelBlue
    var borderColorArgb: Int = -5_192_482
    var customShapeSvgContent: String? = nil
    var customShapeSourceFileName: String? = nil
    var textLines: [WireTextLine] = []
    var svgInserts: [WireSvgInsert] = []
    var imageInserts: [WireImageInsert] = []

    /// Deep copy via JSON (used for undo snapshots).
    func deepCopy() -> WireModel {
        let encoder = JSONEncoder()
        let decoder = JSONDecoder()
        guard let data = try? encoder.encode(self),
              let copy = try? decoder.decode(WireModel.self, from: data)
        else {
            return self
        }
        return copy
    }

    static func blankDocument() -> WireModel {
        var m = WireModel()
        m.id = 0
        m.name = "Untitled"
        m.textLines = [WireTextLine.blank(lineNumber: 0)]
        return m
    }
}

struct ModelSummary: Codable, Identifiable, Equatable {
    var id: Int
    var name: String
    var shapeType: Int
    var modifiedDate: Date?

    var shapeLabel: String {
        ShapeTypeOption(rawValue: shapeType)?.description ?? "Shape \(shapeType)"
    }
}

struct ListModelsResult: Codable {
    var models: [ModelSummary]
}

struct GetModelResult: Codable {
    var model: WireModel
}

struct SaveModelResult: Codable {
    var id: Int
    var name: String
}

struct DeleteModelResult: Codable {
    var id: Int
    var deleted: Bool
}

struct WireTextLine: Codable, Equatable, Identifiable {
    /// UI-stable identity (not sent to Host).
    var id: UUID = UUID()

    var lineNumber: Int = 0
    var content: String = ""
    var fontName: String = "Arial"
    var fontSize: Float = 12
    var textHeight: Float = 5
    var positionMode: Int = 0
    var positionX: Float = 0
    var positionY: Float = 0
    var positionZ: Float = 0
    var rotationZ: Float = 0
    var colorArgb: Int = -29_696 // DarkOrange

    enum CodingKeys: String, CodingKey {
        case lineNumber, content, fontName, fontSize, textHeight
        case positionMode, positionX, positionY, positionZ, rotationZ, colorArgb
    }
}

struct WireSvgInsert: Codable, Equatable, Identifiable {
    var id: UUID = UUID()

    var lineNumber: Int = 0
    var sourceFileName: String? = nil
    var svgContent: String = ""
    var scale: Float = 40
    var embossHeight: Float = 5
    var positionMode: Int = 0
    var positionX: Float = 0
    var positionY: Float = 0
    var positionZ: Float = 0
    var rotationZ: Float = 0
    var colorArgb: Int = -29_696

    enum CodingKeys: String, CodingKey {
        case lineNumber, sourceFileName, svgContent, scale, embossHeight
        case positionMode, positionX, positionY, positionZ, rotationZ, colorArgb
    }

    var positionModeOption: PositionModeOption {
        get { PositionModeOption(rawValue: positionMode) ?? .autoCenter }
        set { positionMode = newValue.rawValue }
    }

    static func fromLibrary(fileName: String, content: String, lineNumber: Int) -> WireSvgInsert {
        var insert = WireSvgInsert()
        insert.lineNumber = lineNumber
        insert.sourceFileName = fileName
        insert.svgContent = content
        insert.scale = 40
        insert.embossHeight = 5
        insert.positionMode = PositionModeOption.autoCenter.rawValue
        insert.colorArgb = -29_696
        return insert
    }
}

struct SvgLibraryItem: Codable, Identifiable, Equatable {
    var fileName: String
    var keywords: [String] = []

    var id: String { fileName }
}

struct SvgLibraryListResult: Codable {
    var files: [SvgLibraryItem]
}

struct SvgImportResult: Codable {
    var fileName: String
}

struct SvgContentResult: Codable {
    var fileName: String
    var content: String
}

struct SvgThumbnailResult: Codable {
    var png: Data
    var width: Int
    var height: Int
}

struct SvgKeywordsResult: Codable {
    var fileName: String
    var keywords: [String]
}

struct WireImageInsert: Codable, Equatable {
    var lineNumber: Int = 0
    var sourceFileName: String? = nil
    var imageData: Data = Data()
    var scale: Float = 40
    var reliefHeight: Float = 3
    var detail: Int = 1
    var invert: Bool = false
    var positionMode: Int = 0
    var positionX: Float = 0
    var positionY: Float = 0
    var positionZ: Float = 0
    var rotationZ: Float = 0
    var colorArgb: Int = -29_696
}

// MARK: - generateParts result

struct WireMesh: Codable, Equatable {
    var vertices: [Float] = []
    var normals: [Float] = []
    var indices: [Int] = []
    var colorArgb: Int = 0
}

struct WirePositionedMesh: Codable, Equatable {
    var index: Int = 0
    var colorArgb: Int = 0
    var mesh: WireMesh = WireMesh()
}

struct GeneratePartsResult: Codable, Equatable {
    var floor: WireMesh = WireMesh()
    var border: WireMesh = WireMesh()
    var textMeshes: [WirePositionedMesh] = []
    var svgMeshes: [WirePositionedMesh] = []
    var imageMeshes: [WirePositionedMesh] = []
    var vertexCount: Int = 0
    var triangleCount: Int = 0
}

struct PingResult: Codable {
    var ok: Bool
    var version: String
    var protocolVersion: String

    enum CodingKeys: String, CodingKey {
        case ok, version
        case protocolVersion = "protocol"
    }
}

struct ExportStlResult: Codable {
    var path: String
    var bytes: Int64
    var vertexCount: Int
    var triangleCount: Int
}

enum ShapeTypeOption: Int, CaseIterable, Identifiable, CustomStringConvertible {
    case circle = 0
    case triangle = 1
    case shield = 2
    case rectangle = 3
    case customSvg = 4

    var id: Int { rawValue }

    var description: String {
        switch self {
        case .circle: return "Circle"
        case .triangle: return "Triangle"
        case .shield: return "Shield"
        case .rectangle: return "Rectangle"
        case .customSvg: return "Custom SVG"
        }
    }
}

/// Matches Core `TextPositionMode`.
enum PositionModeOption: Int, CaseIterable, Identifiable, CustomStringConvertible {
    case autoCenter = 0
    case manual = 1
    case relative = 2

    var id: Int { rawValue }

    var description: String {
        switch self {
        case .autoCenter: return "AutoCenter"
        case .manual: return "Manual"
        case .relative: return "Relative"
        }
    }

    var showsXYZ: Bool { self != .autoCenter }
}

extension WireTextLine {
    var positionModeOption: PositionModeOption {
        get { PositionModeOption(rawValue: positionMode) ?? .autoCenter }
        set { positionMode = newValue.rawValue }
    }

    static func blank(lineNumber: Int = 0) -> WireTextLine {
        var line = WireTextLine()
        line.lineNumber = lineNumber
        line.content = ""
        line.fontName = FontCatalog.preferredDefaultFamily
        line.fontSize = 12
        line.textHeight = 5
        line.positionMode = PositionModeOption.autoCenter.rawValue
        line.colorArgb = -29_696
        return line
    }
}

