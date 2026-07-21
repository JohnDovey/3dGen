import AppKit
import SceneKit
import SwiftUI

struct SceneViewportView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        SceneKitView(
            parts: appModel.parts,
            dragPlaneZ: appModel.model.shapeThickness,
            selectedKind: appModel.selectedKind,
            selectedIndex: appModel.selectedIndex,
            onSelect: { kind, index in
                appModel.setSelection(kind: kind, index: index)
            },
            onDrag: { kind, index, x, y, z in
                appModel.applyItemDrag(kind: kind, index: index, x: x, y: y, z: z)
            }
        )
        .background(Color(nsColor: .windowBackgroundColor))
        .overlay(alignment: .topLeading) {
            Text("3D Preview — drag text/SVG/images to reposition")
                .font(.caption)
                .padding(8)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 6))
                .padding(8)
        }
    }
}

/// NSViewRepresentable wrapper around SCNView — top-down camera, orbit, pick/drag items.
struct SceneKitView: NSViewRepresentable {
    var parts: GeneratePartsResult?
    var dragPlaneZ: Float
    var selectedKind: DraggableKind?
    var selectedIndex: Int?
    var onSelect: (DraggableKind?, Int?) -> Void
    var onDrag: (DraggableKind, Int, Float, Float, Float) -> Void

    func makeNSView(context: Context) -> SCNView {
        let view = SCNView()
        view.scene = SCNScene()
        view.backgroundColor = NSColor.underPageBackgroundColor
        view.allowsCameraControl = true
        view.autoenablesDefaultLighting = false
        view.antialiasingMode = .multisampling4X

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

        context.coordinator.attachGestures(to: view)
        context.coordinator.onSelect = onSelect
        context.coordinator.onDrag = onDrag
        context.coordinator.dragPlaneZ = dragPlaneZ

        return view
    }

    func updateNSView(_ view: SCNView, context: Context) {
        context.coordinator.onSelect = onSelect
        context.coordinator.onDrag = onDrag
        context.coordinator.dragPlaneZ = dragPlaneZ
        context.coordinator.apply(
            parts: parts,
            selectedKind: selectedKind,
            selectedIndex: selectedIndex
        )
    }

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    @MainActor
    final class Coordinator: NSObject {
        var modelRoot = SCNNode()
        var dragPlaneZ: Float = 10
        var onSelect: (DraggableKind?, Int?) -> Void = { _, _ in }
        var onDrag: (DraggableKind, Int, Float, Float, Float) -> Void = { _, _, _, _, _ in }

        private weak var scnView: SCNView?
        private var selectionNode: SCNNode?
        private var isDragging = false
        private var dragKind: DraggableKind?
        private var dragIndex: Int = -1
        private var lastParts: GeneratePartsResult?

        func attachGestures(to view: SCNView) {
            scnView = view
            let click = NSClickGestureRecognizer(target: self, action: #selector(handleClick(_:)))
            view.addGestureRecognizer(click)

            let pan = NSPanGestureRecognizer(target: self, action: #selector(handlePan(_:)))
            pan.buttonMask = 0x1 // left button
            view.addGestureRecognizer(pan)
        }

        func apply(parts: GeneratePartsResult?, selectedKind: DraggableKind?, selectedIndex: Int?) {
            // Rebuild when mesh content changes (including reposition after drag).
            if parts != lastParts {
                lastParts = parts
                modelRoot.childNodes.forEach { $0.removeFromParentNode() }
                selectionNode = nil
                if let parts {
                    if let floor = makeGeometryNode(mesh: parts.floor, name: "floor", draggable: false) {
                        modelRoot.addChildNode(floor)
                    }
                    if let border = makeGeometryNode(mesh: parts.border, name: "border", draggable: false) {
                        modelRoot.addChildNode(border)
                    }
                    for item in parts.textMeshes {
                        if let node = makeGeometryNode(mesh: item.mesh, name: "text-\(item.index)", draggable: true) {
                            modelRoot.addChildNode(node)
                        }
                    }
                    for item in parts.svgMeshes {
                        if let node = makeGeometryNode(mesh: item.mesh, name: "svg-\(item.index)", draggable: true) {
                            modelRoot.addChildNode(node)
                        }
                    }
                    for item in parts.imageMeshes {
                        if let node = makeGeometryNode(mesh: item.mesh, name: "image-\(item.index)", draggable: true) {
                            modelRoot.addChildNode(node)
                        }
                    }
                }
            }

            updateSelectionVisual(kind: selectedKind, index: selectedIndex)
        }

        private func updateSelectionVisual(kind: DraggableKind?, index: Int?) {
            selectionNode?.removeFromParentNode()
            selectionNode = nil
            guard let kind, let index else { return }
            let name = nodeName(kind: kind, index: index)
            guard let target = modelRoot.childNode(withName: name, recursively: false) else { return }

            let bounds = target.boundingBox
            let sizeX = bounds.max.x - bounds.min.x
            let sizeY = bounds.max.y - bounds.min.y
            let sizeZ = bounds.max.z - bounds.min.z
            let box = SCNBox(
                width: Swift.max(CGFloat(sizeX), 0.5) + 1,
                height: Swift.max(CGFloat(sizeY), 0.5) + 1,
                length: Swift.max(CGFloat(sizeZ), 0.5) + 1,
                chamferRadius: 0
            )
            let mat = SCNMaterial()
            mat.diffuse.contents = NSColor.clear
            mat.emission.contents = NSColor.systemYellow
            mat.fillMode = .lines
            mat.isDoubleSided = true
            box.materials = [mat]

            let node = SCNNode(geometry: box)
            node.position = SCNVector3(
                (bounds.min.x + bounds.max.x) / 2,
                (bounds.min.y + bounds.max.y) / 2,
                (bounds.min.z + bounds.max.z) / 2
            )
            node.name = "selection"
            modelRoot.addChildNode(node)
            selectionNode = node
        }

        @objc private func handleClick(_ gr: NSClickGestureRecognizer) {
            guard let view = scnView, gr.state == .ended else { return }
            let p = gr.location(in: view)
            if let hit = hitDraggable(at: p, in: view) {
                onSelect(hit.kind, hit.index)
            } else {
                onSelect(nil, nil)
            }
        }

        @objc private func handlePan(_ gr: NSPanGestureRecognizer) {
            guard let view = scnView else { return }
            let p = gr.location(in: view)

            switch gr.state {
            case .began:
                if let hit = hitDraggable(at: p, in: view) {
                    isDragging = true
                    dragKind = hit.kind
                    dragIndex = hit.index
                    view.allowsCameraControl = false
                    onSelect(hit.kind, hit.index)
                    if let world = unprojectToDragPlane(point: p, in: view) {
                        onDrag(hit.kind, hit.index, world.x, world.y, world.z)
                    }
                } else {
                    isDragging = false
                }
            case .changed:
                guard isDragging, let kind = dragKind else { return }
                if let world = unprojectToDragPlane(point: p, in: view) {
                    onDrag(kind, dragIndex, world.x, world.y, world.z)
                }
            case .ended, .cancelled, .failed:
                isDragging = false
                dragKind = nil
                dragIndex = -1
                view.allowsCameraControl = true
            default:
                break
            }
        }

        private func hitDraggable(at point: NSPoint, in view: SCNView) -> (kind: DraggableKind, index: Int)? {
            let hits = view.hitTest(point, options: [
                .searchMode: SCNHitTestSearchMode.all.rawValue,
                .boundingBoxOnly: false
            ])
            for hit in hits {
                var node: SCNNode? = hit.node
                while let n = node {
                    if let parsed = parseDraggableName(n.name) {
                        return parsed
                    }
                    node = n.parent
                }
            }
            return nil
        }

        /// Ray from camera through point, intersect plane Z = dragPlaneZ (world).
        private func unprojectToDragPlane(point: NSPoint, in view: SCNView) -> (x: Float, y: Float, z: Float)? {
            guard let cameraNode = view.pointOfView else { return nil }

            // Near/far points in world space
            let near = view.unprojectPoint(SCNVector3(point.x, point.y, 0))
            let far = view.unprojectPoint(SCNVector3(point.x, point.y, 1))
            let dir = SCNVector3(far.x - near.x, far.y - near.y, far.z - near.z)
            let planeZ = CGFloat(dragPlaneZ)

            // near.z + t * dir.z = planeZ
            if abs(dir.z) < 1e-8 { return nil }
            let t = (planeZ - near.z) / dir.z
            if t < 0 { return nil }

            let x = Float(near.x + t * dir.x)
            let y = Float(near.y + t * dir.y)
            let z = Float(planeZ)
            _ = cameraNode
            return (x, y, z)
        }

        private func parseDraggableName(_ name: String?) -> (kind: DraggableKind, index: Int)? {
            guard let name else { return nil }
            let parts = name.split(separator: "-")
            guard parts.count == 2, let index = Int(parts[1]) else { return nil }
            switch parts[0] {
            case "text": return (.text, index)
            case "svg": return (.svg, index)
            case "image": return (.image, index)
            default: return nil
            }
        }

        private func nodeName(kind: DraggableKind, index: Int) -> String {
            "\(kind.rawValue)-\(index)"
        }

        private func makeGeometryNode(mesh: WireMesh, name: String, draggable: Bool) -> SCNNode? {
            guard mesh.vertices.count >= 9, mesh.indices.count >= 3 else { return nil }
            guard mesh.vertices.count % 3 == 0 else { return nil }

            let vertexCount = mesh.vertices.count / 3
            let positionFloats = mesh.vertices
            var normalFloats = mesh.normals
            if normalFloats.count < positionFloats.count {
                normalFloats.append(contentsOf: Array(repeating: Float(0), count: positionFloats.count - normalFloats.count))
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
            node.categoryBitMask = draggable ? 2 : 1
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
