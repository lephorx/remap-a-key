using RemapAKey.Core;

namespace RemapAKey;

internal sealed class MainForm : Form
{
    private const string LiveLabel = "Live – instant, no admin needed";
    private const string SystemLabel = "System-wide – works in every app (admin + restart)";

    private readonly AppController _app;
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    private readonly Label _modeHelp = new() { AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = SystemColors.GrayText };
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
    private readonly Label _warning = new() { AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = Color.Firebrick };
    private readonly FlowLayoutPanel _systemPanel = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
    private readonly Label _systemStatus = new() { AutoSize = true, MaximumSize = new Size(520, 0) };
    private readonly CheckBox _checkForUpdates = new() { Text = "Check for updates automatically", AutoSize = true };
    private readonly FlowLayoutPanel _updateBanner = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
    private readonly Label _updateText = new() { AutoSize = true, MaximumSize = new Size(520, 0), Anchor = AnchorStyles.Left };
    private bool _refreshing;

    public MainForm(AppController app, Icon icon)
    {
        _app = app;

        Text = "Remap a Key";
        Icon = icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 620);
        MinimumSize = new Size(480, 540);
        StartPosition = FormStartPosition.CenterScreen;

        _list.Columns.Add("When I press", 220);
        _list.Columns.Add("It acts as", 260);

        _mode.Items.AddRange([LiveLabel, SystemLabel]);

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

        var apply = new Button { Text = "Apply to Windows…", AutoSize = true };
        apply.Click += (_, _) => ApplySystemMap();
        var removeSystem = new Button { Text = "Remove system-wide remap…", AutoSize = true };
        removeSystem.Click += (_, _) => RemoveSystemMap();
        _systemPanel.Controls.AddRange([_systemStatus, apply, removeSystem]);
        _systemPanel.SetFlowBreak(_systemStatus, true);

        var installUpdate = new Button { Text = "Update now…", AutoSize = true };
        installUpdate.Click += (_, _) => UpdateUi.Offer(_app, this);
        _updateBanner.Controls.AddRange([_updateText, installUpdate]);

        var checkNow = new Button { Text = "Check for updates", AutoSize = true };
        checkNow.Click += (_, _) => UpdateUi.CheckNow(_app, this);
        var updates = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        updates.Controls.AddRange([
            new Label { Text = $"Version {Updater.CurrentVersion.ToString(3)}", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText },
            checkNow,
            _checkForUpdates,
        ]);

        var hint = new Label
        {
            Text = "Closing this window keeps Remap a Key running in the tray. Use the tray icon to exit.",
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = SystemColors.GrayText,
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(_updateBanner);
        layout.Controls.Add(new Label { Text = "Remap method", AutoSize = true });
        layout.Controls.Add(_mode);
        layout.Controls.Add(_modeHelp);
        layout.Controls.Add(_warning);
        layout.Controls.Add(_enabled);
        layout.Controls.Add(_list);
        layout.Controls.Add(buttons);
        layout.Controls.Add(_systemPanel);
        layout.Controls.Add(_startWithWindows);
        layout.Controls.Add(updates);
        layout.Controls.Add(hint);
        // The list is the one row that should soak up spare height.
        for (var row = 0; row < layout.Controls.Count; row++)
            layout.RowStyles.Add(layout.GetControlFromPosition(0, row) == _list
                ? new RowStyle(SizeType.Percent, 100)
                : new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        _mode.SelectedIndexChanged += (_, _) =>
        {
            if (!_refreshing) _app.Mode = _mode.SelectedIndex == 1 ? RemapMode.System : RemapMode.Live;
        };
        _enabled.CheckedChanged += (_, _) => { if (!_refreshing) _app.Enabled = _enabled.Checked; };
        _startWithWindows.CheckedChanged += (_, _) => { if (!_refreshing) _app.StartWithWindows = _startWithWindows.Checked; };
        _checkForUpdates.CheckedChanged += (_, _) => { if (!_refreshing) _app.CheckForUpdates = _checkForUpdates.Checked; };
        _list.SelectedIndexChanged += (_, _) => _remove.Enabled = _list.SelectedItems.Count > 0;
        _list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

        _app.Changed += RefreshView;
        RefreshView();
    }

    private void RefreshView()
    {
        _refreshing = true;

        var system = _app.Mode == RemapMode.System;
        _mode.SelectedIndex = system ? 1 : 0;
        _enabled.Checked = _app.Enabled;
        _startWithWindows.Checked = _app.StartWithWindows;
        _checkForUpdates.Checked = _app.CheckForUpdates;

        _updateBanner.Visible = _app.AvailableUpdate is not null;
        _updateText.Text = $"Version {_app.AvailableUpdate?.Version.ToString(3)} is available.";

        _modeHelp.Text = system
            ? "Windows itself swaps the keys, before any program sees them, so it works everywhere (Raycast, games, " +
              "admin windows, even the login screen). Click Apply to save it; it takes effect after a restart. " +
              "Remap a Key doesn't need to be running."
            : "Remap a Key swaps the keys while it runs. Instant, but a few programs ignore keys remapped this way.";

        _enabled.Visible = !system;
        _startWithWindows.Visible = !system;
        _systemPanel.Visible = system;
        _warning.Visible = !system && SystemKeyMap.ReadInstalled() is not null;
        _warning.Text = "⚠ A system-wide remap is also installed in Windows, so remaps made here stack on top of it " +
                        "(a swap would be undone). Switch to System-wide mode to change or remove it.";
        if (system) _systemStatus.Text = DescribeSystemState();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var m in _app.Config.Mappings)
            _list.Items.Add(new ListViewItem([m.From, m.To ?? "(disabled)"]) { Tag = m.From });
        _list.EndUpdate();
        _remove.Enabled = false;

        _refreshing = false;
    }

    private string DescribeSystemState()
    {
        var desired = _app.DesiredSystemMap(out var skipped);
        var installed = SystemKeyMap.ReadInstalled();

        var text = (desired, installed) switch
        {
            (null, null) => "No remaps yet.",
            (null, not null) => "Windows still has a remap installed, but your list is empty. Click Remove to clear it.",
            (not null, not null) when desired.AsSpan().SequenceEqual(installed) =>
                "Applied. If you only just applied it, restart Windows to activate it.",
            _ => "Not applied yet. Click Apply to save these remaps to Windows.",
        };

        return skipped.Count == 0
            ? text
            : text + $" Not supported system-wide, so skipped: {string.Join(", ", skipped.Select(m => m.From))}.";
    }

    private void ApplySystemMap()
    {
        if (_app.ApplySystemMap())
            MessageBox.Show(this, "Saved to Windows. Restart your PC for the remaps to take effect.",
                "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(this, "Nothing was changed: the administrator prompt was declined or failed.",
                "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void RemoveSystemMap()
    {
        if (_app.RemoveSystemMap())
            MessageBox.Show(this, "Removed. Restart your PC to go back to the normal keyboard.",
                "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(this, "Nothing was changed: the administrator prompt was declined or failed.",
                "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void AddMapping()
    {
        using var dialog = new AddMappingDialog(_app);
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
