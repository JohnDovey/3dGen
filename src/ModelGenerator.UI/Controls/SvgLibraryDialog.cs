using ModelGenerator.Core.Services;

namespace ModelGenerator.UI.Controls;

/// <summary>Lets the user browse the SVG library (with thumbnails), search/filter by name or
/// keyword, import new files from disk, tag or delete existing ones, and pick one to insert into
/// the current model.</summary>
public class SvgLibraryDialog : Form
{
    private readonly ISvgLibraryService _svgLibrary;
    private readonly TextBox _searchBox;
    private readonly ListView _listView;
    private readonly ImageList _thumbnails = new() { ImageSize = new Size(64, 64), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Button _insertButton;
    private readonly Button _deleteButton;
    private readonly Button _tagsButton;

    public string? SelectedFileName { get; private set; }
    public string? SelectedSvgContent { get; private set; }

    public SvgLibraryDialog(ISvgLibraryService svgLibrary)
    {
        _svgLibrary = svgLibrary;

        Text = "SVG Library";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(520, 450);

        var searchLabel = new Label { Text = "Search:", Location = new Point(12, 15), AutoSize = true };
        _searchBox = new TextBox { Location = new Point(70, 12), Width = 438 };

        _listView = new ListView
        {
            View = View.LargeIcon,
            MultiSelect = false,
            LargeImageList = _thumbnails,
            Location = new Point(12, 40),
            Size = new Size(496, 322),
            ShowItemToolTips = true
        };

        var importButton = new Button { Text = "Import SVG...", Location = new Point(12, 372), Width = 100 };
        _deleteButton = new Button { Text = "Delete", Location = new Point(118, 372), Width = 75, Enabled = false };
        _tagsButton = new Button { Text = "Tags...", Location = new Point(199, 372), Width = 75, Enabled = false };
        _insertButton = new Button { Text = "Insert", DialogResult = DialogResult.OK, Location = new Point(354, 372), Width = 75, Enabled = false };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(435, 372), Width = 75 };

        Controls.Add(searchLabel);
        Controls.Add(_searchBox);
        Controls.Add(_listView);
        Controls.Add(importButton);
        Controls.Add(_deleteButton);
        Controls.Add(_tagsButton);
        Controls.Add(_insertButton);
        Controls.Add(cancelButton);

        AcceptButton = _insertButton;
        CancelButton = cancelButton;

        _listView.SelectedIndexChanged += (_, _) =>
        {
            bool hasSelection = _listView.SelectedItems.Count > 0;
            _insertButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
            _tagsButton.Enabled = hasSelection;
        };
        _listView.DoubleClick += (_, _) =>
        {
            if (_listView.SelectedItems.Count > 0)
            {
                _insertButton.PerformClick();
            }
        };
        importButton.Click += (_, _) => ImportFiles();
        _deleteButton.Click += (_, _) => DeleteSelected();
        _tagsButton.Click += (_, _) => EditTagsForSelected();
        _insertButton.Click += (_, _) => SelectInsert();
        _searchBox.TextChanged += (_, _) => ApplyFilter();

        Load += (_, _) => RefreshList();
    }

    /// <summary>Re-renders every thumbnail from disk — needed after import/delete, since the set
    /// of files on disk actually changed. ApplyFilter() alone (used for search-as-you-type)
    /// reuses these already-rendered thumbnails instead of re-rendering on every keystroke.</summary>
    private void RefreshList()
    {
        _thumbnails.Images.Clear();

        foreach (var fileName in _svgLibrary.ListSvgFiles())
        {
            try
            {
                string content = _svgLibrary.ReadSvgContent(fileName);
                using var bitmap = PngThumbnail.TryDecode(_svgLibrary.RenderThumbnail(content, 64, 64));
                if (bitmap is not null)
                {
                    _thumbnails.Images.Add(fileName, bitmap);
                }
            }
            catch (Exception)
            {
                // A malformed library SVG must not crash the picker — it just shows with no
                // thumbnail image (still selectable/insertable, since the content itself may
                // still be valid enough for the converter, or the user can delete it manually).
            }
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string previouslySelected = _listView.SelectedItems.Count > 0 ? (string)_listView.SelectedItems[0].Tag! : "";

        _listView.Items.Clear();
        foreach (var fileName in _svgLibrary.SearchFiles(_searchBox.Text))
        {
            var item = new ListViewItem(fileName) { Tag = fileName };
            if (_thumbnails.Images.ContainsKey(fileName))
            {
                item.ImageKey = fileName;
            }
            var keywords = _svgLibrary.GetKeywords(fileName);
            if (keywords.Count > 0)
            {
                item.ToolTipText = string.Join(", ", keywords);
            }
            _listView.Items.Add(item);
            if (fileName == previouslySelected)
            {
                item.Selected = true;
            }
        }
    }

    private void ImportFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "SVG files (*.svg)|*.svg",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            _svgLibrary.ImportFile(path);
        }
        RefreshList();
    }

    private void DeleteSelected()
    {
        if (_listView.SelectedItems.Count == 0)
        {
            return;
        }

        string fileName = (string)_listView.SelectedItems[0].Tag!;
        var confirm = MessageBox.Show(this, $"Delete '{fileName}' from the library? This cannot be undone.",
            "Delete SVG", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _svgLibrary.DeleteFile(fileName);
        RefreshList();
    }

    private void EditTagsForSelected()
    {
        if (_listView.SelectedItems.Count == 0)
        {
            return;
        }

        string fileName = (string)_listView.SelectedItems[0].Tag!;
        string currentTags = string.Join(", ", _svgLibrary.GetKeywords(fileName));
        using var dialog = new TextInputDialog("Edit Tags", $"Tags for '{fileName}' (comma-separated):", currentTags);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var keywords = dialog.InputText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        _svgLibrary.SetKeywords(fileName, keywords);
        ApplyFilter();
    }

    private void SelectInsert()
    {
        if (_listView.SelectedItems.Count == 0)
        {
            return;
        }

        SelectedFileName = (string)_listView.SelectedItems[0].Tag!;
        SelectedSvgContent = _svgLibrary.ReadSvgContent(SelectedFileName);
    }
}
