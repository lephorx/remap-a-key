namespace RemapAKey.Core;

public enum KeyAction
{
    /// <summary>Let the keystroke through untouched.</summary>
    Pass,
    /// <summary>Swallow the keystroke.</summary>
    Block,
    /// <summary>Swallow the keystroke and send <see cref="KeyDecision.Replacement"/> instead.</summary>
    Replace,
}

public readonly record struct KeyDecision(KeyAction Action, KeyStroke Replacement = default)
{
    public static readonly KeyDecision Pass = new(KeyAction.Pass);
    public static readonly KeyDecision Block = new(KeyAction.Block);
}

/// <summary>
/// The remapping logic, free of any Windows API so it can be unit tested. Feed it every key event
/// and it says what to do with it. Not thread safe: the keyboard hook calls it from a single thread.
/// </summary>
public sealed class RemapEngine
{
    private Dictionary<KeyStroke, KeyStroke?> _map = new();

    // Keys currently held down that we remapped, and what they were remapped to. The release is routed
    // through this rather than the live mapping, so changing the mapping (or turning remapping off)
    // while a key is held can't leave the replacement key stuck down.
    private readonly Dictionary<KeyStroke, KeyStroke?> _held = new();

    public bool Enabled { get; set; } = true;

    /// <summary>Replaces the active mappings. Unknown key names and no-op mappings are ignored.</summary>
    public void SetMappings(IEnumerable<KeyMapping> mappings)
    {
        var map = new Dictionary<KeyStroke, KeyStroke?>();

        foreach (var mapping in mappings)
        {
            var from = KeyCatalog.FindByName(mapping.From);
            if (from is null) continue;

            KeyStroke? to = null;
            if (mapping.To is not null)
            {
                var toDef = KeyCatalog.FindByName(mapping.To);
                if (toDef is null || toDef.Stroke == from.Stroke) continue;
                to = toDef.Stroke;
            }

            map[from.Stroke] = to;
        }

        _map = map;
    }

    public KeyDecision Process(KeyStroke key, bool isUp)
    {
        // Covers both the release and the auto-repeat presses of a key we already remapped.
        if (_held.TryGetValue(key, out var heldTarget))
        {
            if (isUp) _held.Remove(key);
            return Decide(heldTarget);
        }

        if (isUp || !Enabled || !_map.TryGetValue(key, out var target))
            return KeyDecision.Pass;

        _held[key] = target;
        return Decide(target);
    }

    private static KeyDecision Decide(KeyStroke? target) =>
        target is { } t ? new KeyDecision(KeyAction.Replace, t) : KeyDecision.Block;
}
