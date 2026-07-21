import Foundation

/// Snapshot undo/redo stack (matches Core `UndoManager<T>` — full document copies, not commands).
@MainActor
final class SnapshotUndoStack {
    private var undoStack: [WireModel] = []
    private var redoStack: [WireModel] = []
    private let maxEntries: Int

    init(maxEntries: Int = 50) {
        self.maxEntries = maxEntries
    }

    var canUndo: Bool { !undoStack.isEmpty }
    var canRedo: Bool { !redoStack.isEmpty }

    func record(_ previous: WireModel) {
        undoStack.append(previous.deepCopy())
        if undoStack.count > maxEntries {
            undoStack.removeFirst(undoStack.count - maxEntries)
        }
        redoStack.removeAll()
    }

    func undo(current: WireModel) -> WireModel? {
        guard let previous = undoStack.popLast() else { return nil }
        redoStack.append(current.deepCopy())
        return previous
    }

    func redo(current: WireModel) -> WireModel? {
        guard let next = redoStack.popLast() else { return nil }
        undoStack.append(current.deepCopy())
        return next
    }

    func clear() {
        undoStack.removeAll()
        redoStack.removeAll()
    }
}
