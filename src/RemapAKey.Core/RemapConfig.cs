using System.Text.Json;

namespace RemapAKey.Core;

public sealed class RemapConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public List<KeyMapping> Mappings { get; set; } = [];

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemapAKey", "config.json");

    /// <summary>Loads the config, falling back to defaults if the file is missing or unreadable.</summary>
    public static RemapConfig Load(string path)
    {
        try
        {
            var config = JsonSerializer.Deserialize<RemapConfig>(File.ReadAllText(path), JsonOptions);
            if (config is not null)
            {
                config.Mappings ??= [];
                return config;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new RemapConfig();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write-then-move so a crash mid-save can't leave a truncated config behind.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Adds or replaces the mapping for the given source key.</summary>
    public void SetMapping(KeyMapping mapping)
    {
        Mappings.RemoveAll(m => string.Equals(m.From, mapping.From, StringComparison.OrdinalIgnoreCase));
        Mappings.Add(mapping);
    }
}
