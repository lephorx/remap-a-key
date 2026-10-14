using RemapAKey.Core;

namespace RemapAKey;

/// <summary>Pick a key from a list, or press "Press a key…" and hit it on the keyboard.</summary>
internal sealed class KeyPicker : UserControl
{
    public const string DisabledLabel = "(nothing – disable the key)";

    private readonly KeyboardHook _hook;
    private readonly Func<KeyStroke, KeyDef?> _resolve;
    private readonly ComboBox _combo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button _capture = new() { Text = "Press a key…", AutoSize = true, Dock = DockStyle.Fill };

    public event EventHandler? SelectionChanged;

    public KeyPicker(KeyboardHook hook, Func<KeyStroke, KeyDef?> resolve, bool allowDisabled)
    {
        _hook = hook;
        _resolve = resolve;

        if (allowDisabled) _combo.Items.Add(DisabledLabel);
        _combo.Items.AddRange(KeyCatalog.All.Select(k => k.Name).Cast<object>().ToArray());
        _combo.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        _capture.Click += (_, _) => BeginCapture();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(_combo, 0, 0);
        layout.Controls.Add(_capture, 1, 0);

        Controls.Add(layout);
        Height = _combo.PreferredHeight + 8;
    }

    /// <summary>The chosen key name, or null if nothing is chosen / the "disable" entry is chosen.</summary>
    public string? SelectedKey => _combo.SelectedItem is string s && s != DisabledLabel ? s : null;

    public bool HasSelection => _combo.SelectedIndex >= 0;

    public void Select(string? keyName)
    {
        var index = keyName is null ? _combo.Items.IndexOf(DisabledLabel) : _combo.Items.IndexOf(keyName);
        if (index >= 0) _combo.SelectedIndex = index;
    }

    private void BeginCapture()
    {
        _capture.Text = "Press any key…";
        _capture.Enabled = false;

        void Reset()
        {
            _capture.Text = "Press a key…";
            _capture.Enabled = true;
        }

        _hook.BeginCapture(stroke =>
        {
            // Defer: we're inside the keyboard hook callback, which must return quickly.
            BeginInvoke(() =>
            {
                Reset();

                var def = _resolve(stroke);
                if (def is null)
                    MessageBox.Show(this, "That key can't be remapped. Pick one from the list instead.",
                        "Unsupported key", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    Select(def.Name);
            });
        }, onReplaced: Reset);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hook.CancelCapture();
        base.Dispose(disposing);
    }
}
