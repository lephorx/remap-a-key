using RemapAKey.Core;

namespace RemapAKey;

internal sealed class MainForm : Form
{
    private readonly AppController _app;
    private readonly CheckBox _enabled = new() { Text = "Remapping enabled", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly ListView _list = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = true,
        HideSelection = false,
        Dock = DockStyle.Fill,
    };
    private readonly Button _remove = new() { Text = "Remove", AutoSize = true, Enabled = false };

    public MainForm(AppController app, Icon icon)
    {
        _app = app;

        Text = "Remap a Key";
        Icon = icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 420);
        MinimumSize = new Size(480, 340);
        StartPosition = FormStartPosition.CenterScreen;

        _list.Columns.Add("When I press", 220);
        _list.Columns.Add("It acts as", 260);

        var add = new Button { Text = "Add…", AutoSize = true };
        add.Click += (_, _) => AddMapping();
        _remove.Click += (_, _) => RemoveSelected();

        var presetMenu = new ContextMenuStrip();
        foreach (var preset in Presets.All)
        {
            var captured = preset;
            presetMenu.Items.Add(captured.Name, null, (_, _) => _app.AddMappings(captured.Mappings));
        }
        var presets = new Button { Text = "Presets ▾", AutoSize = true };
        presets.Click += (_, _) => presetMenu.Show(presets, new Point(0, presets.Height));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
        buttons.Controls.AddRange([add, _remove, presets]);

        var hint = new Label
        {
            Text = "Closing this window keeps Remap a Key running in the tray. Use the tray icon to exit.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_enabled);
        layout.Controls.Add(_list);
        layout.Controls.Add(buttons);
        layout.Controls.Add(_startWithWindows);
        layout.Controls.Add(hint);
        Controls.Add(layout);

        _enabled.CheckedChanged += (_, _) => _app.Enabled = _enabled.Checked;
        _startWithWindows.CheckedChanged += (_, _) => _app.StartWithWindows = _startWithWindows.Checked;
        _list.SelectedIndexChanged += (_, _) => _remove.Enabled = _list.SelectedItems.Count > 0;
        _list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

        _app.Changed += RefreshView;
        RefreshView();
    }

    private void RefreshView()
    {
        _enabled.Checked = _app.Enabled;
        _startWithWindows.Checked = _app.StartWithWindows;

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var m in _app.Config.Mappings)
            _list.Items.Add(new ListViewItem([m.From, m.To ?? "(disabled)"]) { Tag = m.From });
        _list.EndUpdate();
        _remove.Enabled = false;
    }

    private void AddMapping()
    {
        using var dialog = new AddMappingDialog(_app.Hook);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _app.AddMappings(dialog.Result);
    }

    private void RemoveSelected()
    {
        foreach (var from in _list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToList())
            _app.RemoveMapping(from);
    }

    // The X button hides to the tray; the app only quits from the tray menu.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }
}
