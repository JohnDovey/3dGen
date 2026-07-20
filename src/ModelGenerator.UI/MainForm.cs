using System.Windows.Media;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Data.Repository;
using ModelGenerator.UI.Controls;
using CoreMesh = ModelGenerator.Core.Models.Mesh;

namespace ModelGenerator.UI;

public class MainForm : Form
{
    private readonly IModelOrchestrator _orchestrator;
    private readonly IModelRepository _repository;

    private readonly ShapeSelectorControl _shapeSelector = new();
    private readonly TextLinesPanel _textLinesPanel = new();
    private readonly HelixViewportHost _viewportHost = new() { Dock = DockStyle.Fill };
    private readonly Button _exportButton = new() { Text = "Export STL...", AutoSize = true };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 40, Padding = new Padding(6) };

    private CoreMesh? _currentMesh;
    private int? _currentModelId;
    private string _currentModelName = "Untitled";

    public MainForm(IModelOrchestrator orchestrator, IModelRepository repository)
    {
        _orchestrator = orchestrator;
        _repository = repository;

        Width = 1200;
        Height = 800;

        var menuStrip = BuildMenuStrip();
        MainMenuStrip = menuStrip;

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 420, AutoScroll = true };

        var textLinesLabel = new Label
        {
            Text = "Text lines",
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(8, 12, 0, 4),
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };
        _exportButton.Dock = DockStyle.Top;
        _exportButton.Margin = new Padding(8);

        // A plain Panel stretches Dock=Top children to its full width (a FlowLayoutPanel does
        // NOT, which is why an earlier version of this left panel rendered all its inputs
        // collapsed to ~1px wide). For same-edge Dock siblings, the LAST one added ends up
        // closest to that edge, so add these in reverse of the desired top-to-bottom order.
        leftPanel.Controls.Add(_exportButton);
        leftPanel.Controls.Add(_textLinesPanel);
        leftPanel.Controls.Add(textLinesLabel);
        leftPanel.Controls.Add(_shapeSelector);

        // WinForms docks controls in reverse of add order — the LAST control added is processed
        // FIRST and claims its edge before earlier-added siblings get a turn. Fill must therefore
        // be added FIRST so every other Dock (Top/Left/Bottom) claims its space before Fill takes
        // whatever's left. Adding Fill last (as an earlier version of this code did) silently
        // gives it the full client rect, overlapping the other docked controls — the overlap
        // never caused a visible glitch (their own painting still looked right) but did throw
        // off the viewport's hit-testing coordinates, which is what surfaced this bug.
        Controls.Add(_viewportHost);
        Controls.Add(_statusLabel);
        Controls.Add(leftPanel);
        Controls.Add(menuStrip);

        _shapeSelector.ValuesChanged += (_, _) => RegeneratePreview();
        _textLinesPanel.LinesChanged += (_, _) => RegeneratePreview();
        _exportButton.Click += (_, _) => ExportStl();
        _viewportHost.TextLineDragged += (lineIndex, x, y, z) => _textLinesPanel.UpdateLinePosition(lineIndex, x, y, z);

        _textLinesPanel.AddLine();
        UpdateTitle();
        RegeneratePreview();
    }

    private MenuStrip BuildMenuStrip()
    {
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add("&New", null, (_, _) => NewModel());
        fileMenu.DropDownItems.Add("&Open...", null, async (_, _) => await OpenModelAsync());
        fileMenu.DropDownItems.Add("&Save", null, async (_, _) => await SaveModelAsync(forceNewName: false));
        fileMenu.DropDownItems.Add("Save &As...", null, async (_, _) => await SaveModelAsync(forceNewName: true));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("&Export STL...", null, (_, _) => ExportStl());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("E&xit", null, (_, _) => Close());

        var helpMenu = new ToolStripMenuItem("&Help");
        helpMenu.DropDownItems.Add("&How to Use", null, (_, _) => ShowHelp());
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add("&About", null, (_, _) => ShowAbout());

        var menuStrip = new MenuStrip();
        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(helpMenu);
        return menuStrip;
    }

    private HelpForm? _helpForm;

    private void ShowHelp()
    {
        if (_helpForm is { IsDisposed: false })
        {
            _helpForm.Activate();
            return;
        }

        _helpForm = new HelpForm();
        _helpForm.Show(this);
    }

    private void ShowAbout()
    {
        MessageBox.Show(this,
            "3D Model Generator\n\n" +
            "Creates 3D-printable models from basic shapes (circle, triangle, shield, " +
            "rectangle) with embossed, multi-line custom text. Preview the model in 3D and " +
            "export it to STL for printing.\n\n" +
            $"Version {AppVersion}\n" +
            "Copyright © John Dovey <dovey.john@gmail.com>",
            "About 3D Model Generator",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void NewModel()
    {
        _currentModelId = null;
        _currentModelName = "Untitled";
        _shapeSelector.LoadFrom(new Model());
        _textLinesPanel.Clear();
        _textLinesPanel.AddLine();
        UpdateTitle();
        RegeneratePreview();
    }

    private async Task OpenModelAsync()
    {
        using var dialog = new OpenModelDialog(_repository);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedModelId is not int id)
        {
            return;
        }

        var model = await _repository.GetModelByIdAsync(id);
        if (model is null)
        {
            MessageBox.Show(this, "That model could not be found — it may have been deleted.", "Open Model",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _currentModelId = model.Id;
        _currentModelName = model.Name;
        _shapeSelector.LoadFrom(model);
        _textLinesPanel.LoadLines(model.TextLines);
        UpdateTitle();
        RegeneratePreview();
    }

    private async Task SaveModelAsync(bool forceNewName)
    {
        string name = _currentModelName;
        if (forceNewName || _currentModelId is null)
        {
            using var dialog = new TextInputDialog("Save Model", "Model name:", name);
            if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.InputText))
            {
                return;
            }
            name = dialog.InputText.Trim();
        }

        var model = BuildModelFromControls();
        model.Name = name;
        model.Id = forceNewName ? 0 : _currentModelId ?? 0;

        try
        {
            int id = await _repository.SaveModelAsync(model);
            if (_currentMesh is not null)
            {
                await _repository.SaveMeshAsync(id, _currentMesh);
            }

            _currentModelId = id;
            _currentModelName = name;
            UpdateTitle();
            _statusLabel.Text = $"Saved '{name}'.";
            _statusLabel.ForeColor = System.Drawing.Color.Black;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the model:\n{ex.Message}", "Save Model",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Model BuildModelFromControls()
    {
        var model = new Model();
        _shapeSelector.ApplyTo(model);
        model.TextLines = _textLinesPanel.Lines;
        return model;
    }

    private static string AppVersion => Application.ProductVersion;

    private void UpdateTitle() => Text = $"3D Model Generator v{AppVersion} — {_currentModelName}";

    private void RegeneratePreview()
    {
        var model = BuildModelFromControls();

        try
        {
            var (baseMesh, textMeshes) = _orchestrator.GenerateModelParts(model);

            var merged = new CoreMesh();
            merged.Append(baseMesh);
            foreach (var textMesh in textMeshes)
            {
                merged.Append(textMesh.Mesh);
            }
            _currentMesh = merged;

            _viewportHost.ShowModel(
                baseMesh,
                textMeshes.Select(t => t.Mesh).ToList(),
                Colors.LightSteelBlue,
                Colors.DarkOrange,
                model.ShapeThickness);
            _statusLabel.Text = $"{_currentMesh.Vertices.Count} vertices, {_currentMesh.Indices.Count / 3} triangles.";
            _statusLabel.ForeColor = System.Drawing.Color.Black;
            _exportButton.Enabled = true;
        }
        catch (Exception ex)
        {
            // Broad catch is intentional: this runs on every keystroke/control change (a live
            // preview), and any failure here — an unimplemented shape, an invalid parameter
            // combination, a bad font — should surface as a status message, never crash the app.
            _currentMesh = null;
            _viewportHost.Clear();
            _statusLabel.Text = ex.Message;
            _statusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _exportButton.Enabled = false;
        }
    }

    private void ExportStl()
    {
        if (_currentMesh is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "STL files (*.stl)|*.stl", FileName = "model.stl" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _orchestrator.ExportSTL(_currentMesh, dialog.FileName);
            _statusLabel.Text = $"Exported to {dialog.FileName}";
        }
    }
}
