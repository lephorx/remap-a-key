namespace RemapAKey;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // The elevated helper: write the registry value and exit, no UI.
        if (SystemKeyMap.IsElevatedCommand(args))
            return SystemKeyMap.RunElevatedCommand(args);

        Updater.WaitForPredecessor(args);

        using var singleInstance = new Mutex(initiallyOwned: true, "RemapAKey.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("Remap a Key is already running – look for its icon in the system tray.",
                "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        Updater.CleanUpLeftovers();
        ApplicationConfiguration.Initialize();

        AppController app;
        try
        {
            app = new AppController();
        }
        catch (InvalidOperationException e)
        {
            MessageBox.Show(e.Message, "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        var startHidden = args.Contains(AutoStart.MinimizedArg);
        Application.Run(new TrayApplicationContext(app, startHidden, Updater.WasJustUpdated(args)));
        return 0;
    }
}
