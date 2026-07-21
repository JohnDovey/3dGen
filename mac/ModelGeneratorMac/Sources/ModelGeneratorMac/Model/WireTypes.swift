import Foundation

// MARK: - Wire model (matches ModelGenerator.Core JSON / Host protocol)

struct WireModel: Codable, Equatable {
    var name: String = ""
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
}

struct WireTextLine: Codable, Equatable {
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
}

struct WireSvgInsert: Codable, Equatable {
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
    // CustomSvg = 4 deferred to later phase

    var id: Int { rawValue }

    var description: String {
        switch self {
        case .circle: return "Circle"
        case .triangle: return "Triangle"
        case .shield: return "Shield"
        case .rectangle: return "Rectangle"
        }
    }
}
