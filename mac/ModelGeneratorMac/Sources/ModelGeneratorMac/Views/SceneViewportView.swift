import AppKit
import SceneKit
import SwiftUI

struct SceneViewportView: View {
    @Environment(AppModel.self) private var appModel

    var body: some View {
        SceneKitView(
            parts: appModel.parts,
            dragPlaneZ: appModel.model.shapeThickness,
            selectedKind: appModel.selectedKind,
            selectedIndex: appModel.selectedIndex,
            statusHint: statusHint,
            onSelect: { kind, index in
                appModel.setSelection(kind: kind, index: index)
            },
            onDrag: { kind, index, x, y, z in
                appModel.applyItemDrag(kind: kind, index: index, x: x, y: y, z: z)
            }
        )
        .background(Color(nsColor: .underPageBackgroundColor))
        .overlay(alignment: .topLeading) {
            Text("3D Preview — drag text/SVG/images to reposition")
                .font(.caption)
                .padding(8)
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 6))
                .padding(8)
        }
        .overlay(alignment: .bottomLeading) {
            if let statusHint {
                Text(statusHint)
                    .font(.caption)
                    .foregroundStyle(appModel.statusIsError ? Color.red : Color.secondary)
                    .padding(8)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 6))
                    .padding(8)
            }
        }
    }

    private var statusHint: String? {
        if appModel.parts == nil {
            return appModel.statusIsError ? appModel.statusText : "Waiting for preview…"
        }
        if let parts = appModel.parts, parts.triangleCount == 0 {
            return "Preview empty (0 triangles)"
        }
        return nil
    }
}

/// NSViewRepresentable wrapper around SCNView — top-down camera, orbit, pick/drag items.
struct SceneKitView: NSViewRepresentable {
    var parts: GeneratePartsResult?
    var dragPlaneZ: Float
    var selectedKind: DraggableKind?
    var selectedIndex: Int?
    var statusHint: String?
    var onSelect: (DraggableKind?, Int?) -> Void
    var onDrag: (DraggableKind, Int, Float, Float, Float) -> Void

    func makeNSView(context: Context) -> SCNView {
        let view = SCNView(frame: NSRect(x: 0, y: 0, width: 640, height: 480))
        let scene = SCNScene()
        view.scene = scene
        view.backgroundColor = NSColor.windowBackgroundColor
        view.allowsCameraControl = true
        view.autoenablesDefaultLighting = true
        view.antialiasingMode = .multisampling4X
        view.rendersContinuously = true

        // Core meshes sit in XY with thickness along +Z. Default camera looks along −Z (+Y up).
        let cameraNode = SCNNode()
        cameraNode.name = "camera"
        cameraNode.camera = SCNCamera()
        cameraNode.camera?.zNear = 0.1
        cameraNode.camera?.zFar = 10_000
        cameraNode.camera?.fieldOfView = 45
        cameraNode.position = SCNVector3(0, 0, 200)
        scene.rootNode.addChildNode(cameraNode)
        view.pointOfView = cameraNode

        let ambient = SCNNode()
        ambient.name = "ambientLight"
        ambient.light = SCNLight()
        ambient.light?.type = .ambient
        ambient.light?.intensity = 400
        ambient.light?.color = NSColor.white
        scene.rootNode.addChildNode(ambient)

        let key = SCNNode()
        key.name = "keyLight"
        key.light = SCNLight()
        key.light?.type = .omni
        key.light?.intensity = 900
        key.position = SCNVector3(80, 120, 200)
        scene.rootNode.addChildNode(key)

        let fill = SCNNode()
        fill.name = "fillLight"
        fill.light = SCNLight()
        fill.light?.type = .omni
        fill.light?.intensity = 400
        fill.position = SCNVector3(-100, -60, 150)
        scene.rootNode.addChildNode(fill)

        let modelRoot = SCNNode()
        modelRoot.name = "modelRoot"
        scene.rootNode.addChildNode(modelRoot)
        context.coordinator.modelRoot = modelRoot
        context.coordinator.scnView = view
        // Force geometry rebuild even if parts identity matches a previous view instance.
        context.coordinator.invalidateGeometryCache()

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
        // If SwiftUI recreated the NSView, re-bind the coordinator.
        if context.coordinator.scnView !== view {
            context.coordinator.scnView = view
            context.coordinator.invalidateGeometryCache()
        }
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

        weak var scnView: SCNView?
        private var selectionNode: SCNNode?
        private var isDragging = false
        private var dragKind: DraggableKind?
        private var dragIndex: Int = -1
        /// Lightweight identity so we rebuild when mesh content changes without comparing megabytes of floats each frame.
        private var lastPartsToken: String?
        private var didFrameCamera = false

        func invalidateGeometryCache() {
            lastPartsToken = nil
            didFrameCamera = false
        }

        func attachGestures(to view: SCNView) {
            scnView = view
            // Avoid stacking gesture recognizers if update re-attaches.
            view.gestureRecognizers
                .filter { $0 is NSClickGestureRecognizer || $0 is NSPanGestureRecognizer }
                .forEach { view.removeGestureRecognizer($0) }

            let click = NSClickGestureRecognizer(target: self, action: #selector(handleClick(_:)))
            view.addGestureRecognizer(click)

            let pan = NSPanGestureRecognizer(target: self, action: #selector(handlePan(_:)))
            pan.buttonMask = 0x1
            view.addGestureRecognizer(pan)
        }

        func apply(parts: GeneratePartsResult?, selectedKind: DraggableKind?, selectedIndex: Int?) {
            let token = Self.partsToken(parts)
            let needsRebuild = token != lastPartsToken || modelRoot.parent == nil
            if needsRebuild {
                lastPartsToken = token
                // Ensure modelRoot is attached to the live scene.
                if modelRoot.parent == nil, let scene = scnView?.scene {
                    scene.rootNode.childNode(withName: "modelRoot", recursively: false)?.removeFromParentNode()
                    scene.rootNode.addChildNode(modelRoot)
                }
                modelRoot.childNodes.forEach { $0.removeFromParentNode() }
                selectionNode = nil
                didFrameCamera = false

                if let parts {
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
                    for item in parts.borderTextMeshes {
                        if let node = makeGeometryNode(mesh: item.mesh, name: "borderText-\(item.index)") {
                            modelRoot.addChildNode(node)
                        }
                    }

                    // Bounding boxes can be stale until the next layout pass.
                    DispatchQueue.main.async { [weak self] in
                        self?.frameCameraIfNeeded()
                    }
                    frameCameraIfNeeded()
                }
            }

            updateSelectionVisual(kind: selectedKind, index: selectedIndex)
        }

        private static func partsToken(_ parts: GeneratePartsResult?) -> String {
            guard let parts else { return "nil" }
            // Lightweight identity so we rebuild when mesh *content* changes without
            // Equatable-comparing megabytes of floats each SwiftUI update.
            // Include per-layer vertex counts + colors so border-text (and other) color-only
            // or topology-stable edits still refresh — analogous to WinForms always
            // ClearVisuals()+re-add on every ShowModel.
            var token = "\(parts.vertexCount)|\(parts.triangleCount)"
            token += "|f:\(parts.floor.vertices.count),\(parts.floor.indices.count),\(parts.floor.colorArgb)"
            token += "|b:\(parts.border.vertices.count),\(parts.border.indices.count),\(parts.border.colorArgb)"
            token += positionedToken("t", parts.textMeshes)
            token += positionedToken("s", parts.svgMeshes)
            token += positionedToken("i", parts.imageMeshes)
            token += positionedToken("bt", parts.borderTextMeshes)
            return token
        }

        private static func positionedToken(_ prefix: String, _ items: [WirePositionedMesh]) -> String {
            var s = "|\(prefix):\(items.count)"
            for item in items {
                s += "{\(item.index),\(item.mesh.vertices.count),\(item.mesh.indices.count),\(item.mesh.colorArgb),\(item.colorArgb)}"
            }
            return s
        }

        /// Place a top-down camera so the whole model is visible.
        private func frameCameraIfNeeded() {
            guard let view = scnView, !didFrameCamera else { return }
            // Prefer union of child geometry bounds (more reliable than empty parent bbox).
            var minB = SCNVector3(CGFloat.greatestFiniteMagnitude, CGFloat.greatestFiniteMagnitude, CGFloat.greatestFiniteMagnitude)
            var maxB = SCNVector3(-CGFloat.greatestFiniteMagnitude, -CGFloat.greatestFiniteMagnitude, -CGFloat.greatestFiniteMagnitude)
            var any = false
            for child in modelRoot.childNodes where child.geometry != nil {
                let (cmin, cmax) = child.boundingBox
                minB.x = min(minB.x, cmin.x); minB.y = min(minB.y, cmin.y); minB.z = min(minB.z, cmin.z)
                maxB.x = max(maxB.x, cmax.x); maxB.y = max(maxB.y, cmax.y); maxB.z = max(maxB.z, cmax.z)
                any = true
            }
            if !any {
                (minB, maxB) = modelRoot.boundingBox
            }

            let dx = maxB.x - minB.x
            let dy = maxB.y - minB.y
            let dz = maxB.z - minB.z
            if dx < 1e-4 && dy < 1e-4 && dz < 1e-4 { return }

            let cx = (minB.x + maxB.x) * 0.5
            let cy = (minB.y + maxB.y) * 0.5
            let cz = (minB.z + maxB.z) * 0.5
            let radius = CGFloat(max(dx, max(dy, dz))) * 0.5
            let distance = max(radius * 3.2, 40)

            if let cam = view.pointOfView {
                cam.position = SCNVector3(cx, cy, cz + distance)
                cam.look(at: SCNVector3(cx, cy, cz), up: SCNVector3(0, 1, 0), localFront: SCNVector3(0, 0, -1))
            }
            didFrameCamera = true
        }

        private func updateSelectionVisual(kind: DraggableKind?, index: Int?) {
            selectionNode?.removeFromParentNode()
            selectionNode = nil
            guard let kind, let index else { return }
            let name = nodeName(kind: kind, index: index)
            guard let target = modelRoot.childNode(withName: name, recursively: true) else { return }

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
                .searchMode: SCNHitTestSearchMode.all.rawValue
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

        private func unprojectToDragPlane(point: NSPoint, in view: SCNView) -> (x: Float, y: Float, z: Float)? {
            let near = view.unprojectPoint(SCNVector3(point.x, point.y, 0))
            let far = view.unprojectPoint(SCNVector3(point.x, point.y, 1))
            let dir = SCNVector3(far.x - near.x, far.y - near.y, far.z - near.z)
            let planeZ = CGFloat(dragPlaneZ)

            if abs(dir.z) < 1e-8 { return nil }
            let t = (planeZ - near.z) / dir.z
            if t < 0 { return nil }

            return (
                Float(near.x + t * dir.x),
                Float(near.y + t * dir.y),
                Float(planeZ)
            )
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

        /// Build geometry with explicit float data buffers (reliable across SceneKit versions).
        private func makeGeometryNode(mesh: WireMesh, name: String) -> SCNNode? {
            guard mesh.vertices.count >= 9, mesh.indices.count >= 3 else { return nil }
            guard mesh.vertices.count % 3 == 0 else { return nil }
            let vertexCount = mesh.vertices.count / 3
            guard mesh.indices.allSatisfy({ $0 >= 0 && $0 < vertexCount }) else { return nil }

            let vertexData = mesh.vertices.withUnsafeBufferPointer { Data(buffer: $0) }
            let positionSource = SCNGeometrySource(
                data: vertexData,
                semantic: .vertex,
                vectorCount: vertexCount,
                usesFloatComponents: true,
                componentsPerVector: 3,
                bytesPerComponent: MemoryLayout<Float>.size,
                dataOffset: 0,
                dataStride: MemoryLayout<Float>.size * 3
            )

            let normals: [Float]
            if mesh.normals.count == mesh.vertices.count {
                normals = mesh.normals
            } else {
                // Flat +Z fallback when host omits normals.
                var n = [Float](repeating: 0, count: mesh.vertices.count)
                for i in stride(from: 2, to: n.count, by: 3) { n[i] = 1 }
                normals = n
            }
            let normalData = normals.withUnsafeBufferPointer { Data(buffer: $0) }
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

            let indices32 = mesh.indices.map { UInt32(clamping: $0) }
            let indexData = indices32.withUnsafeBufferPointer { Data(buffer: $0) }
            let element = SCNGeometryElement(
                data: indexData,
                primitiveType: .triangles,
                primitiveCount: indices32.count / 3,
                bytesPerIndex: MemoryLayout<UInt32>.size
            )

            let geometry = SCNGeometry(sources: [positionSource, normalSource], elements: [element])
            let material = SCNMaterial()
            material.diffuse.contents = NSColor(argb: mesh.colorArgb)
            material.ambient.contents = NSColor(argb: mesh.colorArgb)
            material.lightingModel = .blinn
            material.isDoubleSided = true
            material.locksAmbientWithDiffuse = true
            geometry.materials = [material]

            let node = SCNNode(geometry: geometry)
            node.name = name
            return node
        }
    }
}

private extension NSColor {
    convenience init(argb: Int) {
        // Core stores signed ARGB ints (e.g. LightSteelBlue = -5192482).
        let u = UInt32(bitPattern: Int32(truncatingIfNeeded: argb))
        let a = CGFloat((u >> 24) & 0xFF) / 255.0
        let r = CGFloat((u >> 16) & 0xFF) / 255.0
        let g = CGFloat((u >> 8) & 0xFF) / 255.0
        let b = CGFloat(u & 0xFF) / 255.0
        self.init(srgbRed: r, green: g, blue: b, alpha: a == 0 ? 1 : a)
    }
}
