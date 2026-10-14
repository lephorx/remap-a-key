using System.Diagnostics;
using RemapAKey.Core;

namespace RemapAKey;

/// <summary>The dialogs around updating: the manual check, the "install it?" question and the download progress.</summary>
internal static class UpdateUi
{
    private const string Title = "Remap a Key";

    /// <summary>"Check for updates" button/menu item: checks, then reports the result or offers to install.</summary>
    public static async void CheckNow(AppController app, IWin32Window? owner)
    {
        switch (await app.CheckForUpdatesAsync())
        {
            case UpdateCheck.Available:
                Offer(app, owner);
                break;
            case UpdateCheck.UpToDate:
                MessageBox.Show(owner, $"You're up to date (version {Updater.CurrentVersion.ToString(3)}).",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            case UpdateCheck.Failed:
                MessageBox.Show(owner, "Couldn't reach GitHub to look for updates. Check your internet connection and try again.",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
        }
    }

    /// <summary>Asks whether to install the update that was found, and does it.</summary>
    public static void Offer(AppController app, IWin32Window? owner)
    {
        if (app.AvailableUpdate is not { } release) return;

        var question = $"Version {release.Version.ToString(3)} is available (you have {Updater.CurrentVersion.ToString(3)}).\n\n" +
                       (Updater.CanSelfUpdate
                           ? "Download and install it now? Remap a Key restarts, and remapping pauses for a moment."
                           : "Open the download page?");
        if (MessageBox.Show(owner, question, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        if (!Updater.CanSelfUpdate)
        {
            OpenReleasePage(release);
            return;
        }

        using var dialog = new DownloadDialog(release);
        if (dialog.ShowDialog(owner) == DialogResult.OK)
        {
            app.RequestExit();
            return;
        }

        if (dialog.Error is { } error)
        {
            var retry = MessageBox.Show(owner,
                $"The update couldn't be installed: {error.Message}\n\nNothing was changed. Open the download page to update by hand?",
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (retry == DialogResult.Yes) OpenReleasePage(release);
        }
    }

    private static void OpenReleasePage(ReleaseInfo release) =>
        Process.Start(new ProcessStartInfo(release.PageUrl) { UseShellExecute = true });
}

/// <summary>Downloads and installs a release, showing progress. OK = installed and the new version is starting.</summary>
internal sealed class DownloadDialog : Form
{
    private readonly ReleaseInfo _release;
    private readonly ProgressBar _bar = new() { Dock = DockStyle.Top, Maximum = 1000, Height = 22 };
    private readonly CancellationTokenSource _cancel = new();

    /// <summary>Why the install failed. Null if it succeeded or the user cancelled.</summary>
    public Exception? Error { get; private set; }

    public DownloadDialog(ReleaseInfo release)
    {
        _release = release;

        Text = "Updating Remap a Key";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ControlBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 120);

        var cancel = new Button { Text = "Cancel", AutoSize = true, Anchor = AnchorStyles.Right };
        cancel.Click += (_, _) => _cancel.Cancel();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = $"Downloading version {release.Version.ToString(3)}…", AutoSize = true });
        layout.Controls.Add(_bar);
        layout.Controls.Add(cancel);
        Controls.Add(layout);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            var progress = new Progress<double>(p => _bar.Value = (int)Math.Clamp(p * _bar.Maximum, 0, _bar.Maximum));
            await Updater.InstallAsync(_release, progress, _cancel.Token);
            DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException)
        {
            DialogResult = DialogResult.Cancel;
        }
        catch (Exception ex)
        {
            Error = ex;
            DialogResult = DialogResult.Abort;
        }
    }
}
