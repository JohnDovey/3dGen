import AppKit
import Combine
import Foundation
import SwiftUI
import UniformTypeIdentifiers

@MainActor
final class AppModel: ObservableObject {
    @Published var model = WireModel.blankDocument()
    @Published var parts: GeneratePartsResult?
    @Published var statusText: String = "Starting…"
    @Published var statusIsError: Bool = false
    @Published var isBusy: Bool = false
    @Published var hostVersion: String = ""
    @Published var alertMessage: String?
    @Published var isConnected: Bool = false
    @Published var isDirty: Bool = false
    @Published var canUndo: Bool = false
    @Published var canRedo: Bool = false
    @Published var showOpenSheet: Bool = false
    @Published var showSaveNameSheet: Bool = false
    @Published var saveNameDraft: String = ""
    @Published var pendingDiscardAction: DiscardAction?
    @Published var modelSummaries: [ModelSummary] = []

    // SVG library UI
    @Published var showSvgLibrarySheet: Bool = false
    @Published var svgLibraryPurpose: SvgLibraryPurpose = .insert
    @Published var svgLibraryItems: [SvgLibraryItem] = []
    @Published var svgLibraryQuery: String = ""
    @Published var svgThumbnailCache: [String: Data] = [:]
    @Published var customShapeThumbnail: Data?

    enum SvgLibraryPurpose {
        case insert
        case customShape
    }

    enum DiscardAction {
        case newDocument
        case openDocument
        case quit
    }

    private let hostProcess = HostProcess()
    private var client: HostClient?
    private var regenerateTask: Task<Void, Never>?
    private var undoCommitTask: Task<Void, Never>?
    private var started = false
    private var isRestoringState = false
    private var undoBurstPending = false
    private var lastCommittedModel: WireModel?
    private let undoStack = SnapshotUndoStack()
    /// When true, next save uses a new name / id=0 (Save As).
    private var forceNewNameOnSave = false

    var canExport: Bool {
        guard let parts else { return false }
        return parts.triangleCount > 0 && !statusIsError
    }

    var windowTitle: String {
        let name = model.name.isEmpty ? "Untitled" : model.name
        let star = isDirty ? "*" : ""
        return "3D Model Generator — \(name)\(star)"
    }

    var shapeType: ShapeTypeOption {
        get { ShapeTypeOption(rawValue: model.shapeType) ?? .circle }
        set {
            noteEdit { self.model.shapeType = newValue.rawValue }
        }
    }

    func startIfNeeded() {
        guard !started else { return }
        started = true
        if model.textLines.isEmpty {
            model.textLines = [WireTextLine.blank(lineNumber: 0)]
        } else {
            normalizeTextLineFonts()
        }
        lastCommittedModel = model.deepCopy()
        Task { await bootstrap() }
    }

    func addTextLine() {
        noteEdit {
            let line = WireTextLine.blank(lineNumber: self.model.textLines.count)
            self.model.textLines.append(line)
            self.renumberTextLines()
        }
    }

    func removeTextLine(at index: Int) {
        guard model.textLines.indices.contains(index) else { return }
        noteEdit {
            self.model.textLines.remove(at: index)
            self.renumberTextLines()
        }
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

    // MARK: - SVG inserts

    func removeSvgInsert(at index: Int) {
        guard model.svgInserts.indices.contains(index) else { return }
        noteEdit {
            self.model.svgInserts.remove(at: index)
            self.renumberSvgInserts()
        }
    }

    func renumberSvgInserts() {
        for i in model.svgInserts.indices {
            model.svgInserts[i].lineNumber = i
        }
    }

    func openSvgLibrary(for purpose: SvgLibraryPurpose) {
        svgLibraryPurpose = purpose
        svgLibraryQuery = ""
        showSvgLibrarySheet = true
        Task { await refreshSvgLibrary() }
    }

    func refreshSvgLibrary() async {
        guard let client else { return }
        do {
            let result = try await client.listSvgFiles(query: svgLibraryQuery)
            svgLibraryItems = result.files
            // Prefetch missing thumbnails
            for item in result.files where svgThumbnailCache[item.fileName] == nil {
                Task {
                    await loadSvgThumbnail(fileName: item.fileName)
                }
            }
        } catch {
            alertMessage = error.localizedDescription
        }
    }

    func loadSvgThumbnail(fileName: String) async {
        guard let client else { return }
        do {
            let thumb = try await client.renderSvgThumbnail(fileName: fileName, width: 64, height: 64)
            svgThumbnailCache[fileName] = thumb.png
        } catch {
            // leave blank
        }
    }

    func importSvgFilesFromPanel() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.allowedContentTypes = [UTType(filenameExtension: "svg")].compactMap { $0 }
        panel.message = "Import SVG files into the library"
        guard panel.runModal() == .OK else { return }
        Task {
            guard let client else { return }
            for url in panel.urls {
                do {
                    _ = try await client.importSvgFile(path: url.path)
                } catch {
                    alertMessage = error.localizedDescription
                }
            }
            await refreshSvgLibrary()
        }
    }

    func deleteSvgLibraryFile(_ fileName: String) {
        Task {
            guard let client else { return }
            do {
                _ = try await client.deleteSvgFile(fileName: fileName)
                svgThumbnailCache.removeValue(forKey: fileName)
                await refreshSvgLibrary()
            } catch {
                alertMessage = error.localizedDescription
            }
        }
    }

    func setSvgLibraryTags(fileName: String, tagsCSV: String) {
        let keywords = tagsCSV
            .split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        Task {
            guard let client else { return }
            do {
                _ = try await client.setSvgKeywords(fileName: fileName, keywords: keywords)
                await refreshSvgLibrary()
            } catch {
                alertMessage = error.localizedDescription
            }
        }
    }

    func selectSvgFromLibrary(fileName: String) {
        Task {
            guard let client else { return }
            do {
                let content = try await client.readSvgContent(fileName: fileName)
                switch svgLibraryPurpose {
                case .insert:
                    noteEdit {
                        let insert = WireSvgInsert.fromLibrary(
                            fileName: fileName,
                            content: content.content,
                            lineNumber: self.model.svgInserts.count
                        )
                        self.model.svgInserts.append(insert)
                        self.renumberSvgInserts()
                    }
                case .customShape:
                    noteEdit {
                        self.model.shapeType = ShapeTypeOption.customSvg.rawValue
                        self.model.customShapeSvgContent = content.content
                        self.model.customShapeSourceFileName = fileName
                    }
                    await refreshCustomShapeThumbnail()
                }
                showSvgLibrarySheet = false
            } catch {
                alertMessage = error.localizedDescription
            }
        }
    }

    func refreshCustomShapeThumbnail() async {
        guard let content = model.customShapeSvgContent, !content.isEmpty, let client else {
            customShapeThumbnail = nil
            return
        }
        do {
            let thumb = try await client.renderSvgThumbnail(svgContent: content, width: 48, height: 48)
            customShapeThumbnail = thumb.png
        } catch {
            customShapeThumbnail = nil
        }
    }

    func clearCustomShape() {
        noteEdit {
            self.model.customShapeSvgContent = nil
            self.model.customShapeSourceFileName = nil
            if self.model.shapeType == ShapeTypeOption.customSvg.rawValue {
                self.model.shapeType = ShapeTypeOption.circle.rawValue
            }
        }
        customShapeThumbnail = nil
    }

    /// Renders an SVG content thumbnail for insert editors (nil on failure).
    func renderSvgContentThumbnail(_ content: String) async -> Data? {
        guard let client else { return nil }
        do {
            let thumb = try await client.renderSvgThumbnail(svgContent: content, width: 48, height: 48)
            return thumb.png
        } catch {
            return nil
        }
    }

    func shutdown() {
        regenerateTask?.cancel()
        undoCommitTask?.cancel()
        hostProcess.stop()
        client = nil
        isConnected = false
    }

    // MARK: - Edit / undo

    /// Call for any user edit that should be undoable and mark the document dirty.
    func noteEdit(_ mutate: () -> Void) {
        if !isRestoringState {
            if !undoBurstPending {
                undoBurstPending = true
                if let last = lastCommittedModel {
                    undoStack.record(last)
                    refreshUndoFlags()
                }
            }
            isDirty = true
            scheduleUndoCommit()
        }
        mutate()
        if !isRestoringState {
            scheduleRegenerate()
        }
    }

    private func scheduleUndoCommit() {
        undoCommitTask?.cancel()
        undoCommitTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 500_000_000)
            guard let self, !Task.isCancelled else { return }
            self.undoBurstPending = false
            self.lastCommittedModel = self.model.deepCopy()
        }
    }

    func undo() {
        guard let previous = undoStack.undo(current: model) else { return }
        restoreModel(previous, markDirty: true)
    }

    func redo() {
        guard let next = undoStack.redo(current: model) else { return }
        restoreModel(next, markDirty: true)
    }

    private func restoreModel(_ snapshot: WireModel, markDirty: Bool) {
        isRestoringState = true
        model = snapshot.deepCopy()
        normalizeTextLineFonts()
        isRestoringState = false
        undoBurstPending = false
        lastCommittedModel = model.deepCopy()
        isDirty = markDirty
        refreshUndoFlags()
        scheduleRegenerate()
    }

    private func refreshUndoFlags() {
        canUndo = undoStack.canUndo
        canRedo = undoStack.canRedo
    }

    func scheduleRegenerate() {
        regenerateTask?.cancel()
        regenerateTask = Task { [weak self] in
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

    // MARK: - File

    func requestNewDocument() {
        if isDirty {
            pendingDiscardAction = .newDocument
        } else {
            performNewDocument()
        }
    }

    func requestOpenDocument() {
        if isDirty {
            pendingDiscardAction = .openDocument
        } else {
            Task { await presentOpenSheet() }
        }
    }

    func confirmDiscardSave() {
        let action = pendingDiscardAction
        pendingDiscardAction = nil
        Task {
            let ok = await save(forceNewName: false)
            guard ok else { return }
            await continueAfterDiscard(action)
        }
    }

    func confirmDiscardDontSave() {
        let action = pendingDiscardAction
        pendingDiscardAction = nil
        Task { await continueAfterDiscard(action) }
    }

    func confirmDiscardCancel() {
        pendingDiscardAction = nil
    }

    private func continueAfterDiscard(_ action: DiscardAction?) async {
        switch action {
        case .newDocument:
            performNewDocument()
        case .openDocument:
            await presentOpenSheet()
        case .quit:
            MacAppSupport.allowTerminate = true
            NSApp.terminate(nil)
        case .none:
            break
        }
    }

    private func performNewDocument() {
        isRestoringState = true
        model = WireModel.blankDocument()
        isRestoringState = false
        undoStack.clear()
        undoBurstPending = false
        lastCommittedModel = model.deepCopy()
        isDirty = false
        refreshUndoFlags()
        scheduleRegenerate()
        statusText = "New model."
        statusIsError = false
    }

    private func presentOpenSheet() async {
        guard let client else {
            alertMessage = "Host not connected"
            return
        }
        do {
            let result = try await client.listModels()
            modelSummaries = result.models
            showOpenSheet = true
        } catch {
            alertMessage = error.localizedDescription
        }
    }

    func openModel(id: Int) {
        showOpenSheet = false
        Task { await loadModel(id: id) }
    }

    func deleteModelSummary(id: Int) {
        Task {
            guard let client else { return }
            do {
                _ = try await client.deleteModel(id: id)
                modelSummaries.removeAll { $0.id == id }
                statusText = "Deleted model #\(id)."
            } catch {
                alertMessage = error.localizedDescription
            }
        }
    }

    private func loadModel(id: Int) async {
        guard let client else { return }
        isBusy = true
        defer { isBusy = false }
        do {
            var loaded = try await client.getModel(id: id)
            for i in loaded.textLines.indices {
                loaded.textLines[i].fontName = FontCatalog.resolve(loaded.textLines[i].fontName)
                loaded.textLines[i].id = UUID()
            }
            for i in loaded.svgInserts.indices {
                loaded.svgInserts[i].id = UUID()
            }
            if loaded.textLines.isEmpty {
                loaded.textLines = [WireTextLine.blank(lineNumber: 0)]
            }

            isRestoringState = true
            model = loaded
            isRestoringState = false
            undoStack.clear()
            undoBurstPending = false
            lastCommittedModel = model.deepCopy()
            isDirty = false
            refreshUndoFlags()
            if model.shapeType == ShapeTypeOption.customSvg.rawValue {
                await refreshCustomShapeThumbnail()
            } else {
                customShapeThumbnail = nil
            }
            await regenerate()
            statusText = "Opened '\(model.name)'."
            statusIsError = false
        } catch {
            alertMessage = error.localizedDescription
        }
    }

    func requestSave() {
        if model.id == 0 || model.name.isEmpty || model.name == "Untitled" {
            forceNewNameOnSave = false
            saveNameDraft = model.name == "Untitled" ? "" : model.name
            showSaveNameSheet = true
        } else {
            Task { _ = await save(forceNewName: false) }
        }
    }

    func requestSaveAs() {
        forceNewNameOnSave = true
        saveNameDraft = model.name == "Untitled" ? "" : model.name
        showSaveNameSheet = true
    }

    func confirmSaveName() {
        let name = saveNameDraft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else {
            alertMessage = "Enter a model name."
            return
        }
        showSaveNameSheet = false
        model.name = name
        if forceNewNameOnSave {
            model.id = 0
        }
        Task { _ = await save(forceNewName: forceNewNameOnSave) }
    }

    func cancelSaveName() {
        showSaveNameSheet = false
    }

    @discardableResult
    func save(forceNewName: Bool) async -> Bool {
        guard let client else {
            alertMessage = "Host not connected"
            return false
        }

        if forceNewName {
            model.id = 0
        }

        if model.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            || model.name == "Untitled"
        {
            forceNewNameOnSave = forceNewName
            saveNameDraft = ""
            showSaveNameSheet = true
            return false
        }

        isBusy = true
        defer { isBusy = false }
        renumberTextLines()

        do {
            let result = try await client.saveModel(model, saveMesh: true)
            model.id = result.id
            model.name = result.name
            isDirty = false
            lastCommittedModel = model.deepCopy()
            undoBurstPending = false
            statusText = "Saved '\(result.name)'."
            statusIsError = false
            return true
        } catch {
            alertMessage = error.localizedDescription
            statusText = error.localizedDescription
            statusIsError = true
            return false
        }
    }

    /// Called from app delegate when user quits with dirty document.
    func requestQuitIfDirty() -> Bool {
        // return true = allow quit
        if !isDirty { return true }
        pendingDiscardAction = .quit
        return false
    }

    // MARK: - Export

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
            if model.shapeType == ShapeTypeOption.customSvg.rawValue {
                await refreshCustomShapeThumbnail()
            }
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
            set: { v in self.noteEdit { self.model.shapeSize = Float(v) } }
        )
    }

    func bindingShapeHeight() -> Binding<Double> {
        Binding(
            get: { Double(self.model.shapeHeight) },
            set: { v in self.noteEdit { self.model.shapeHeight = Float(v) } }
        )
    }

    func bindingThickness() -> Binding<Double> {
        Binding(
            get: { Double(self.model.shapeThickness) },
            set: { v in self.noteEdit { self.model.shapeThickness = Float(v) } }
        )
    }

    func bindingBorderThickness() -> Binding<Double> {
        Binding(
            get: { Double(self.model.borderThickness) },
            set: { v in self.noteEdit { self.model.borderThickness = Float(v) } }
        )
    }

    func bindingBorderHeight() -> Binding<Double> {
        Binding(
            get: { Double(self.model.borderHeight) },
            set: { v in self.noteEdit { self.model.borderHeight = Float(v) } }
        )
    }

    func bindingBaseColor() -> Binding<Color> {
        Binding(
            get: { Color(argb: self.model.baseColorArgb) },
            set: { c in self.noteEdit { self.model.baseColorArgb = c.argbInt } }
        )
    }

    func bindingBorderColor() -> Binding<Color> {
        Binding(
            get: { Color(argb: self.model.borderColorArgb) },
            set: { c in self.noteEdit { self.model.borderColorArgb = c.argbInt } }
        )
    }

    /// Binding used by text line editors — records undo + dirty + regen.
    func textLineBinding(at index: Int) -> Binding<WireTextLine> {
        Binding(
            get: {
                guard self.model.textLines.indices.contains(index) else {
                    return WireTextLine.blank()
                }
                return self.model.textLines[index]
            },
            set: { newValue in
                self.noteEdit {
                    guard self.model.textLines.indices.contains(index) else { return }
                    var line = newValue
                    line.lineNumber = index
                    line.fontName = FontCatalog.resolve(line.fontName)
                    line.fontSize = min(200, max(2, line.fontSize))
                    line.textHeight = min(50, max(0.2, line.textHeight))
                    self.model.textLines[index] = line
                }
            }
        )
    }

    func svgInsertBinding(at index: Int) -> Binding<WireSvgInsert> {
        Binding(
            get: {
                guard self.model.svgInserts.indices.contains(index) else {
                    return WireSvgInsert()
                }
                return self.model.svgInserts[index]
            },
            set: { newValue in
                self.noteEdit {
                    guard self.model.svgInserts.indices.contains(index) else { return }
                    var insert = newValue
                    insert.lineNumber = index
                    insert.scale = min(500, max(1, insert.scale))
                    insert.embossHeight = min(50, max(0.2, insert.embossHeight))
                    self.model.svgInserts[index] = insert
                }
            }
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

/// Bridges SwiftUI AppModel to NSApplication terminate handling.
@MainActor
final class MacAppSupport: NSObject, NSApplicationDelegate {
    weak var appModel: AppModel?
    /// Set true after user confirms discard/save so the second terminate attempt proceeds.
    static var allowTerminate = false

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if Self.allowTerminate { return .terminateNow }
        guard let appModel else { return .terminateNow }
        if appModel.requestQuitIfDirty() {
            return .terminateNow
        }
        return .terminateCancel
    }
}
