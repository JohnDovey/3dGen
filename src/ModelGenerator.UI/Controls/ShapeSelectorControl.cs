using ModelGenerator.Core.Models;
using ModelGenerator.Core.Services;

namespace ModelGenerator.UI.Controls;

/// <summary>Lets the user choose the base shape and its dimensions (size, thickness, border).</summary>
public class ShapeSelectorControl : UserControl
{
    private readonly ISvgLibraryService _svgLibrary;

    private readonly ComboBox _shapeTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _sizeInput = MakeNumeric(60);
    private readonly NumericUpDown _heightInput = MakeNumeric(40);
    private readonly NumericUpDown _thicknessInput = MakeNumeric(10);
    private readonly NumericUpDown _borderThicknessInput = MakeNumeric(5);
    private readonly NumericUpDown _borderHeightInput = MakeNumeric(5);
    private readonly Button _baseColorButton = new() { Text = "Color", Width = 50, AutoSize = false };
    private readonly Button _borderColorButton = new() { Text = "Color", Width = 50, AutoSize = false };
    private readonly PictureBox _customShapeThumbnail = new() { Width = 32, Height = 32, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _chooseCustomShapeButton = new() { Text = "Choose...", AutoSize = true };
    private readonly Label _heightLabel;
    private readonly Label _customShapeLabel;

    private int _baseColorArgb = Color.LightSteelBlue.ToArgb();
    private int _borderColorArgb = Color.LightSteelBlue.ToArgb();
    private string? _customShapeSvgContent;
    private string? _customShapeSourceFileName;

    public event EventHandler? ValuesChanged;

    public ShapeSelectorControl(ISvgLibraryService svgLibrary)
    {
        _svgLibrary = svgLibrary;

        _shapeTypeCombo.Items.AddRange(Enum.GetNames<ShapeType>());
        _shapeTypeCombo.SelectedIndex = 0;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        layout.Controls.Add(new Label { Text = "Shape", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_shapeTypeCombo, 1, 0);

        layout.Controls.Add(new Label { Text = "Size (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_sizeInput, 1, 1);

        _heightLabel = new Label { Text = "Height (mm, Rectangle)", AutoSize = true, Anchor = AnchorStyles.Left };
        layout.Controls.Add(_heightLabel, 0, 2);
        layout.Controls.Add(_heightInput, 1, 2);

        layout.Controls.Add(new Label { Text = "Thickness (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_thicknessInput, 1, 3);

        layout.Controls.Add(new Label { Text = "Border thickness (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(_borderThicknessInput, 1, 4);

        layout.Controls.Add(new Label { Text = "Border height (mm)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        layout.Controls.Add(_borderHeightInput, 1, 5);

        layout.Controls.Add(new Label { Text = "Base color", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        layout.Controls.Add(_baseColorButton, 1, 6);

        layout.Controls.Add(new Label { Text = "Border color", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        layout.Controls.Add(_borderColorButton, 1, 7);

        _customShapeLabel = new Label { Text = "Custom shape SVG", AutoSize = true, Anchor = AnchorStyles.Left };
        layout.Controls.Add(_customShapeLabel, 0, 8);
        var customShapeRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        customShapeRow.Controls.Add(_customShapeThumbnail);
        customShapeRow.Controls.Add(_chooseCustomShapeButton);
        layout.Controls.Add(customShapeRow, 1, 8);

        Controls.Add(layout);
        Dock = DockStyle.Top;
        AutoSize = true;

        UpdateColorButtonSwatches();

        _shapeTypeCombo.SelectedIndexChanged += (_, _) => { UpdateHeightFieldState(); UpdateCustomShapeFieldState(); RaiseValuesChanged(); };
        _sizeInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _heightInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _thicknessInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _borderThicknessInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _borderHeightInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _baseColorButton.Click += (_, _) => PickColor(isBase: true);
        _borderColorButton.Click += (_, _) => PickColor(isBase: false);
        _chooseCustomShapeButton.Click += (_, _) => ChooseCustomShape();

        UpdateHeightFieldState();
        UpdateCustomShapeFieldState();
    }

    public ShapeType ShapeType => Enum.Parse<ShapeType>((string)_shapeTypeCombo.SelectedItem!);
    public float ShapeSize => (float)_sizeInput.Value;
    public float ShapeHeight => (float)_heightInput.Value;
    public float ShapeThickness => (float)_thicknessInput.Value;
    public float BorderThickness => (float)_borderThicknessInput.Value;
    public float BorderHeight => (float)_borderHeightInput.Value;

    public void ApplyTo(Model model)
    {
        model.ShapeType = ShapeType;
        model.ShapeSize = ShapeSize;
        model.ShapeHeight = ShapeHeight;
        model.ShapeThickness = ShapeThickness;
        model.BorderThickness = BorderThickness;
        model.BorderHeight = BorderHeight;
        model.BaseColorArgb = _baseColorArgb;
        model.BorderColorArgb = _borderColorArgb;
        model.CustomShapeSvgContent = _customShapeSvgContent;
        model.CustomShapeSourceFileName = _customShapeSourceFileName;
    }

    /// <summary>Populates the controls from a persisted Model (inverse of ApplyTo).</summary>
    public void LoadFrom(Model model)
    {
        _shapeTypeCombo.SelectedIndex = (int)model.ShapeType;
        _sizeInput.Value = ClampToRange(_sizeInput, (decimal)model.ShapeSize);
        _heightInput.Value = ClampToRange(_heightInput, (decimal)model.ShapeHeight);
        _thicknessInput.Value = ClampToRange(_thicknessInput, (decimal)model.ShapeThickness);
        _borderThicknessInput.Value = ClampToRange(_borderThicknessInput, (decimal)model.BorderThickness);
        _borderHeightInput.Value = ClampToRange(_borderHeightInput, (decimal)model.BorderHeight);
        _baseColorArgb = model.BaseColorArgb;
        _borderColorArgb = model.BorderColorArgb;
        UpdateColorButtonSwatches();

        _customShapeSvgContent = model.CustomShapeSvgContent;
        _customShapeSourceFileName = model.CustomShapeSourceFileName;
        UpdateCustomShapeThumbnail();

        UpdateHeightFieldState();
        UpdateCustomShapeFieldState();
    }

    private void PickColor(bool isBase)
    {
        using var dialog = new ColorDialog { Color = Color.FromArgb(isBase ? _baseColorArgb : _borderColorArgb) };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (isBase)
        {
            _baseColorArgb = dialog.Color.ToArgb();
        }
        else
        {
            _borderColorArgb = dialog.Color.ToArgb();
        }
        UpdateColorButtonSwatches();
        RaiseValuesChanged();
    }

    private void UpdateColorButtonSwatches()
    {
        _baseColorButton.BackColor = Color.FromArgb(_baseColorArgb);
        _borderColorButton.BackColor = Color.FromArgb(_borderColorArgb);
    }

    private void ChooseCustomShape()
    {
        using var dialog = new SvgLibraryDialog(_svgLibrary);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.SelectedSvgContent is null)
        {
            return;
        }

        _customShapeSvgContent = dialog.SelectedSvgContent;
        _customShapeSourceFileName = dialog.SelectedFileName;
        UpdateCustomShapeThumbnail();
        RaiseValuesChanged();
    }

    private void UpdateCustomShapeThumbnail()
    {
        _customShapeThumbnail.Image?.Dispose();
        if (_customShapeSvgContent is null)
        {
            _customShapeThumbnail.Image = null;
            return;
        }

        try
        {
            _customShapeThumbnail.Image?.Dispose();
            _customShapeThumbnail.Image = PngThumbnail.TryDecode(_svgLibrary.RenderThumbnail(_customShapeSvgContent, 32, 32));
        }
        catch (Exception)
        {
            // A malformed custom shape SVG must not crash the editor — leave the thumbnail blank;
            // ShapeGenerator will surface a friendly error via RegeneratePreview's status label.
            _customShapeThumbnail.Image = null;
        }
    }

    private void UpdateCustomShapeFieldState()
    {
        bool isCustomSvg = ShapeType == ShapeType.CustomSvg;
        _customShapeLabel.Enabled = isCustomSvg;
        _chooseCustomShapeButton.Enabled = isCustomSvg;
    }

    private static decimal ClampToRange(NumericUpDown input, decimal value) =>
        Math.Clamp(value, input.Minimum, input.Maximum);

    private void UpdateHeightFieldState()
    {
        bool isRectangle = ShapeType == ShapeType.Rectangle;
        _heightInput.Enabled = isRectangle;
        _heightLabel.Enabled = isRectangle;
    }

    private void RaiseValuesChanged() => ValuesChanged?.Invoke(this, EventArgs.Empty);

    private static NumericUpDown MakeNumeric(decimal value) => new()
    {
        Minimum = 0.1m,
        Maximum = 1000m,
        DecimalPlaces = 1,
        Increment = 1m,
        Value = value,
        Width = 100
    };
}
