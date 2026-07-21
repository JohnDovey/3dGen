using System.IO;
using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;
using ModelGenerator.Core.Services.ProjectBundle;
using ModelGenerator.Core.Utilities;
using ModelGenerator.Data.Repository;
using ModelGenerator.UI.Controls;
using CoreMesh = ModelGenerator.Core.Models.Mesh;

namespace ModelGenerator.UI;

public class MainForm : Form
{
    private readonly IModelOrchestrator _orchestrator;
    private readonly IModelRepository _repository;
    private readonly ISvgLibraryService _svgLibrary;
    private readonly IImageLibraryService _imageLibrary;
    private readonly IProjectBundleService _projectBundleService;

    private readonly ShapeSelectorControl _shapeSelector;
    private readonly TextLinesPanel _textLinesPanel = new();
    private readonly SvgInsertsPanel _svgInsertsPanel;
    private readonly ImageInsertsPanel _imageInsertsPanel;
    private readonly BorderTextLinesPanel _borderTextPanel = new();
    private readonly HelixViewportHost _viewportHost = new() { Dock = DockStyle.Fill };
    private readonly Button _exportButton = new() { Text = "Export STL...", AutoSize = true };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 40, Padding = new Padding(6) };

    private CoreMesh? _currentMesh;
    private int? _currentModelId;
    private string _currentModelName = "Untitled";

    // Undo/redo: full-Model snapshots rather than reversible commands, since edits already mutate
    // controls in place. Rapid-fire bursts (viewport drag mouse-moves, TextBox keystrokes) are
    // coalesced into a single undo step via a short idle debounce, so one Ctrl+Z undoes "that
    // drag" or "that word", not one mouse-move/keystroke at a time.
    private const int UndoDebounceMilliseconds = 500;
    private readonly UndoManager<Model> _undoManager = new();
    private readonly System.Windows.Forms.Timer _undoDebounceTimer = new() { Interval = UndoDebounceMilliseconds };
    private Model? _lastCommittedModel;
    private bool _isRestoringState;
    private bool _undoBurstPending;
    private ToolStripMenuItem _undoMenuItem = null!;
    private ToolStripMenuItem _redoMenuItem = null!;

    // Unsaved-changes tracking: true whenever the model differs from what's on disk, so New/
    // Open/close-the-window can offer to save first instead of silently discarding work.
    private bool _isDirty;
    private bool _isClosingConfirmed;
    private bool _closePromptPending;

    public MainForm(IModelOrchestrator orchestrator, IModelRepository repository, ISvgLibraryService svgLibrary, IImageLibraryService imageLibrary, IProjectBundleService projectBundleService)
    {
        _orchestrator = orchestrator;
        _repository = repository;
        _svgLibrary = svgLibrary;
        _imageLibrary = imageLibrary;
        _projectBundleService = projectBundleService;
        _shapeSelector = new ShapeSelectorControl(svgLibrary);
        _svgInsertsPanel = new SvgInsertsPanel(svgLibrary);
        _imageInsertsPanel = new ImageInsertsPanel(imageLibrary);

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
        var svgInsertsLabel = new Label
        {
            Text = "SVG inserts",
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(8, 12, 0, 4),
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };
        var imageInsertsLabel = new Label
        {
            Text = "Image inserts",
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
        var borderTextLabel = new Label
        {
            Text = "Border text",
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(8, 12, 0, 4),
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
        };

        leftPanel.Controls.Add(_exportButton);
        leftPanel.Controls.Add(_borderTextPanel);
        leftPanel.Controls.Add(borderTextLabel);
        leftPanel.Controls.Add(_imageInsertsPanel);
        leftPanel.Controls.Add(imageInsertsLabel);
        leftPanel.Controls.Add(_svgInsertsPanel);
        leftPanel.Controls.Add(svgInsertsLabel);
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

        _undoDebounceTimer.Tick += (_, _) =>
        {
            _undoDebounceTimer.Stop();
            _undoBurstPending = false;
            _lastCommittedModel = BuildModelFromControls();
        };

        _shapeSelector.ValuesChanged += (_, _) => OnEditableStateChanged();
        _textLinesPanel.LinesChanged += (_, _) => OnEditableStateChanged();
        _svgInsertsPanel.InsertsChanged += (_, _) => OnEditableStateChanged();
        _imageInsertsPanel.InsertsChanged += (_, _) => OnEditableStateChanged();
        _borderTextPanel.LinesChanged += (_, _) => OnEditableStateChanged();
        _exportButton.Click += (_, _) => ExportStl();
        _viewportHost.ItemDragged += (kind, index, x, y, z) =>
        {
            switch (kind)
            {
                case DraggableItemKind.TextLine:
                    _textLinesPanel.UpdateLinePosition(index, x, y, z);
                    break;
                case DraggableItemKind.SvgInsert:
                    _svgInsertsPanel.UpdateInsertPosition(index, x, y, z);
                    break;
                default:
                    _imageInsertsPanel.UpdateInsertPosition(index, x, y, z);
                    break;
            }
        };

        _textLinesPanel.AddLine();
        // The initial blank text line fires LinesChanged like any other edit would, but a
        // freshly launched "Untitled" document shouldn't already read as modified — clear the
        // dirty flag/undo burst state this one time, after it's had its say, rather than
        // wrapping the AddLine call itself in RunGuardedFromUndoTracking (which would also skip
        // seeding _lastCommittedModel further down).
        ResetUndoBurstState();
        _isDirty = false;
        UpdateTitle();
        RegeneratePreview();
        _lastCommittedModel = BuildModelFromControls();
        UpdateUndoRedoMenuState();
    }

    private MenuStrip BuildMenuStrip()
    {
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add("&New", null, async (_, _) => await NewModelAsync());
        fileMenu.DropDownItems.Add("&Open...", null, async (_, _) => await OpenModelAsync());
        fileMenu.DropDownItems.Add("&Save", null, async (_, _) => await SaveModelAsync(forceNewName: false));
        fileMenu.DropDownItems.Add("Save &As...", null, async (_, _) => await SaveModelAsync(forceNewName: true));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("&Export STL...", null, (_, _) => ExportStl());
        fileMenu.DropDownItems.Add("Export &Project...", null, (_, _) => ExportProject());
        fileMenu.DropDownItems.Add("Import Pro&ject...", null, async (_, _) => await ImportProjectAsync());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("E&xit", null, (_, _) => Close());

        var editMenu = new ToolStripMenuItem("&Edit");
        _undoMenuItem = new ToolStripMenuItem("&Undo", null, (_, _) => Undo())
        {
            ShortcutKeys = Keys.Control | Keys.Z,
            Enabled = false
        };
        _redoMenuItem = new ToolStripMenuItem("&Redo", null, (_, _) => Redo())
        {
            ShortcutKeys = Keys.Control | Keys.Y,
            Enabled = false
        };
        editMenu.DropDownItems.Add(_undoMenuItem);
        editMenu.DropDownItems.Add(_redoMenuItem);

        var helpMenu = new ToolStripMenuItem("&Help");
        helpMenu.DropDownItems.Add("&How to Use", null, (_, _) => ShowHelp());
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add("&About", null, (_, _) => ShowAbout());

        var menuStrip = new MenuStrip();
        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(editMenu);
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

    private async Task NewModelAsync()
    {
        if (!await ConfirmDiscardUnsavedChangesAsync())
        {
            return;
        }

        _currentModelId = null;
        _currentModelName = "Untitled";

        RunGuardedFromUndoTracking(() =>
        {
            _shapeSelector.LoadFrom(new Model());
            _textLinesPanel.Clear();
            _textLinesPanel.AddLine();
            _svgInsertsPanel.Clear();
            _imageInsertsPanel.Clear();
            _borderTextPanel.Clear();
        });

        // A different document has no relationship to the previous one's undo history.
        _undoManager.Clear();
        _isDirty = false;
        UpdateTitle();
        RegeneratePreview();
        ResetUndoBurstState();
        _lastCommittedModel = BuildModelFromControls();
        UpdateUndoRedoMenuState();
    }

    private async Task OpenModelAsync()
    {
        if (!await ConfirmDiscardUnsavedChangesAsync())
        {
            return;
        }

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

        RunGuardedFromUndoTracking(() =>
        {
            _shapeSelector.LoadFrom(model);
            _textLinesPanel.LoadLines(model.TextLines);
            _svgInsertsPanel.LoadInserts(model.SvgInserts);
            _imageInsertsPanel.LoadInserts(model.ImageInserts);
            _borderTextPanel.LoadLines(model.BorderTextLines);
        });

        // A different document has no relationship to the previous one's undo history.
        _undoManager.Clear();
        _isDirty = false;
        UpdateTitle();
        RegeneratePreview();
        ResetUndoBurstState();
        _lastCommittedModel = BuildModelFromControls();
        UpdateUndoRedoMenuState();
    }

    /// <summary>Prompts to save if there are unsaved changes, before a destructive action
    /// (New/Open/Import/closing the window) would otherwise silently discard them. Returns true if it's
    /// safe to proceed — either nothing was dirty, the user chose to discard, or the user chose
    /// to save and the save succeeded.</summary>
    private async Task<bool> ConfirmDiscardUnsavedChangesAsync()
    {
        if (!_isDirty)
        {
            return true;
        }

        // Use the form as owner so the dialog stays on top of the Helix ElementHost child.
        var result = MessageBox.Show(
            this,
            $"'{_currentModelName}' has unsaved changes.\n\nDo you want to save before continuing?",
            "Unsaved changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1);

        return result switch
        {
            DialogResult.Yes => await SaveModelAsync(forceNewName: false),
            DialogResult.No => true,
            _ => false
        };
    }

    /// <summary>
    /// WinForms does not reliably support <c>async void OnFormClosing</c>: after the first
    /// <c>await</c> the close sequence can finish without honouring Cancel, so Exit/window-X
    /// silently discarded dirty models. Cancel synchronously, then prompt on the next message
    /// pump tick and re-close only if the user confirms.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isClosingConfirmed || !_isDirty)
        {
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnFormClosing(e);

        // A modal MessageBox pumps its own nested message loop, so a rapid double
        // click/Alt+F4-repeat on the X button before the first prompt appears can re-enter
        // OnFormClosing and queue a second BeginInvoke before _isClosingConfirmed/_isDirty
        // change — without this guard that shows two stacked confirmation dialogs.
        if (_closePromptPending)
        {
            return;
        }
        _closePromptPending = true;

        // Defer the Yes/No/Cancel prompt until after FormClosing returns with Cancel=true.
        BeginInvoke(new Action(async () =>
        {
            try
            {
                if (!_isDirty || _isClosingConfirmed || IsDisposed)
                {
                    return;
                }

                if (await ConfirmDiscardUnsavedChangesAsync())
                {
                    _isClosingConfirmed = true;
                    Close();
                }
            }
            finally
            {
                _closePromptPending = false;
            }
        }));
    }

    private void Undo()
    {
        if (!_undoManager.CanUndo)
        {
            return;
        }

        var current = BuildModelFromControls();
        var previous = _undoManager.Undo(current);
        RestoreModelIntoControls(previous);
    }

    private void Redo()
    {
        if (!_undoManager.CanRedo)
        {
            return;
        }

        var current = BuildModelFromControls();
        var next = _undoManager.Redo(current);
        RestoreModelIntoControls(next);
    }

    /// <summary>Loads a model snapshot back into the controls for Undo/Redo — unlike New/Open,
    /// this doesn't touch _currentModelId/_currentModelName/title (undo/redo rewinds the content
    /// of the model you're editing, it doesn't change which saved model you're attached to) or
    /// the undo/redo stacks themselves.</summary>
    private void RestoreModelIntoControls(Model model)
    {
        RunGuardedFromUndoTracking(() =>
        {
            _shapeSelector.LoadFrom(model);
            _textLinesPanel.LoadLines(model.TextLines);
            _svgInsertsPanel.LoadInserts(model.SvgInserts);
            _imageInsertsPanel.LoadInserts(model.ImageInserts);
            _borderTextPanel.LoadLines(model.BorderTextLines);
        });

        MarkDirty();
        RegeneratePreview();
        ResetUndoBurstState();
        _lastCommittedModel = model;
        UpdateUndoRedoMenuState();
    }

    /// <summary>Runs a block of control-mutating code (New/Open/Undo/Redo, all of which call
    /// LoadFrom/LoadLines/LoadInserts on the panels) without those panels' own Changed/
    /// LinesChanged/InsertsChanged events being treated as user edits worth recording on the undo
    /// stack.</summary>
    private void RunGuardedFromUndoTracking(Action action)
    {
        _isRestoringState = true;
        try
        {
            action();
        }
        finally
        {
            _isRestoringState = false;
        }
    }

    private void ResetUndoBurstState()
    {
        _undoDebounceTimer.Stop();
        _undoBurstPending = false;
    }

    private void UpdateUndoRedoMenuState()
    {
        _undoMenuItem.Enabled = _undoManager.CanUndo;
        _redoMenuItem.Enabled = _undoManager.CanRedo;
    }

    /// <summary>Every control that edits the model routes its change event through here instead
    /// of calling RegeneratePreview directly — records an undo checkpoint for the FIRST change in
    /// a burst (see UndoDebounceMilliseconds), then always regenerates the preview.</summary>
    private void OnEditableStateChanged()
    {
        if (!_isRestoringState)
        {
            if (!_undoBurstPending)
            {
                _undoBurstPending = true;
                if (_lastCommittedModel is not null)
                {
                    _undoManager.RecordSnapshot(_lastCommittedModel);
                    UpdateUndoRedoMenuState();
                }
            }
            _undoDebounceTimer.Stop();
            _undoDebounceTimer.Start();

            MarkDirty();
        }

        RegeneratePreview();
    }

    /// <summary>Marks the document modified and refreshes title + status chrome.</summary>
    private void MarkDirty()
    {
        if (_isDirty)
        {
            // Still refresh title in case the name changed while already dirty.
            UpdateTitle();
            return;
        }

        _isDirty = true;
        UpdateTitle();
    }

    /// <summary>Returns true if the model was actually saved — false if the user cancelled the
    /// name prompt or the save threw, so callers (e.g. the close-confirmation flow) know whether
    /// it's safe to proceed with whatever they were about to do.</summary>
    private async Task<bool> SaveModelAsync(bool forceNewName)
    {
        string name = _currentModelName;
        if (forceNewName || _currentModelId is null)
        {
            using var dialog = new TextInputDialog("Save Model", "Model name:", name);
            if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.InputText))
            {
                return false;
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
            _isDirty = false;
            UpdateTitle();
            SetStatus($"Saved '{name}'.");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the model:\n{ex.Message}", "Save Model",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }


    private void ExportProject()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "3D Model Project (*.mgproj)|*.mgproj",
            FileName = string.IsNullOrWhiteSpace(_currentModelName) || _currentModelName == "Untitled"
                ? "project.mgproj"
                : _currentModelName + ".mgproj"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var model = BuildModelFromControls();
            model.Name = _currentModelName;
            _projectBundleService.ExportBundle(model, dialog.FileName, AppVersion);
            SetStatus($"Exported project to {Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not export the project:\n{ex.Message}", "Export Project",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ImportProjectAsync()
    {
        if (!await ConfirmDiscardUnsavedChangesAsync())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "3D Model Project (*.mgproj)|*.mgproj"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var model = _projectBundleService.ImportBundle(dialog.FileName);
            _currentModelId = null;
            _currentModelName = string.IsNullOrWhiteSpace(model.Name) ? "Untitled" : model.Name;

            RunGuardedFromUndoTracking(() =>
            {
                _shapeSelector.LoadFrom(model);
                _textLinesPanel.LoadLines(model.TextLines);
                _svgInsertsPanel.LoadInserts(model.SvgInserts);
                _imageInsertsPanel.LoadInserts(model.ImageInserts);
                _borderTextPanel.LoadLines(model.BorderTextLines);
            });

            _undoManager.Clear();
            _isDirty = false;
            UpdateTitle();
            RegeneratePreview();
            ResetUndoBurstState();
            _lastCommittedModel = BuildModelFromControls();
            UpdateUndoRedoMenuState();
            SetStatus($"Imported project '{_currentModelName}'.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not import the project:\n{ex.Message}", "Import Project",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Model BuildModelFromControls()
    {
        var model = new Model();
        _shapeSelector.ApplyTo(model);
        model.TextLines = _textLinesPanel.Lines;
        model.SvgInserts = _svgInsertsPanel.Inserts;
        model.ImageInserts = _imageInsertsPanel.Inserts;
        model.BorderTextLines = _borderTextPanel.Lines;
        return model;
    }

    private static string AppVersion => Application.ProductVersion;

    private void UpdateTitle()
    {
        // Trailing * is the standard dirty marker (also visible in the taskbar/alt-tab title).
        string dirtyMark = _isDirty ? " *" : "";
        Text = $"3D Model Generator v{AppVersion} — {_currentModelName}{dirtyMark}";
    }

    /// <summary>Appends an unsaved-changes hint to status messages so dirty state is obvious
    /// even if the title bar * is easy to miss.</summary>
    private void SetStatus(string message, bool isError = false)
    {
        string dirtyHint = _isDirty ? "  ·  Unsaved changes" : "";
        _statusLabel.Text = message + dirtyHint;
        _statusLabel.ForeColor = isError ? System.Drawing.Color.DarkRed : System.Drawing.Color.Black;
    }

    private void RegeneratePreview()
    {
        var model = BuildModelFromControls();

        try
        {
            var (floor, border, textMeshes, svgMeshes, imageMeshes, borderTextMeshes) = _orchestrator.GenerateModelParts(model);

            var merged = new CoreMesh();
            merged.Append(floor);
            merged.Append(border);
            foreach (var textMesh in textMeshes)
            {
                merged.Append(textMesh.Mesh);
            }
            foreach (var svgMesh in svgMeshes)
            {
                merged.Append(svgMesh.Mesh);
            }
            foreach (var imageMesh in imageMeshes)
            {
                merged.Append(imageMesh.Mesh);
            }
            foreach (var borderText in borderTextMeshes)
            {
                if (borderText.Mesh.Vertices.Count > 0)
                {
                    merged.Append(borderText.Mesh);
                }
            }
            _currentMesh = merged;

            var items = textMeshes
                .Select((t, i) => new DraggableMesh(t.Mesh, t.Line.ColorArgb.ToWpfColor(), DraggableItemKind.TextLine, i))
                .Concat(svgMeshes.Select((s, i) => new DraggableMesh(s.Mesh, s.Insert.ColorArgb.ToWpfColor(), DraggableItemKind.SvgInsert, i)))
                .Concat(imageMeshes.Select((im, i) => new DraggableMesh(im.Mesh, im.Insert.ColorArgb.ToWpfColor(), DraggableItemKind.ImageInsert, i)))
                .ToList();

            var borderTextColored = borderTextMeshes
                .Where(b => b.Mesh.Vertices.Count > 0)
                .Select(b => new ColoredMesh(b.Mesh, b.Line.ColorArgb.ToWpfColor()))
                .ToList();

            _viewportHost.ShowModel(
                new ColoredMesh(floor, model.BaseColorArgb.ToWpfColor()),
                new ColoredMesh(border, model.BorderColorArgb.ToWpfColor()),
                items,
                model.ShapeThickness,
                borderTextColored);
            SetStatus($"{_currentMesh.Vertices.Count} vertices, {_currentMesh.Indices.Count / 3} triangles.");
            _exportButton.Enabled = true;
        }
        catch (Exception ex)
        {
            // Broad catch is intentional: this runs on every keystroke/control change (a live
            // preview), and any failure here — an unimplemented shape, an invalid parameter
            // combination, a bad font — should surface as a status message, never crash the app.
            _currentMesh = null;
            _viewportHost.Clear();
            SetStatus(ex.Message, isError: true);
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
            SetStatus($"Exported to {dialog.FileName}");
        }
    }
}
