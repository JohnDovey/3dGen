using ModelGenerator.Core.Utilities;
using Xunit;

namespace ModelGenerator.Tests;

public class UndoManagerTests
{
    [Fact]
    public void NewManager_CannotUndoOrRedo()
    {
        var manager = new UndoManager<string>();

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void RecordSnapshot_ThenUndo_ReturnsThePreviousState()
    {
        var manager = new UndoManager<string>();

        manager.RecordSnapshot("v1");
        string restored = manager.Undo("v2");

        Assert.Equal("v1", restored);
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);
    }

    [Fact]
    public void Undo_ThenRedo_ReturnsTheStateThatWasUndone()
    {
        var manager = new UndoManager<string>();

        manager.RecordSnapshot("v1");
        string undone = manager.Undo("v2");
        string redone = manager.Redo(undone);

        Assert.Equal("v2", redone);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void MultipleUndos_UnwindInReverseOrder()
    {
        var manager = new UndoManager<string>();

        manager.RecordSnapshot("v1");
        manager.RecordSnapshot("v2");
        manager.RecordSnapshot("v3");

        Assert.Equal("v3", manager.Undo("v4"));
        Assert.Equal("v2", manager.Undo("v3"));
        Assert.Equal("v1", manager.Undo("v2"));
        Assert.False(manager.CanUndo);
    }

    [Fact]
    public void RecordSnapshot_AfterUndo_ClearsRedoStack()
    {
        var manager = new UndoManager<string>();

        manager.RecordSnapshot("v1");
        manager.Undo("v2");
        Assert.True(manager.CanRedo);

        manager.RecordSnapshot("v1-again");

        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Clear_DiscardsBothStacks()
    {
        var manager = new UndoManager<string>();

        manager.RecordSnapshot("v1");
        manager.Undo("v2");
        Assert.True(manager.CanRedo);

        manager.Clear();

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void RecordSnapshot_BeyondMaxEntries_DropsTheOldestEntry()
    {
        var manager = new UndoManager<int>(maxEntries: 3);

        manager.RecordSnapshot(1);
        manager.RecordSnapshot(2);
        manager.RecordSnapshot(3);
        manager.RecordSnapshot(4); // should evict "1"

        Assert.Equal(4, manager.Undo(5));
        Assert.Equal(3, manager.Undo(4));
        Assert.Equal(2, manager.Undo(3));
        Assert.False(manager.CanUndo); // "1" was evicted, not just these three
    }
}
