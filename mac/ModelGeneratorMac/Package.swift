// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "ModelGeneratorMac",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(name: "ModelGeneratorMac", targets: ["ModelGeneratorMac"])
    ],
    targets: [
        .executableTarget(
            name: "ModelGeneratorMac",
            path: "Sources/ModelGeneratorMac"
        )
    ]
)
