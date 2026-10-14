namespace RemapAKey;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var singleInstance = new Mutex(initiallyOwned: true, "RemapAKey.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("Remap a Key is already running – look for its icon in the system tray.",
                "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        AppController app;
        try
        {
            app = new AppController();
        }
        catch (InvalidOperationException e)
        {
            MessageBox.Show(e.Message, "Remap a Key", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var startHidden = args.Contains(AutoStart.MinimizedArg);
        Application.Run(new TrayApplicationContext(app, startHidden));
    }
}
