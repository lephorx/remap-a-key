namespace RemapAKey;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppController _app;
    private readonly MainForm _window;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly Icon _activeIcon = AppIcon.Create(active: true);
    private readonly Icon _pausedIcon = AppIcon.Create(active: false);

    public TrayApplicationContext(AppController app, bool startHidden)
    {
        _app = app;
        _window = new MainForm(app, _activeIcon);

        _enabledItem = new ToolStripMenuItem("Remapping enabled") { CheckOnClick = true };
        _enabledItem.Click += (_, _) => _app.Enabled = _enabledItem.Checked;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowWindow();

        _app.Changed += UpdateTray;
        UpdateTray();

        if (!startHidden) ShowWindow();
    }

    private void UpdateTray()
    {
        _enabledItem.Checked = _app.Enabled;
        _tray.Icon = _app.Enabled ? _activeIcon : _pausedIcon;
        _tray.Text = _app.Enabled ? "Remap a Key – active" : "Remap a Key – paused";
    }

    private void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == FormWindowState.Minimized) _window.WindowState = FormWindowState.Normal;
        _window.Activate();
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _app.Dispose(); // unhooks the keyboard, so the keys are back to normal
        base.ExitThreadCore();
    }
}
