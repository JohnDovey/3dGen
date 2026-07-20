using ModelGenerator.Core.Services;

namespace ModelGenerator.UI.Controls;

/// <summary>Lets the user browse the SVG library (with thumbnails), import new files from disk,
/// and pick one to insert into the current model.</summary>
public class SvgLibraryDialog : Form
{
    private readonly ISvgLibraryService _svgLibrary;
    private readonly ListView _listView;
    private readonly ImageList _thumbnails = new() { ImageSize = new Size(64, 64), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Button _insertButton;

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
        ClientSize = new Size(520, 420);

        _listView = new ListView
        {
            View = View.LargeIcon,
            MultiSelect = false,
            LargeImageList = _thumbnails,
            Location = new Point(12, 12),
            Size = new Size(496, 350)
        };

        var importButton = new Button { Text = "Import SVG...", Location = new Point(12, 372), Width = 110 };
        _insertButton = new Button { Text = "Insert", DialogResult = DialogResult.OK, Location = new Point(354, 372), Width = 75, Enabled = false };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(435, 372), Width = 75 };

        Controls.Add(_listView);
        Controls.Add(importButton);
        Controls.Add(_insertButton);
        Controls.Add(cancelButton);

        AcceptButton = _insertButton;
        CancelButton = cancelButton;

        _listView.SelectedIndexChanged += (_, _) => _insertButton.Enabled = _listView.SelectedItems.Count > 0;
        _listView.DoubleClick += (_, _) =>
        {
            if (_listView.SelectedItems.Count > 0)
            {
                _insertButton.PerformClick();
            }
        };
        importButton.Click += (_, _) => ImportFiles();
        _insertButton.Click += (_, _) => SelectInsert();

        Load += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        _listView.Items.Clear();
        _thumbnails.Images.Clear();

        foreach (var fileName in _svgLibrary.ListSvgFiles())
        {
            var item = new ListViewItem(fileName) { Tag = fileName };
            try
            {
                string content = _svgLibrary.ReadSvgContent(fileName);
                using var bitmap = _svgLibrary.RenderThumbnail(content, 64, 64);
                _thumbnails.Images.Add(fileName, bitmap);
                item.ImageKey = fileName;
            }
            catch (Exception)
            {
                // A malformed library SVG must not crash the picker — it just shows with no
                // thumbnail image (still selectable/insertable, since the content itself may
                // still be valid enough for the converter, or the user can delete it manually).
            }
            _listView.Items.Add(item);
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
