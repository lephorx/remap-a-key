using RemapAKey.Core;

namespace RemapAKey;

/// <summary>Owns the config, the engine and the hook, and keeps the three in sync.</summary>
internal sealed class AppController : IDisposable
{
    private readonly string _configPath = RemapConfig.DefaultPath;
    private readonly RemapEngine _engine = new();

    public RemapConfig Config { get; }
    public KeyboardHook Hook { get; }

    /// <summary>Raised after any change, so the window and tray menu can refresh.</summary>
    public event Action? Changed;

    /// <summary>Raised when an automatic check finds a release it hasn't told the user about yet.</summary>
    public event Action<ReleaseInfo>? UpdateFound;

    /// <summary>Raised when the app should quit (e.g. after installing an update that has already been started).</summary>
    public event Action? ExitRequested;

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly System.Windows.Forms.Timer _updateTimer = new();
    private bool _checking;
    private Version? _announced;

    /// <summary>A newer release than the one running, if a check has found one.</summary>
    public ReleaseInfo? AvailableUpdate { get; private set; }

    public AppController()
    {
        Config = RemapConfig.Load(_configPath);
        Hook = new KeyboardHook(_engine);
        Hook.Install();
        Sync();

        if (Updater.CanSelfUpdate)
        {
            _updateTimer.Interval = (int)FirstCheckDelay.TotalMilliseconds;
            _updateTimer.Tick += async (_, _) =>
            {
                _updateTimer.Interval = (int)CheckInterval.TotalMilliseconds;
                if (Config.CheckForUpdates) await CheckForUpdatesAsync(announce: true);
            };
            _updateTimer.Start();
        }
    }

    public RemapMode Mode
    {
        get => Config.Mode;
        set
        {
            Config.Mode = value;
            Commit();
        }
    }

    /// <summary>Whether this app is currently remapping keys itself (as opposed to Windows doing it).</summary>
    public bool LiveActive => Config.Mode == RemapMode.Live && Config.Enabled;

    public bool Enabled
    {
        get => Config.Enabled;
        set
        {
            Config.Enabled = value;
            Commit();
        }
    }

    public bool StartWithWindows
    {
        get => Config.StartWithWindows;
        set
        {
            AutoStart.Set(value);
            Config.StartWithWindows = value;
            Commit();
        }
    }

    public bool CheckForUpdates
    {
        get => Config.CheckForUpdates;
        set
        {
            Config.CheckForUpdates = value;
            Commit();
        }
    }

    /// <summary>Asks GitHub for the newest release. Null if the check failed or another one is already running.</summary>
    public async Task<UpdateCheck?> CheckForUpdatesAsync(bool announce = false)
    {
        if (_checking) return null;
        _checking = true;
        try
        {
            AvailableUpdate = await Updater.CheckAsync();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            return UpdateCheck.Failed;
        }
        finally
        {
            _checking = false;
        }

        Changed?.Invoke();

        if (AvailableUpdate is null) return UpdateCheck.UpToDate;
        if (announce && _announced != AvailableUpdate.Version)
        {
            _announced = AvailableUpdate.Version;
            UpdateFound?.Invoke(AvailableUpdate);
        }

        return UpdateCheck.Available;
    }

    public void RequestExit() => ExitRequested?.Invoke();

    public void AddMappings(IEnumerable<KeyMapping> mappings)
    {
        foreach (var mapping in mappings) Config.SetMapping(mapping);
        Commit();
    }

    public void RemoveMapping(string from)
    {
        Config.Mappings.RemoveAll(m => m.From == from);
        Commit();
    }

    private void Commit()
    {
        Sync();
        Config.Save(_configPath);
        Changed?.Invoke();
    }

    private void Sync()
    {
        // In system-wide mode Windows does the remapping; doing it here too would apply every swap twice.
        _engine.SetMappings(Config.Mode == RemapMode.Live ? Config.Mappings : []);
        _engine.Enabled = Config.Enabled;
    }

    /// <summary>The map the current list would produce, or null if the list is empty.</summary>
    public byte[]? DesiredSystemMap(out IReadOnlyList<KeyMapping> skipped) =>
        ScancodeMap.Build(Config.Mappings, out skipped);

    public bool ApplySystemMap()
    {
        var ok = SystemKeyMap.ApplyElevated(DesiredSystemMap(out _));
        Changed?.Invoke();
        return ok;
    }

    public bool RemoveSystemMap()
    {
        var ok = SystemKeyMap.ApplyElevated(null);
        Changed?.Invoke();
        return ok;
    }

    /// <summary>
    /// Which key was physically pressed, given what the hook reported. If a system-wide map is installed the
    /// hook only sees the already-remapped key, so undo that mapping to get back to the key under the finger.
    /// </summary>
    public KeyDef? ResolveCaptured(KeyStroke stroke)
    {
        var def = KeyCatalog.Find(stroke);
        if (def is null) return null;

        var installed = SystemKeyMap.ReadInstalled();
        if (installed is null || !ScancodeMap.TryParse(installed, out var entries)) return def;

        foreach (var (physical, produced) in entries)
            if (produced == def.Scan && KeyCatalog.FindByScan(physical) is { } original)
                return original;

        return def;
    }

    public void Dispose()
    {
        _updateTimer.Dispose();
        Hook.Dispose();
    }
}

internal enum UpdateCheck
{
    UpToDate,
    Available,
    Failed,
}
