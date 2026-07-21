import AppKit
import Combine
import Foundation
import SwiftUI
import UniformTypeIdentifiers

@MainActor
final class AppModel: ObservableObject {
    @Published var model = WireModel()
    @Published var parts: GeneratePartsResult?
    @Published var statusText: String = "Starting…"
    @Published var statusIsError: Bool = false
    @Published var isBusy: Bool = false
    @Published var hostVersion: String = ""
    @Published var alertMessage: String?
    @Published var isConnected: Bool = false

    private let hostProcess = HostProcess()
    private var client: HostClient?
    private var regenerateTask: Task<Void, Never>?
    private var started = false

    var canExport: Bool {
        guard let parts else { return false }
        return parts.triangleCount > 0 && !statusIsError
    }

    var windowTitle: String {
        let name = model.name.isEmpty ? "Untitled" : model.name
        return "3D Model Generator — \(name)"
    }

    var shapeType: ShapeTypeOption {
        get { ShapeTypeOption(rawValue: model.shapeType) ?? .circle }
        set {
            model.shapeType = newValue.rawValue
            scheduleRegenerate()
        }
    }

    func startIfNeeded() {
        guard !started else { return }
        started = true
        // Match WinForms: one blank text line on a fresh document.
        if model.textLines.isEmpty {
            model.textLines = [WireTextLine.blank(lineNumber: 0)]
        } else {
            normalizeTextLineFonts()
        }
        Task { await bootstrap() }
    }

    func addTextLine() {
        let line = WireTextLine.blank(lineNumber: model.textLines.count)
        model.textLines.append(line)
        renumberTextLines()
        scheduleRegenerate()
    }

    func removeTextLine(at index: Int) {
        guard model.textLines.indices.contains(index) else { return }
        model.textLines.remove(at: index)
        renumberTextLines()
        scheduleRegenerate()
    }

    func renumberTextLines() {
        for i in model.textLines.indices {
            model.textLines[i].lineNumber = i
            model.textLines[i].fontName = FontCatalog.resolve(model.textLines[i].fontName)
        }
    }

    private func normalizeTextLineFonts() {
        for i in model.textLines.indices {
            model.textLines[i].fontName = FontCatalog.resolve(model.textLines[i].fontName)
        }
    }

    func shutdown() {
        regenerateTask?.cancel()
        hostProcess.stop()
        client = nil
        isConnected = false
    }

    func scheduleRegenerate() {
        regenerateTask?.cancel()
        regenerateTask = Task { [weak self] in
            // Debounce slider/typing bursts (text content edits are chatty)
            try? await Task.sleep(nanoseconds: 120_000_000)
            guard let self, !Task.isCancelled else { return }
            await self.regenerate()
        }
    }

    func regenerate() async {
        guard let client else {
            statusText = "Host not connected"
            statusIsError = true
            return
        }

        isBusy = true
        defer { isBusy = false }

        // Resolve fonts & renumber before send so Core gets a consistent payload.
        renumberTextLines()

        do {
            let result = try await client.generateParts(model: model)
            parts = result
            let textCount = model.textLines.filter { !$0.content.isEmpty }.count
            let textNote = textCount > 0 ? " · \(textCount) text line\(textCount == 1 ? "" : "s")" : ""
            statusText = "\(result.vertexCount) vertices, \(result.triangleCount) triangles\(textNote)."
            statusIsError = false
        } catch {
            parts = nil
            statusText = error.localizedDescription
            statusIsError = true
        }
    }

    func exportSTL() {
        let panel = NSSavePanel()
        if let stlType = UTType(filenameExtension: "stl") {
            panel.allowedContentTypes = [stlType]
        }
        panel.nameFieldStringValue = (model.name.isEmpty ? "model" : model.name) + ".stl"
        panel.canCreateDirectories = true
        panel.title = "Export STL"

        guard panel.runModal() == .OK, let url = panel.url else { return }

        Task {
            await exportSTL(to: url)
        }
    }

    private func exportSTL(to url: URL) async {
        guard let client else {
            alertMessage = "Host not connected"
            return
        }
        isBusy = true
        defer { isBusy = false }
        do {
            let result = try await client.exportStl(model: model, path: url.path)
            statusText = "Exported \(result.triangleCount) triangles → \(url.lastPathComponent)"
            statusIsError = false
        } catch {
            alertMessage = error.localizedDescription
            statusText = error.localizedDescription
            statusIsError = true
        }
    }

    private func bootstrap() async {
        statusText = "Starting ModelGenerator.Host…"
        statusIsError = false
        do {
            try await hostProcess.ensureRunning()
            let c = HostClient(socketPath: hostProcess.socketPath)
            let ping = try await c.ping()
            client = c
            isConnected = true
            hostVersion = ping.version
            statusText = "Host \(ping.version) ready (protocol \(ping.protocolVersion))"
            await regenerate()
        } catch {
            isConnected = false
            statusText = error.localizedDescription
            statusIsError = true
            alertMessage = error.localizedDescription
        }
    }
}

// MARK: - Bindings helpers for inspector

extension AppModel {
    func bindingShapeSize() -> Binding<Double> {
        Binding(
            get: { Double(self.model.shapeSize) },
            set: { self.model.shapeSize = Float($0); self.scheduleRegenerate() }
        )
    }

    func bindingShapeHeight() -> Binding<Double> {
        Binding(
            get: { Double(self.model.shapeHeight) },
            set: { self.model.shapeHeight = Float($0); self.scheduleRegenerate() }
        )
    }

    func bindingThickness() -> Binding<Double> {
        Binding(
            get: { Double(self.model.shapeThickness) },
            set: { self.model.shapeThickness = Float($0); self.scheduleRegenerate() }
        )
    }

    func bindingBorderThickness() -> Binding<Double> {
        Binding(
            get: { Double(self.model.borderThickness) },
            set: { self.model.borderThickness = Float($0); self.scheduleRegenerate() }
        )
    }

    func bindingBorderHeight() -> Binding<Double> {
        Binding(
            get: { Double(self.model.borderHeight) },
            set: { self.model.borderHeight = Float($0); self.scheduleRegenerate() }
        )
    }

    func bindingBaseColor() -> Binding<Color> {
        Binding(
            get: { Color(argb: self.model.baseColorArgb) },
            set: { self.model.baseColorArgb = $0.argbInt; self.scheduleRegenerate() }
        )
    }

    func bindingBorderColor() -> Binding<Color> {
        Binding(
            get: { Color(argb: self.model.borderColorArgb) },
            set: { self.model.borderColorArgb = $0.argbInt; self.scheduleRegenerate() }
        )
    }
}

extension Color {
    init(argb: Int) {
        let a = Double((argb >> 24) & 0xFF) / 255.0
        let r = Double((argb >> 16) & 0xFF) / 255.0
        let g = Double((argb >> 8) & 0xFF) / 255.0
        let b = Double(argb & 0xFF) / 255.0
        self.init(.sRGB, red: r, green: g, blue: b, opacity: a == 0 ? 1 : a)
    }

    /// Packed ARGB as a signed 32-bit value (matches System.Drawing / C# `int` ToArgb).
    var argbInt: Int {
        let ns = NSColor(self)
        guard let rgb = ns.usingColorSpace(.sRGB) else { return -5_192_482 }
        let a = UInt32(round(rgb.alphaComponent * 255))
        let r = UInt32(round(rgb.redComponent * 255))
        let g = UInt32(round(rgb.greenComponent * 255))
        let b = UInt32(round(rgb.blueComponent * 255))
        let packed = (a << 24) | (r << 16) | (g << 8) | b
        return Int(Int32(bitPattern: packed))
    }
}
