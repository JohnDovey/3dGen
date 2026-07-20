using ModelGenerator.Core.Models;

namespace ModelGenerator.UI.Controls;

/// <summary>Lets the user choose the base shape and its dimensions (size, thickness, border).</summary>
public class ShapeSelectorControl : UserControl
{
    private readonly ComboBox _shapeTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _sizeInput = MakeNumeric(60);
    private readonly NumericUpDown _heightInput = MakeNumeric(40);
    private readonly NumericUpDown _thicknessInput = MakeNumeric(10);
    private readonly NumericUpDown _borderThicknessInput = MakeNumeric(5);
    private readonly NumericUpDown _borderHeightInput = MakeNumeric(5);
    private readonly Label _heightLabel;

    public event EventHandler? ValuesChanged;

    public ShapeSelectorControl()
    {
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

        Controls.Add(layout);
        Dock = DockStyle.Top;
        AutoSize = true;

        _shapeTypeCombo.SelectedIndexChanged += (_, _) => { UpdateHeightFieldState(); RaiseValuesChanged(); };
        _sizeInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _heightInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _thicknessInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _borderThicknessInput.ValueChanged += (_, _) => RaiseValuesChanged();
        _borderHeightInput.ValueChanged += (_, _) => RaiseValuesChanged();

        UpdateHeightFieldState();
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
        UpdateHeightFieldState();
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
