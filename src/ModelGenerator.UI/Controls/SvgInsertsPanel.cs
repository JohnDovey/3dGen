using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;

namespace ModelGenerator.UI.Controls;

/// <summary>Holds one SvgInsertEditorControl row per inserted SVG, plus an "Insert SVG..." button
/// that opens the library dialog — mirrors TextLinesPanel.</summary>
public class SvgInsertsPanel : UserControl
{
    private readonly ISvgLibraryService _svgLibrary;

    // Rows are positioned manually (see LayoutRows) rather than via Dock=Top — same reverse-add-
    // order reasoning documented in TextLinesPanel.
    private readonly Panel _rowsPanel = new() { Dock = DockStyle.Top, AutoSize = false };

    private readonly Button _addInsertButton = new() { Text = "+ Insert SVG...", AutoSize = true, Dock = DockStyle.Top };

    public event EventHandler? InsertsChanged;

    public SvgInsertsPanel(ISvgLibraryService svgLibrary)
    {
        _svgLibrary = svgLibrary;

        Controls.Add(_addInsertButton);
        Controls.Add(_rowsPanel);
        Dock = DockStyle.Top;
        AutoSize = true;

        _rowsPanel.Resize += (_, _) => LayoutRows();
        _addInsertButton.Click += (_, _) => AddInsertFromLibrary();
    }

    public List<SvgInsert> Inserts =>
        _rowsPanel.Controls.OfType<SvgInsertEditorControl>().Select(c => c.ToSvgInsert()).ToList();

    /// <summary>Replaces all rows with the given inserts, in LineNumber order.</summary>
    public void LoadInserts(IEnumerable<SvgInsert> inserts)
    {
        Clear();
        foreach (var insert in inserts.OrderBy(i => i.LineNumber))
        {
            var row = CreateRow();
            row.LoadFrom(_svgLibrary, insert);
            _rowsPanel.Controls.Add(row);
        }
        LayoutRows();
        InsertsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Switches the insert at insertIndex to Manual mode with the given absolute X/Y/Z
    /// (mm) — used when the user drags that insert in the 3D viewport.</summary>
    public void UpdateInsertPosition(int insertIndex, float x, float y, float z)
    {
        var row = _rowsPanel.Controls.OfType<SvgInsertEditorControl>().FirstOrDefault(r => r.LineNumber == insertIndex);
        row?.SetManualPosition(x, y, z);
    }

    public void Clear()
    {
        foreach (var row in _rowsPanel.Controls.OfType<SvgInsertEditorControl>().ToList())
        {
            _rowsPanel.Controls.Remove(row);
            row.Dispose();
        }
        LayoutRows();
    }

    private void AddInsertFromLibrary()
    {
        using var dialog = new SvgLibraryDialog(_svgLibrary);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.SelectedSvgContent is null)
        {
            return;
        }

        var row = CreateRow();
        row.SetSvgSource(_svgLibrary, dialog.SelectedFileName, dialog.SelectedSvgContent);
        _rowsPanel.Controls.Add(row);
        LayoutRows();
        InsertsChanged?.Invoke(this, EventArgs.Empty);
    }

    private SvgInsertEditorControl CreateRow()
    {
        var row = new SvgInsertEditorControl
        {
            LineNumber = _rowsPanel.Controls.Count,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        row.Changed += (_, _) => InsertsChanged?.Invoke(this, EventArgs.Empty);
        row.RemoveRequested += (_, _) => RemoveInsert(row);
        return row;
    }

    private void RemoveInsert(SvgInsertEditorControl row)
    {
        _rowsPanel.Controls.Remove(row);
        row.Dispose();
        RenumberInserts();
        LayoutRows();
        InsertsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenumberInserts()
    {
        int i = 0;
        foreach (var row in _rowsPanel.Controls.OfType<SvgInsertEditorControl>())
        {
            row.LineNumber = i++;
        }
    }

    private void LayoutRows()
    {
        int y = 0;
        foreach (var row in _rowsPanel.Controls.OfType<SvgInsertEditorControl>())
        {
            row.Width = _rowsPanel.ClientSize.Width;
            row.Location = new Point(0, y);
            y += row.Height;
        }
        _rowsPanel.Height = Math.Max(y, 1);
    }
}
