using ModelGenerator.Core.Models;

namespace ModelGenerator.UI.Controls;

/// <summary>Holds one TextLineEditorControl row per line of embossed text, plus an "Add line" button.</summary>
public class TextLinesPanel : UserControl
{
    // Rows are positioned manually (see LayoutRows) rather than via Dock=Top: for same-edge Dock
    // siblings, the LAST control added ends up closest to that edge, which would put each newly
    // added row above the existing ones instead of appending below them.
    private readonly Panel _rowsPanel = new() { Dock = DockStyle.Top, AutoSize = false };

    private readonly Button _addLineButton = new() { Text = "+ Add Text Line", AutoSize = true, Dock = DockStyle.Top };

    public event EventHandler? LinesChanged;

    public TextLinesPanel()
    {
        // Last-added-is-topmost applies here too (only two, static, children): add the button
        // first so _rowsPanel ends up above it.
        Controls.Add(_addLineButton);
        Controls.Add(_rowsPanel);
        Dock = DockStyle.Top;
        AutoSize = true;

        _rowsPanel.Resize += (_, _) => LayoutRows();
        _addLineButton.Click += (_, _) => AddLine();
    }

    public List<TextLine> Lines =>
        _rowsPanel.Controls.OfType<TextLineEditorControl>().Select(c => c.ToTextLine()).ToList();

    public void AddLine() => AddLine(initial: null);

    /// <summary>Replaces all rows with the given lines, in LineNumber order.</summary>
    public void LoadLines(IEnumerable<TextLine> lines)
    {
        Clear();
        foreach (var line in lines.OrderBy(l => l.LineNumber))
        {
            AddLine(line);
        }
        LinesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Switches the line at lineIndex to Manual mode with the given absolute X/Y/Z (mm)
    /// — used when the user drags that line's text in the 3D viewport.</summary>
    public void UpdateLinePosition(int lineIndex, float x, float y, float z)
    {
        var row = _rowsPanel.Controls.OfType<TextLineEditorControl>().FirstOrDefault(r => r.LineNumber == lineIndex);
        row?.SetManualPosition(x, y, z);
    }

    public void Clear()
    {
        foreach (var row in _rowsPanel.Controls.OfType<TextLineEditorControl>().ToList())
        {
            _rowsPanel.Controls.Remove(row);
            row.Dispose();
        }
        LayoutRows();
    }

    private void AddLine(TextLine? initial)
    {
        var row = new TextLineEditorControl
        {
            LineNumber = _rowsPanel.Controls.Count,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        if (initial is not null)
        {
            row.LoadFrom(initial);
        }
        row.Changed += (_, _) => LinesChanged?.Invoke(this, EventArgs.Empty);
        row.RemoveRequested += (_, _) => RemoveLine(row);
        _rowsPanel.Controls.Add(row);
        LayoutRows();
        LinesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveLine(TextLineEditorControl row)
    {
        _rowsPanel.Controls.Remove(row);
        row.Dispose();
        RenumberLines();
        LayoutRows();
        LinesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenumberLines()
    {
        int i = 0;
        foreach (var row in _rowsPanel.Controls.OfType<TextLineEditorControl>())
        {
            row.LineNumber = i++;
        }
    }

    private void LayoutRows()
    {
        int y = 0;
        foreach (var row in _rowsPanel.Controls.OfType<TextLineEditorControl>())
        {
            row.Width = _rowsPanel.ClientSize.Width;
            row.Location = new Point(0, y);
            y += row.Height;
        }
        _rowsPanel.Height = Math.Max(y, 1);
    }
}
