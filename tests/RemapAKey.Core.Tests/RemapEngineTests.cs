using RemapAKey.Core;

namespace RemapAKey.Core.Tests;

public class RemapEngineTests
{
    private static KeyStroke Key(string name) => KeyCatalog.FindByName(name)!.Stroke;

    private static KeyMapping Map(string from, string? to) => new(from, to);

    private static RemapEngine EngineWith(params KeyMapping[] mappings)
    {
        var engine = new RemapEngine();
        engine.SetMappings(mappings);
        return engine;
    }

    [Fact]
    public void UnmappedKeyPassesThrough()
    {
        var engine = EngineWith(Map("Caps Lock", "Left Ctrl"));

        Assert.Equal(KeyAction.Pass, engine.Process(Key("A"), isUp: false).Action);
        Assert.Equal(KeyAction.Pass, engine.Process(Key("A"), isUp: true).Action);
    }

    [Fact]
    public void MappedKeyIsReplacedOnPressAndRelease()
    {
        var engine = EngineWith(Map("Caps Lock", "Left Ctrl"));

        var down = engine.Process(Key("Caps Lock"), isUp: false);
        var up = engine.Process(Key("Caps Lock"), isUp: true);

        Assert.Equal(new KeyDecision(KeyAction.Replace, Key("Left Ctrl")), down);
        Assert.Equal(new KeyDecision(KeyAction.Replace, Key("Left Ctrl")), up);
    }

    [Fact]
    public void SwapDoesNotChainThroughBothMappings()
    {
        var engine = EngineWith(Map("Left Ctrl", "Left Win"), Map("Left Win", "Left Ctrl"));

        Assert.Equal(Key("Left Win"), engine.Process(Key("Left Ctrl"), false).Replacement);
        Assert.Equal(Key("Left Ctrl"), engine.Process(Key("Left Win"), false).Replacement);
    }

    [Fact]
    public void DisabledKeyIsBlocked()
    {
        var engine = EngineWith(Map("Caps Lock", null));

        Assert.Equal(KeyAction.Block, engine.Process(Key("Caps Lock"), false).Action);
        Assert.Equal(KeyAction.Block, engine.Process(Key("Caps Lock"), true).Action);
    }

    [Fact]
    public void AutoRepeatKeepsReplacingWhileHeld()
    {
        var engine = EngineWith(Map("Caps Lock", "Left Ctrl"));

        for (var i = 0; i < 3; i++)
            Assert.Equal(KeyAction.Replace, engine.Process(Key("Caps Lock"), false).Action);
    }

    [Fact]
    public void DisablingWhileHeldStillReleasesTheReplacementKey()
    {
        var engine = EngineWith(Map("Caps Lock", "Left Ctrl"));
        engine.Process(Key("Caps Lock"), false);

        engine.Enabled = false;

        Assert.Equal(new KeyDecision(KeyAction.Replace, Key("Left Ctrl")), engine.Process(Key("Caps Lock"), true));
        Assert.Equal(KeyAction.Pass, engine.Process(Key("Caps Lock"), false).Action);
    }

    [Fact]
    public void ChangingMappingsWhileHeldReleasesTheOriginalTarget()
    {
        var engine = EngineWith(Map("Caps Lock", "Left Ctrl"));
        engine.Process(Key("Caps Lock"), false);

        engine.SetMappings([new("Caps Lock", "Esc")]);

        Assert.Equal(Key("Left Ctrl"), engine.Process(Key("Caps Lock"), true).Replacement);
        Assert.Equal(Key("Esc"), engine.Process(Key("Caps Lock"), false).Replacement);
    }

    [Fact]
    public void ReleaseOfKeyPressedBeforeMappingExistedPassesThrough()
    {
        var engine = new RemapEngine();
        engine.Process(Key("Caps Lock"), false);

        engine.SetMappings([new("Caps Lock", "Left Ctrl")]);

        Assert.Equal(KeyAction.Pass, engine.Process(Key("Caps Lock"), true).Action);
    }

    [Fact]
    public void EnterAndNumpadEnterAreDistinct()
    {
        var engine = EngineWith(Map("Numpad Enter", "Space"));

        Assert.Equal(KeyAction.Pass, engine.Process(Key("Enter"), false).Action);
        Assert.Equal(Key("Space"), engine.Process(Key("Numpad Enter"), false).Replacement);
    }

    [Fact]
    public void UnknownNamesAndSelfMappingsAreIgnored()
    {
        var engine = EngineWith(Map("Nonexistent", "A"), Map("B", "Nonexistent"), Map("C", "C"));

        Assert.Equal(KeyAction.Pass, engine.Process(Key("B"), false).Action);
        Assert.Equal(KeyAction.Pass, engine.Process(Key("C"), false).Action);
    }

    [Fact]
    public void EveryPresetUsesKnownKeys()
    {
        foreach (var mapping in Presets.All.SelectMany(p => p.Mappings))
        {
            Assert.NotNull(KeyCatalog.FindByName(mapping.From));
            if (mapping.To is not null) Assert.NotNull(KeyCatalog.FindByName(mapping.To));
        }
    }

    [Fact]
    public void CatalogHasNoDuplicateNamesOrStrokes()
    {
        // The static initialisers throw on duplicates; touching the catalog proves there are none.
        Assert.NotEmpty(KeyCatalog.All);
    }
}

public class RemapConfigTests
{
    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        try
        {
            var config = new RemapConfig { Enabled = false, StartWithWindows = true };
            config.SetMapping(new("Caps Lock", "Left Ctrl"));
            config.SetMapping(new("Insert", null));
            config.Save(path);

            var loaded = RemapConfig.Load(path);

            Assert.False(loaded.Enabled);
            Assert.True(loaded.StartWithWindows);
            Assert.Equal(config.Mappings, loaded.Mappings);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void MissingOrCorruptFileGivesDefaults()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ not json");

        Assert.True(RemapConfig.Load(path).Enabled);
        Assert.Empty(RemapConfig.Load(path).Mappings);
        Assert.Empty(RemapConfig.Load(path + ".missing").Mappings);

        File.Delete(path);
    }

    [Fact]
    public void SetMappingReplacesExistingSourceKey()
    {
        var config = new RemapConfig();
        config.SetMapping(new("Caps Lock", "Left Ctrl"));
        config.SetMapping(new("caps lock", "Esc"));

        Assert.Single(config.Mappings);
        Assert.Equal("Esc", config.Mappings[0].To);
    }
}
