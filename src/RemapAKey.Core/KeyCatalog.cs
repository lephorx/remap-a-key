namespace RemapAKey.Core;

public sealed record KeyDef(string Name, KeyStroke Stroke);

/// <summary>The keys that can be remapped, with the names shown in the UI and stored in the config.</summary>
public static class KeyCatalog
{
    public static IReadOnlyList<KeyDef> All { get; } = Build();

    private static readonly Dictionary<string, KeyDef> ByName =
        All.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<KeyStroke, KeyDef> ByStroke =
        All.ToDictionary(k => k.Stroke);

    public static KeyDef? FindByName(string? name) =>
        name is not null && ByName.TryGetValue(name, out var def) ? def : null;

    public static KeyDef? Find(KeyStroke stroke) =>
        ByStroke.TryGetValue(stroke, out var def) ? def : null;

    private static List<KeyDef> Build()
    {
        var keys = new List<KeyDef>();

        void Add(string name, int vk, bool extended = false) =>
            keys.Add(new KeyDef(name, new KeyStroke((ushort)vk, extended)));

        // Modifiers first: they are by far the most commonly remapped keys.
        Add("Left Ctrl", 0xA2);
        Add("Right Ctrl", 0xA3, extended: true);
        Add("Left Win", 0x5B, extended: true);
        Add("Right Win", 0x5C, extended: true);
        Add("Left Alt", 0xA4);
        Add("Right Alt", 0xA5, extended: true);
        Add("Left Shift", 0xA0);
        Add("Right Shift", 0xA1);
        Add("Caps Lock", 0x14);
        Add("Menu (context key)", 0x5D, extended: true);

        Add("Tab", 0x09);
        Add("Esc", 0x1B);
        Add("Enter", 0x0D);
        Add("Space", 0x20);
        Add("Backspace", 0x08);
        Add("Delete", 0x2E, extended: true);
        Add("Insert", 0x2D, extended: true);
        Add("Home", 0x24, extended: true);
        Add("End", 0x23, extended: true);
        Add("Page Up", 0x21, extended: true);
        Add("Page Down", 0x22, extended: true);
        Add("Up", 0x26, extended: true);
        Add("Down", 0x28, extended: true);
        Add("Left", 0x25, extended: true);
        Add("Right", 0x27, extended: true);
        Add("Print Screen", 0x2C, extended: true);
        Add("Scroll Lock", 0x91);
        Add("Pause", 0x13);

        for (var i = 1; i <= 24; i++) Add($"F{i}", 0x6F + i);
        for (var c = 'A'; c <= 'Z'; c++) Add(c.ToString(), c);
        for (var c = '0'; c <= '9'; c++) Add(c.ToString(), c);

        Add("Semicolon", 0xBA);
        Add("Equals", 0xBB);
        Add("Comma", 0xBC);
        Add("Minus", 0xBD);
        Add("Period", 0xBE);
        Add("Slash", 0xBF);
        Add("Backtick", 0xC0);
        Add("Left Bracket", 0xDB);
        Add("Backslash", 0xDC);
        Add("Right Bracket", 0xDD);
        Add("Quote", 0xDE);
        Add("ISO extra key (<>)", 0xE2);

        Add("Num Lock", 0x90, extended: true);
        for (var i = 0; i <= 9; i++) Add($"Numpad {i}", 0x60 + i);
        Add("Numpad *", 0x6A);
        Add("Numpad +", 0x6B);
        Add("Numpad -", 0x6D);
        Add("Numpad .", 0x6E);
        Add("Numpad /", 0x6F, extended: true);
        Add("Numpad Enter", 0x0D, extended: true);

        Add("Volume Mute", 0xAD, extended: true);
        Add("Volume Down", 0xAE, extended: true);
        Add("Volume Up", 0xAF, extended: true);
        Add("Next Track", 0xB0, extended: true);
        Add("Previous Track", 0xB1, extended: true);
        Add("Stop Media", 0xB2, extended: true);
        Add("Play/Pause", 0xB3, extended: true);

        return keys;
    }
}
