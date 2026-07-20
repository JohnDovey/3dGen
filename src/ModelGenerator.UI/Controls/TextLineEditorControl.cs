using System.Drawing.Text;
using ModelGenerator.Core.Models;

namespace ModelGenerator.UI.Controls;

/// <summary>One row of the text editor: content, font, size, emboss height, and position mode
/// with its associated X/Y/Z/rotation fields (only meaningful for Manual/Relative).</summary>
public class TextLineEditorControl : UserControl
{
    private static readonly string[] FontNames = LoadInstalledFontNames();

    private readonly TextBox _contentBox = new() { Width = 140 };
    private readonly ComboBox _fontCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly NumericUpDown _fontSizeInput = MakeNumeric(12, min: 2, max: 200);
    private readonly NumericUpDown _textHeightInput = MakeNumeric(5, min: 0.2m, max: 50);
    private readonly ComboBox _positionModeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
    private readonly NumericUpDown _positionXInput = MakeNumeric(0, min: -500, max: 500);
    private readonly NumericUpDown _positionYInput = MakeNumeric(0, min: -500, max: 500);
    private readonly NumericUpDown _positionZInput = MakeNumeric(0, min: -500, max: 500);
    private readonly NumericUpDown _rotationZInput = MakeNumeric(0, min: -360, max: 360);
    private readonly Button _removeButton = new() { Text = "Remove", AutoSize = true };

    public event EventHandler? Changed;
    public event EventHandler? RemoveRequested;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.Browsable(false)]
    public int LineNumber { get; set; }

    public TextLineEditorControl()
    {
        _fontCombo.Items.AddRange(FontNames);
        _fontCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FontNames, "Arial"));
        _fontCombo.DrawMode = DrawMode.OwnerDrawFixed;
        _fontCombo.ItemHeight = 18;
        _fontCombo.DrawItem += FontCombo_DrawItem;

        _positionModeCombo.Items.AddRange(Enum.GetNames<TextPositionMode>());
        _positionModeCombo.SelectedIndex = 0;

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
        layout.Controls.Add(Labeled("Emboss (mm)", _textHeightInput));
        layout.Controls.Add(Labeled("Position", _positionModeCombo));
        layout.Controls.Add(Labeled("X", _positionXInput));
        layout.Controls.Add(Labeled("Y", _positionYInput));
        layout.Controls.Add(Labeled("Z", _positionZInput));
        layout.Controls.Add(Labeled("Rot°", _rotationZInput));
        layout.Controls.Add(_removeButton);

        Controls.Add(layout);
        // No Dock here: TextLinesPanel positions rows manually (Location/Width) so newly added
        // rows append below existing ones instead of jumping to the top of the stack.
        AutoSize = true;
        BorderStyle = BorderStyle.FixedSingle;

        _contentBox.TextChanged += (_, _) => RaiseChanged();
        _fontCombo.SelectedIndexChanged += (_, _) => RaiseChanged();
        _fontSizeInput.ValueChanged += (_, _) => RaiseChanged();
        _textHeightInput.ValueChanged += (_, _) => RaiseChanged();
        _positionModeCombo.SelectedIndexChanged += (_, _) => { UpdatePositionFieldsState(); RaiseChanged(); };
        _positionXInput.ValueChanged += (_, _) => RaiseChanged();
        _positionYInput.ValueChanged += (_, _) => RaiseChanged();
        _positionZInput.ValueChanged += (_, _) => RaiseChanged();
        _rotationZInput.ValueChanged += (_, _) => RaiseChanged();
        _removeButton.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);

        UpdatePositionFieldsState();
    }

    public TextLine ToTextLine() => new()
    {
        LineNumber = LineNumber,
        Content = _contentBox.Text,
        FontName = (string)_fontCombo.SelectedItem!,
        FontSize = (float)_fontSizeInput.Value,
        TextHeight = (float)_textHeightInput.Value,
        PositionMode = Enum.Parse<TextPositionMode>((string)_positionModeCombo.SelectedItem!),
        PositionX = (float)_positionXInput.Value,
        PositionY = (float)_positionYInput.Value,
        PositionZ = (float)_positionZInput.Value,
        RotationZ = (float)_rotationZInput.Value
    };

    /// <summary>Populates the row's controls from a persisted TextLine (inverse of ToTextLine).</summary>
    public void LoadFrom(TextLine line)
    {
        _contentBox.Text = line.Content;

        int fontIndex = Array.IndexOf(FontNames, line.FontName);
        _fontCombo.SelectedIndex = fontIndex >= 0 ? fontIndex : Math.Max(0, Array.IndexOf(FontNames, "Arial"));

        _fontSizeInput.Value = ClampToRange(_fontSizeInput, (decimal)line.FontSize);
        _textHeightInput.Value = ClampToRange(_textHeightInput, (decimal)line.TextHeight);
        _positionModeCombo.SelectedIndex = (int)line.PositionMode;
        _positionXInput.Value = ClampToRange(_positionXInput, (decimal)line.PositionX);
        _positionYInput.Value = ClampToRange(_positionYInput, (decimal)line.PositionY);
        _positionZInput.Value = ClampToRange(_positionZInput, (decimal)line.PositionZ);
        _rotationZInput.Value = ClampToRange(_rotationZInput, (decimal)line.RotationZ);

        UpdatePositionFieldsState();
    }

    private static decimal ClampToRange(NumericUpDown input, decimal value) =>
        Math.Clamp(value, input.Minimum, input.Maximum);

    private void FontCombo_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        e.DrawBackground();
        string fontName = FontNames[e.Index];
        using var font = TryCreateFont(fontName, 10.5f);
        using var brush = new SolidBrush(e.ForeColor);
        e.Graphics.DrawString(fontName, font, brush, e.Bounds);
        e.DrawFocusRectangle();
    }

    private static Font TryCreateFont(string name, float size)
    {
        try
        {
            return new Font(name, size);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, size);
        }
    }

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

    private static string[] LoadInstalledFontNames()
    {
        using var installed = new InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).Distinct().OrderBy(n => n).ToArray();
        return names.Length > 0 ? names : new[] { "Arial" };
    }
}
