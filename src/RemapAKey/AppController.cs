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

    public AppController()
    {
        Config = RemapConfig.Load(_configPath);
        Hook = new KeyboardHook(_engine);
        Hook.Install();
        Sync();
    }

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
        _engine.SetMappings(Config.Mappings);
        _engine.Enabled = Config.Enabled;
    }

    public void Dispose() => Hook.Dispose();
}
