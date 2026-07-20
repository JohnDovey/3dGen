using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;

namespace ModelGenerator.UI.Controls;

/// <summary>One row of the SVG inserts editor: thumbnail, scale, emboss height, color, and
/// position mode with its associated X/Y/Z/rotation fields — mirrors TextLineEditorControl.</summary>
public class SvgInsertEditorControl : UserControl
{
    private readonly PictureBox _thumbnail = new() { Width = 48, Height = 48, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    private readonly NumericUpDown _scaleInput = MakeNumeric(40, min: 1, max: 500);
    private readonly NumericUpDown _embossHeightInput = MakeNumeric(5, min: 0.2m, max: 50);
    private readonly Button _colorButton = new() { Text = "Color", Width = 50, AutoSize = false };
    private readonly ComboBox _positionModeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
    private readonly NumericUpDown _positionXInput = MakeNumeric(0, min: -500, max: 500);
    private readonly NumericUpDown _positionYInput = MakeNumeric(0, min: -500, max: 500);
    private readonly NumericUpDown _positionZInput = MakeNumeric(0, min: -500, max: 500);
    private readonly NumericUpDown _rotationZInput = MakeNumeric(0, min: -360, max: 360);
    private readonly Button _removeButton = new() { Text = "Remove", AutoSize = true };

    private string? _sourceFileName;
    private string _svgContent = string.Empty;
    private int _colorArgb = Color.DarkOrange.ToArgb();

    public event EventHandler? Changed;
    public event EventHandler? RemoveRequested;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.Browsable(false)]
    public int LineNumber { get; set; }

    public SvgInsertEditorControl()
    {
        _positionModeCombo.Items.AddRange(Enum.GetNames<TextPositionMode>());
        _positionModeCombo.SelectedIndex = 0;
        UpdateColorButtonSwatch();

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            WrapContents = true,
            Padding = new Padding(4)
        };

        layout.Controls.Add(_thumbnail);
        layout.Controls.Add(Labeled("Scale (mm)", _scaleInput));
        layout.Controls.Add(Labeled("Emboss (mm)", _embossHeightInput));
        layout.Controls.Add(Labeled("Color", _colorButton));
        layout.Controls.Add(Labeled("Position", _positionModeCombo));
        layout.Controls.Add(Labeled("X", _positionXInput));
        layout.Controls.Add(Labeled("Y", _positionYInput));
        layout.Controls.Add(Labeled("Z", _positionZInput));
        layout.Controls.Add(Labeled("Rot°", _rotationZInput));
        layout.Controls.Add(_removeButton);

        Controls.Add(layout);
        // No Dock here: SvgInsertsPanel positions rows manually (Location/Width), same reason as
        // TextLineEditorControl.
        AutoSize = true;
        BorderStyle = BorderStyle.FixedSingle;

        _scaleInput.ValueChanged += (_, _) => RaiseChanged();
        _embossHeightInput.ValueChanged += (_, _) => RaiseChanged();
        _colorButton.Click += (_, _) => PickColor();
        _positionModeCombo.SelectedIndexChanged += (_, _) => { UpdatePositionFieldsState(); RaiseChanged(); };
        _positionXInput.ValueChanged += (_, _) => RaiseChanged();
        _positionYInput.ValueChanged += (_, _) => RaiseChanged();
        _positionZInput.ValueChanged += (_, _) => RaiseChanged();
        _rotationZInput.ValueChanged += (_, _) => RaiseChanged();
        _removeButton.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);

        UpdatePositionFieldsState();
    }

    /// <summary>Sets the SVG content/source and re-renders the thumbnail — called right after
    /// construction when this row is created from a library selection.</summary>
    public void SetSvgSource(ISvgLibraryService svgLibrary, string? sourceFileName, string svgContent)
    {
        _sourceFileName = sourceFileName;
        _svgContent = svgContent;
        RenderThumbnail(svgLibrary, svgContent);
        RaiseChanged();
    }

    public SvgInsert ToSvgInsert() => new()
    {
        LineNumber = LineNumber,
        SourceFileName = _sourceFileName,
        SvgContent = _svgContent,
        Scale = (float)_scaleInput.Value,
        EmbossHeight = (float)_embossHeightInput.Value,
        ColorArgb = _colorArgb,
        PositionMode = Enum.Parse<TextPositionMode>((string)_positionModeCombo.SelectedItem!),
        PositionX = (float)_positionXInput.Value,
        PositionY = (float)_positionYInput.Value,
        PositionZ = (float)_positionZInput.Value,
        RotationZ = (float)_rotationZInput.Value
    };

    /// <summary>Populates the row's controls from a persisted SvgInsert (inverse of ToSvgInsert).</summary>
    public void LoadFrom(ISvgLibraryService svgLibrary, SvgInsert insert)
    {
        _sourceFileName = insert.SourceFileName;
        _svgContent = insert.SvgContent;
        _colorArgb = insert.ColorArgb;
        UpdateColorButtonSwatch();
        RenderThumbnail(svgLibrary, insert.SvgContent);

        _scaleInput.Value = ClampToRange(_scaleInput, (decimal)insert.Scale);
        _embossHeightInput.Value = ClampToRange(_embossHeightInput, (decimal)insert.EmbossHeight);
        _positionModeCombo.SelectedIndex = (int)insert.PositionMode;
        _positionXInput.Value = ClampToRange(_positionXInput, (decimal)insert.PositionX);
        _positionYInput.Value = ClampToRange(_positionYInput, (decimal)insert.PositionY);
        _positionZInput.Value = ClampToRange(_positionZInput, (decimal)insert.PositionZ);
        _rotationZInput.Value = ClampToRange(_rotationZInput, (decimal)insert.RotationZ);

        UpdatePositionFieldsState();
    }

    /// <summary>Switches this insert to Manual position mode and sets its absolute X/Y/Z (mm) —
    /// used when the user drags this insert in the 3D viewport.</summary>
    public void SetManualPosition(float x, float y, float z)
    {
        _positionModeCombo.SelectedIndex = (int)TextPositionMode.Manual;
        _positionXInput.Value = ClampToRange(_positionXInput, (decimal)x);
        _positionYInput.Value = ClampToRange(_positionYInput, (decimal)y);
        _positionZInput.Value = ClampToRange(_positionZInput, (decimal)z);
    }

    private void RenderThumbnail(ISvgLibraryService svgLibrary, string svgContent)
    {
        try
        {
            _thumbnail.Image?.Dispose();
            _thumbnail.Image = svgLibrary.RenderThumbnail(svgContent, 48, 48);
        }
        catch (Exception)
        {
            // A malformed SVG must not crash the editor — leave the thumbnail blank.
            _thumbnail.Image = null;
        }
    }

    private void PickColor()
    {
        using var dialog = new ColorDialog { Color = Color.FromArgb(_colorArgb) };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _colorArgb = dialog.Color.ToArgb();
            UpdateColorButtonSwatch();
            RaiseChanged();
        }
    }

    private void UpdateColorButtonSwatch() => _colorButton.BackColor = Color.FromArgb(_colorArgb);

    private static decimal ClampToRange(NumericUpDown input, decimal value) =>
        Math.Clamp(value, input.Minimum, input.Maximum);

    private void UpdatePositionFieldsState()
    {
        bool manualOrRelative = Enum.Parse<TextPositionMode>((string)_positionModeCombo.SelectedItem!) != TextPositionMode.AutoCenter;
        _positionXInput.Enabled = manualOrRelative;
        _positionYInput.Enabled = manualOrRelative;
        _positionZInput.Enabled = manualOrRelative;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static Control Labeled(string label, Control input)
    {
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        panel.Controls.Add(input);
        return panel;
    }

    private static NumericUpDown MakeNumeric(decimal value, decimal min, decimal max) => new()
    {
        Minimum = min,
        Maximum = max,
        DecimalPlaces = 1,
        Increment = 1m,
        Value = value,
        Width = 60
    };
}
