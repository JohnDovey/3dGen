namespace ModelGenerator.UI.Controls;

/// <summary>A minimal "enter a name" prompt, used for Save/Save As.</summary>
public class TextInputDialog : Form
{
    private readonly TextBox _input;

    public string InputText => _input.Text;

    public TextInputDialog(string title, string prompt, string initialValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(360, 120);

        var label = new Label { Text = prompt, AutoSize = true, Location = new Point(12, 12) };
        _input = new TextBox { Text = initialValue, Location = new Point(12, 36), Width = 336 };

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(192, 76), Width = 75 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(273, 76), Width = 75 };

        Controls.Add(label);
        Controls.Add(_input);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        _input.SelectAll();
    }
}
