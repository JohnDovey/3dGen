using System.Drawing.Text;
using ModelGenerator.Core.Models;

namespace ModelGenerator.UI.Controls;

/// <summary>One row of border-following text: content, font, size, height, embossed/engraved mode, anchor angle, color.</summary>
public class BorderTextLineEditorControl : UserControl
{
    private static readonly string[] FontNames = LoadInstalledFontNames();

    private readonly TextBox _contentBox = new() { Width = 120 };
    private readonly ComboBox _fontCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly NumericUpDown _fontSizeInput = MakeNumeric(8, min: 2, max: 100);
    private readonly NumericUpDown _heightInput = MakeNumeric(1.5m, min: 0.2m, max: 20, decimals: 1);
    private readonly ComboBox _modeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly NumericUpDown _anchorInput = MakeNumeric(90, min: -360, max: 360);
    private readonly Button _colorButton = new() { Text = "Color", Width = 50, AutoSize = false };
    private readonly Button _removeButton = new() { Text = "Remove", AutoSize = true };

    private int _colorArgb = Color.DarkOrange.ToArgb();

    public event EventHandler? Changed;
    public event EventHandler? RemoveRequested;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.Browsable(false)]
    public int LineNumber { get; set; }

    public BorderTextLineEditorControl()
    {
        _fontCombo.Items.AddRange(FontNames);
        _fontCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FontNames, "Arial"));
        _modeCombo.Items.AddRange(Enum.GetNames<BorderTextMode>());
        _modeCombo.SelectedIndex = 0;
        UpdateColorButtonSwatch();

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            WrapContents = true,
            Padding = new Padding(4)
        };

        layout.Controls.Add(Labeled("Text", _contentBox));
        layout.Controls.Add(Labeled("Font", _fontCombo));
        layout.Controls.Add(Labeled("Size", _fontSizeInput));
        layout.Controls.Add(Labeled("Height (mm)", _heightInput));
        layout.Controls.Add(Labeled("Mode", _modeCombo));
        layout.Controls.Add(Labeled("Anchor°", _anchorInput));
        layout.Controls.Add(Labeled("Color", _colorButton));
        layout.Controls.Add(_removeButton);

        Controls.Add(layout);
        AutoSize = true;
        BorderStyle = BorderStyle.FixedSingle;

        _contentBox.TextChanged += (_, _) => RaiseChanged();
        _fontCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _fontSizeInput.ValueChanged += (_, _) => RaiseChanged();
        _heightInput.ValueChanged += (_, _) => RaiseChanged();
        _modeCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _anchorInput.ValueChanged += (_, _) => RaiseChanged();
        _colorButton.Click += (_, _) => PickColor();
        _removeButton.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    public BorderTextLine ToBorderTextLine() => new()
    {
        LineNumber = LineNumber,
        Content = _contentBox.Text,
        FontName = (string)_fontCombo.SelectedItem!,
        FontSize = (float)_fontSizeInput.Value,
        Height = (float)_heightInput.Value,
        Mode = Enum.Parse<BorderTextMode>((string)_modeCombo.SelectedItem!),
        AnchorAngleDegrees = (float)_anchorInput.Value,
        ColorArgb = _colorArgb
    };

    public void LoadFrom(BorderTextLine line)
    {
        _contentBox.Text = line.Content;
        int fontIndex = Array.IndexOf(FontNames, line.FontName);
        _fontCombo.SelectedIndex = fontIndex >= 0 ? fontIndex : Math.Max(0, Array.IndexOf(FontNames, "Arial"));
        _fontSizeInput.Value = ClampToRange(_fontSizeInput, (decimal)line.FontSize);
        _heightInput.Value = ClampToRange(_heightInput, (decimal)line.Height);
        _modeCombo.SelectedIndex = (int)line.Mode;
        _anchorInput.Value = ClampToRange(_anchorInput, (decimal)line.AnchorAngleDegrees);
        _colorArgb = line.ColorArgb;
        UpdateColorButtonSwatch();
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

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static NumericUpDown MakeNumeric(decimal value, decimal min, decimal max, int decimals = 0) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        DecimalPlaces = decimals,
        Width = 60
    };

    private static decimal ClampToRange(NumericUpDown input, decimal value) =>
        Math.Clamp(value, input.Minimum, input.Maximum);

    private static Control Labeled(string text, Control control)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(new Label { Text = text, AutoSize = true });
        panel.Controls.Add(control);
        return panel;
    }

    private static string[] LoadInstalledFontNames()
    {
        using var collection = new InstalledFontCollection();
        return collection.Families.Select(f => f.Name).OrderBy(n => n).ToArray();
    }
}
