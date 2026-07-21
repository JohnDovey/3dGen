import SceneKit
import SwiftUI

struct SceneViewportView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        SceneKitView(parts: appModel.parts)
            .background(Color(nsColor: .windowBackgroundColor))
            .overlay(alignment: .topLeading) {
                Text("3D Preview")
                    .font(.caption)
                    .padding(8)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 6))
                    .padding(8)
            }
    }
}

/// NSViewRepresentable wrapper around SCNView — top-down camera, orbit allowed via default controls.
struct SceneKitView: NSViewRepresentable {
    var parts: GeneratePartsResult?

    func makeNSView(context: Context) -> SCNView {
        let view = SCNView()
        view.scene = SCNScene()
        view.backgroundColor = NSColor.underPageBackgroundColor
        view.allowsCameraControl = true
        view.autoenablesDefaultLighting = false
        view.antialiasingMode = .multisampling4X

        // Top-down camera matching HelixViewportHost: +X right, +Y up, look along -Z
        let cameraNode = SCNNode()
        cameraNode.name = "camera"
        cameraNode.camera = SCNCamera()
        cameraNode.camera?.fieldOfView = 45
        cameraNode.position = SCNVector3(0, 0, 300)
        cameraNode.look(at: SCNVector3(0, 0, 0), up: SCNVector3(0, 1, 0), localFront: SCNVector3(0, 0, -1))
        view.scene?.rootNode.addChildNode(cameraNode)
        view.pointOfView = cameraNode

        let ambient = SCNNode()
        ambient.light = SCNLight()
        ambient.light?.type = .ambient
        ambient.light?.color = NSColor(calibratedWhite: 0.35, alpha: 1)
        view.scene?.rootNode.addChildNode(ambient)

        let key = SCNNode()
        key.light = SCNLight()
        key.light?.type = .directional
        key.light?.color = NSColor.white
        key.eulerAngles = SCNVector3(-0.8, 0.4, 0)
        view.scene?.rootNode.addChildNode(key)

        let fill = SCNNode()
        fill.light = SCNLight()
        fill.light?.type = .directional
        fill.light?.color = NSColor(calibratedWhite: 0.25, alpha: 1)
        fill.eulerAngles = SCNVector3(0.5, -0.6, 0.2)
        view.scene?.rootNode.addChildNode(fill)

        context.coordinator.modelRoot = SCNNode()
        context.coordinator.modelRoot.name = "modelRoot"
        view.scene?.rootNode.addChildNode(context.coordinator.modelRoot)

        return view
    }

    func updateNSView(_ view: SCNView, context: Context) {
        context.coordinator.apply(parts: parts)
    }

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    final class Coordinator {
        var modelRoot = SCNNode()

        func apply(parts: GeneratePartsResult?) {
            modelRoot.childNodes.forEach { $0.removeFromParentNode() }
            guard let parts else { return }

            if let floor = makeGeometryNode(mesh: parts.floor, name: "floor") {
                modelRoot.addChildNode(floor)
            }
            if let border = makeGeometryNode(mesh: parts.border, name: "border") {
                modelRoot.addChildNode(border)
            }
            for item in parts.textMeshes {
                if let node = makeGeometryNode(mesh: item.mesh, name: "text-\(item.index)") {
                    modelRoot.addChildNode(node)
                }
            }
            for item in parts.svgMeshes {
                if let node = makeGeometryNode(mesh: item.mesh, name: "svg-\(item.index)") {
                    modelRoot.addChildNode(node)
                }
            }
            for item in parts.imageMeshes {
                if let node = makeGeometryNode(mesh: item.mesh, name: "image-\(item.index)") {
                    modelRoot.addChildNode(node)
                }
            }
        }

        private func makeGeometryNode(mesh: WireMesh, name: String) -> SCNNode? {
            guard mesh.vertices.count >= 9, mesh.indices.count >= 3 else { return nil }
            guard mesh.vertices.count % 3 == 0 else { return nil }

            // Pack as contiguous Float xyz — on macOS SCNVector3 is CGFloat (Double), which is
            // the wrong component width for usesFloatComponents: true.
            let vertexCount = mesh.vertices.count / 3
            let positionFloats = mesh.vertices
            var normalFloats = mesh.normals
            if normalFloats.count < positionFloats.count {
                normalFloats.append(contentsOf: Array(repeating: Float(0), count: positionFloats.count - normalFloats.count))
                // Default missing normals to +Z
                for i in 0..<vertexCount {
                    let base = i * 3
                    if base + 2 < mesh.normals.count { continue }
                    normalFloats[base] = 0
                    normalFloats[base + 1] = 0
                    normalFloats[base + 2] = 1
                }
            }

            let positionData = positionFloats.withUnsafeBufferPointer { Data(buffer: $0) }
            let normalData = normalFloats.withUnsafeBufferPointer { Data(buffer: $0) }

            let positionSource = SCNGeometrySource(
                data: positionData,
                semantic: .vertex,
                vectorCount: vertexCount,
                usesFloatComponents: true,
                componentsPerVector: 3,
                bytesPerComponent: MemoryLayout<Float>.size,
                dataOffset: 0,
                dataStride: MemoryLayout<Float>.size * 3
            )
            let normalSource = SCNGeometrySource(
                data: normalData,
                semantic: .normal,
                vectorCount: vertexCount,
                usesFloatComponents: true,
                componentsPerVector: 3,
                bytesPerComponent: MemoryLayout<Float>.size,
                dataOffset: 0,
                dataStride: MemoryLayout<Float>.size * 3
            )

            var indices32 = mesh.indices.map { Int32($0) }
            let indexData = Data(bytes: &indices32, count: MemoryLayout<Int32>.size * indices32.count)
            let element = SCNGeometryElement(
                data: indexData,
                primitiveType: .triangles,
                primitiveCount: indices32.count / 3,
                bytesPerIndex: MemoryLayout<Int32>.size
            )

            let geometry = SCNGeometry(sources: [positionSource, normalSource], elements: [element])
            let material = SCNMaterial()
            material.diffuse.contents = NSColor(argb: mesh.colorArgb)
            material.lightingModel = .blinn
            material.isDoubleSided = true
            geometry.materials = [material]

            let node = SCNNode(geometry: geometry)
            node.name = name
            return node
        }
    }
}

private extension NSColor {
    convenience init(argb: Int) {
        let a = CGFloat((argb >> 24) & 0xFF) / 255.0
        let r = CGFloat((argb >> 16) & 0xFF) / 255.0
        let g = CGFloat((argb >> 8) & 0xFF) / 255.0
        let b = CGFloat(argb & 0xFF) / 255.0
        self.init(srgbRed: r, green: g, blue: b, alpha: a == 0 ? 1 : a)
    }
}
