using ModelGenerator.Data.Repository;

namespace ModelGenerator.UI.Controls;

/// <summary>Lists saved models from the SQLite repository, letting the user open or delete one.</summary>
public class OpenModelDialog : Form
{
    private readonly IModelRepository _repository;
    private readonly ListView _listView;
    private readonly Button _openButton;
    private readonly Button _deleteButton;

    public int? SelectedModelId { get; private set; }

    public OpenModelDialog(IModelRepository repository)
    {
        _repository = repository;

        Text = "Open Model";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(520, 360);

        _listView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            Location = new Point(12, 12),
            Size = new Size(496, 300)
        };
        _listView.Columns.Add("Name", 220);
        _listView.Columns.Add("Shape", 100);
        _listView.Columns.Add("Modified", 160);

        _openButton = new Button { Text = "Open", DialogResult = DialogResult.OK, Location = new Point(273, 322), Width = 75, Enabled = false };
        _deleteButton = new Button { Text = "Delete", Location = new Point(354, 322), Width = 75, Enabled = false };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(435, 322), Width = 75 };

        Controls.Add(_listView);
        Controls.Add(_openButton);
        Controls.Add(_deleteButton);
        Controls.Add(cancelButton);

        AcceptButton = _openButton;
        CancelButton = cancelButton;

        _listView.SelectedIndexChanged += (_, _) =>
        {
            bool hasSelection = _listView.SelectedItems.Count > 0;
            _openButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
        };
        _listView.DoubleClick += (_, _) =>
        {
            if (_listView.SelectedItems.Count > 0)
            {
                _openButton.PerformClick();
            }
        };
        _openButton.Click += (_, _) => SelectedModelId = (int)_listView.SelectedItems[0].Tag!;
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync();

        Load += async (_, _) => await RefreshListAsync();
    }

    private async Task RefreshListAsync()
    {
        _listView.Items.Clear();
        var models = await _repository.ListModelsAsync();
        foreach (var model in models)
        {
            var item = new ListViewItem(model.Name) { Tag = model.Id };
            item.SubItems.Add(model.ShapeType.ToString());
            item.SubItems.Add(model.ModifiedDate.ToLocalTime().ToString("g"));
            _listView.Items.Add(item);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_listView.SelectedItems.Count == 0)
        {
            return;
        }

        var item = _listView.SelectedItems[0];
        var confirm = MessageBox.Show(this, $"Delete '{item.Text}'? This cannot be undone.", "Delete Model",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        await _repository.DeleteModelAsync((int)item.Tag!);
        await RefreshListAsync();
    }
}
