using ModelGenerator.Core.Services;

namespace ModelGenerator.UI.Controls;

/// <summary>Lets the user browse the image library (with thumbnails), import new JPG/PNG files
/// from disk, and pick one to insert into the current model.</summary>
public class ImageLibraryDialog : Form
{
    private readonly IImageLibraryService _imageLibrary;
    private readonly ListView _listView;
    private readonly ImageList _thumbnails = new() { ImageSize = new Size(64, 64), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Button _insertButton;

    public string? SelectedFileName { get; private set; }
    public byte[]? SelectedImageData { get; private set; }

    public ImageLibraryDialog(IImageLibraryService imageLibrary)
    {
        _imageLibrary = imageLibrary;

        Text = "Image Library";
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

        var importButton = new Button { Text = "Import Image...", Location = new Point(12, 372), Width = 110 };
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

        foreach (var fileName in _imageLibrary.ListImageFiles())
        {
            var item = new ListViewItem(fileName) { Tag = fileName };
            try
            {
                byte[] data = _imageLibrary.ReadImageBytes(fileName);
                using var bitmap = _imageLibrary.RenderThumbnail(data, 64, 64);
                _thumbnails.Images.Add(fileName, bitmap);
                item.ImageKey = fileName;
            }
            catch (Exception)
            {
                // A corrupt library image must not crash the picker — it just shows with no
                // thumbnail image (still selectable, so the user can delete it manually).
            }
            _listView.Items.Add(item);
        }
    }

    private void ImportFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            _imageLibrary.ImportFile(path);
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
        SelectedImageData = _imageLibrary.ReadImageBytes(SelectedFileName);
    }
}
