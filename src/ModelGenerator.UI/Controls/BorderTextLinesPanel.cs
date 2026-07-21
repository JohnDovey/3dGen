using ModelGenerator.Core.Models;

namespace ModelGenerator.UI.Controls;

/// <summary>Holds one BorderTextLineEditorControl row per border-text line, plus Add button.</summary>
public class BorderTextLinesPanel : UserControl
{
    private readonly Panel _rowsPanel = new() { Dock = DockStyle.Top, AutoSize = false };
    private readonly Button _addLineButton = new() { Text = "+ Add Border Text", AutoSize = true, Dock = DockStyle.Top };

    public event EventHandler? LinesChanged;

    public BorderTextLinesPanel()
    {
        Controls.Add(_addLineButton);
        Controls.Add(_rowsPanel);
        Dock = DockStyle.Top;
        AutoSize = true;

        _rowsPanel.Resize += (_, _) => LayoutRows();
        _addLineButton.Click += (_, _) => AddLine();
    }

    public List<BorderTextLine> Lines =>
        _rowsPanel.Controls.OfType<BorderTextLineEditorControl>().Select(c => c.ToBorderTextLine()).ToList();

    public void AddLine() => AddLine(initial: null);

    public void LoadLines(IEnumerable<BorderTextLine> lines)
    {
        Clear();
        foreach (var line in lines.OrderBy(l => l.LineNumber))
        {
            AddLine(line);
        }
        LinesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        foreach (var row in _rowsPanel.Controls.OfType<BorderTextLineEditorControl>().ToList())
        {
            _rowsPanel.Controls.Remove(row);
            row.Dispose();
        }
        LayoutRows();
    }

    private void AddLine(BorderTextLine? initial)
    {
        var row = new BorderTextLineEditorControl
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

    private void RemoveLine(BorderTextLineEditorControl row)
    {
        _rowsPanel.Controls.Remove(row);
        row.Dispose();
        int i = 0;
        foreach (var r in _rowsPanel.Controls.OfType<BorderTextLineEditorControl>())
        {
            r.LineNumber = i++;
        }
        LayoutRows();
        LinesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LayoutRows()
    {
        int y = 0;
        int width = Math.Max(100, _rowsPanel.ClientSize.Width);
        foreach (var row in _rowsPanel.Controls.OfType<BorderTextLineEditorControl>())
        {
            row.Location = new Point(0, y);
            row.Width = width;
            y += row.Height + 4;
        }
        _rowsPanel.Height = Math.Max(y, 4);
    }
}
