using RemapAKey.Core;

namespace RemapAKey;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppController _app;
    private readonly MainForm _window;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _updateItem = new() { Visible = false };
    private readonly Icon _activeIcon = AppIcon.Create(active: true);
    private bool _balloonIsUpdate;
    private readonly Icon _pausedIcon = AppIcon.Create(active: false);

    public TrayApplicationContext(AppController app, bool startHidden, bool justUpdated)
    {
        _app = app;
        _window = new MainForm(app, _activeIcon);

        _enabledItem = new ToolStripMenuItem("Remapping enabled") { CheckOnClick = true };
        _enabledItem.Click += (_, _) => _app.Enabled = _enabledItem.Checked;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        _updateItem.Click += (_, _) => UpdateUi.Offer(_app, null);
        menu.Items.Add(_updateItem);
        menu.Items.Add("Check for updates…", null, (_, _) => UpdateUi.CheckNow(_app, null));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowWindow();

        _app.Changed += UpdateTray;
        _app.ExitRequested += ExitThread;
        _app.UpdateFound += release =>
        {
            _balloonIsUpdate = true;
            _tray.ShowBalloonTip(10_000, "Remap a Key update available",
                $"Version {release.Version.ToString(3)} is ready to install. Click to update.", ToolTipIcon.Info);
        };
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_balloonIsUpdate) UpdateUi.Offer(_app, null);
        };
        UpdateTray();

        if (justUpdated)
        {
            _balloonIsUpdate = false;
            _tray.ShowBalloonTip(5_000, "Remap a Key", $"Updated to version {Updater.CurrentVersion.ToString(3)}.", ToolTipIcon.Info);
        }

        if (!startHidden) ShowWindow();
    }

    private void UpdateTray()
    {
        var system = _app.Mode == RemapMode.System;
        _enabledItem.Checked = _app.Enabled;
        _enabledItem.Enabled = !system; // in system-wide mode Windows does the remapping, so there's nothing to pause
        _tray.Icon = system || _app.Enabled ? _activeIcon : _pausedIcon;
        _updateItem.Visible = _app.AvailableUpdate is not null;
        _updateItem.Text = $"Update to version {_app.AvailableUpdate?.Version.ToString(3)}…";
        _tray.Text = system ? "Remap a Key – system-wide mode"
            : _app.Enabled ? "Remap a Key – active" : "Remap a Key – paused";
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
