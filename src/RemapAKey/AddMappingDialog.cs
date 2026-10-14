using RemapAKey.Core;

namespace RemapAKey;

internal sealed class AddMappingDialog : Form
{
    private readonly KeyPicker _from;
    private readonly KeyPicker _to;
    private readonly CheckBox _swap = new() { Text = "Also do the reverse (swap the two keys)", AutoSize = true, Checked = true };
    private readonly Button _ok = new() { Text = "Add", DialogResult = DialogResult.OK, Enabled = false, AutoSize = true };

    public AddMappingDialog(AppController app)
    {
        Text = "Add a key remap";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 250);

        _from = new KeyPicker(app.Hook, app.ResolveCaptured, allowDisabled: false);
        _to = new KeyPicker(app.Hook, app.ResolveCaptured, allowDisabled: true);
        _from.SelectionChanged += (_, _) => UpdateState();
        _to.SelectionChanged += (_, _) => UpdateState();
        _swap.CheckedChanged += (_, _) => UpdateState();

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_ok);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "When I press:", AutoSize = true });
        layout.Controls.Add(_from);
        layout.Controls.Add(new Label { Text = "Act as:", AutoSize = true, Margin = new Padding(3, 10, 3, 0) });
        layout.Controls.Add(_to);
        layout.Controls.Add(_swap);
        layout.Controls.Add(buttons);
        foreach (Control c in layout.Controls) if (c is KeyPicker) c.Dock = DockStyle.Top;

        Controls.Add(layout);
        AcceptButton = _ok;
        CancelButton = cancel;
    }

    /// <summary>The mappings to add (one, or two when swapping).</summary>
    public IReadOnlyList<KeyMapping> Result { get; private set; } = [];

    private bool SameKey => _from.SelectedKey is { } f && f == _to.SelectedKey;

    private void UpdateState()
    {
        // Swapping only makes sense when the target is a real key.
        _swap.Enabled = _to.SelectedKey is not null;
        _ok.Enabled = _from.HasSelection && _to.HasSelection && !SameKey;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            var from = _from.SelectedKey!;
            var to = _to.SelectedKey;

            var result = new List<KeyMapping> { new(from, to) };
            if (_swap.Checked && to is not null) result.Add(new KeyMapping(to, from));
            Result = result;
        }

        base.OnFormClosing(e);
    }
}
