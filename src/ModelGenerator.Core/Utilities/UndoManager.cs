namespace ModelGenerator.Core.Utilities;

/// <summary>Generic undo/redo stack over full-state snapshots, not command objects — the simplest
/// design for a UI where individual field edits already mutate controls in place rather than
/// going through explicit reversible commands. The caller owns capturing/restoring a snapshot;
/// this class only owns the stack mechanics. Capped at maxEntries to bound memory for a long
/// editing session (each snapshot can be a full document, e.g. including inserted image bytes).</summary>
public class UndoManager<T>
{
    private const int DefaultMaxEntries = 50;

    private readonly Stack<T> _undoStack = new();
    private readonly Stack<T> _redoStack = new();
    private readonly int _maxEntries;

    public UndoManager(int maxEntries = DefaultMaxEntries)
    {
        _maxEntries = maxEntries;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Records previousState as an undo checkpoint and clears the redo stack — a new
    /// edit invalidates any previously-undone future.</summary>
    public void RecordSnapshot(T previousState)
    {
        _undoStack.Push(previousState);
        if (_undoStack.Count > _maxEntries)
        {
            TrimOldest(_undoStack);
        }
        _redoStack.Clear();
    }

    /// <summary>Pops the most recent undo checkpoint, pushes currentState onto the redo stack so
    /// it can be redone, and returns the checkpoint to restore. Throws if CanUndo is false.</summary>
    public T Undo(T currentState)
    {
        var previous = _undoStack.Pop();
        _redoStack.Push(currentState);
        return previous;
    }

    /// <summary>Pops the most recent redo checkpoint, pushes currentState back onto the undo
    /// stack, and returns the checkpoint to restore. Throws if CanRedo is false.</summary>
    public T Redo(T currentState)
    {
        var next = _redoStack.Pop();
        _undoStack.Push(currentState);
        return next;
    }

    /// <summary>Discards all history — used when switching to a different document, where the
    /// previous document's undo history no longer applies.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    /// <summary>Stack&lt;T&gt; only pops/pushes from the top, so dropping the oldest (bottom)
    /// entry means rebuilding the stack without it — cheap relative to how rarely the cap is hit.</summary>
    private static void TrimOldest(Stack<T> stack)
    {
        var newestFirst = stack.ToArray();
        stack.Clear();
        for (int i = newestFirst.Length - 2; i >= 0; i--)
        {
            stack.Push(newestFirst[i]);
        }
    }
}
