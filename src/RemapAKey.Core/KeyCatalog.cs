namespace RemapAKey.Core;

/// <param name="Scan">
/// The keyboard's scan code (set 1), with an 0xE0 prefix in the high byte for extended keys. This is what the
/// system-wide "Scancode Map" works with. Zero means the key can't be used in system-wide mode.
/// </param>
public sealed record KeyDef(string Name, KeyStroke Stroke, ushort Scan);

/// <summary>The keys that can be remapped, with the names shown in the UI and stored in the config.</summary>
public static class KeyCatalog
{
    public static IReadOnlyList<KeyDef> All { get; } = Build();

    private static readonly Dictionary<string, KeyDef> ByName =
        All.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<KeyStroke, KeyDef> ByStroke =
        All.ToDictionary(k => k.Stroke);

    private static readonly Dictionary<ushort, KeyDef> ByScan =
        All.Where(k => k.Scan != 0).ToDictionary(k => k.Scan);

    public static KeyDef? FindByName(string? name) =>
        name is not null && ByName.TryGetValue(name, out var def) ? def : null;

    public static KeyDef? FindByScan(ushort scan) =>
        scan != 0 && ByScan.TryGetValue(scan, out var def) ? def : null;

    public static KeyDef? Find(KeyStroke stroke) =>
        ByStroke.TryGetValue(stroke, out var def) ? def : null;

    private static List<KeyDef> Build()
    {
        var keys = new List<KeyDef>();

        void Add(string name, int vk, int scan, bool extended = false) =>
            keys.Add(new KeyDef(name, new KeyStroke((ushort)vk, extended), (ushort)scan));

        // Modifiers first: they are by far the most commonly remapped keys.
        Add("Left Ctrl", 0xA2, 0x1D);
        Add("Right Ctrl", 0xA3, 0xE01D, extended: true);
        Add("Left Win", 0x5B, 0xE05B, extended: true);
        Add("Right Win", 0x5C, 0xE05C, extended: true);
        Add("Left Alt", 0xA4, 0x38);
        Add("Right Alt", 0xA5, 0xE038, extended: true);
        Add("Left Shift", 0xA0, 0x2A);
        Add("Right Shift", 0xA1, 0x36);
        Add("Caps Lock", 0x14, 0x3A);
        Add("Menu (context key)", 0x5D, 0xE05D, extended: true);

        Add("Tab", 0x09, 0xF);
        Add("Esc", 0x1B, 0x1);
        Add("Enter", 0x0D, 0x1C);
        Add("Space", 0x20, 0x39);
        Add("Backspace", 0x08, 0xE);
        Add("Delete", 0x2E, 0xE053, extended: true);
        Add("Insert", 0x2D, 0xE052, extended: true);
        Add("Home", 0x24, 0xE047, extended: true);
        Add("End", 0x23, 0xE04F, extended: true);
        Add("Page Up", 0x21, 0xE049, extended: true);
        Add("Page Down", 0x22, 0xE051, extended: true);
        Add("Up", 0x26, 0xE048, extended: true);
        Add("Down", 0x28, 0xE050, extended: true);
        Add("Left", 0x25, 0xE04B, extended: true);
        Add("Right", 0x27, 0xE04D, extended: true);
        Add("Print Screen", 0x2C, 0xE037, extended: true);
        Add("Scroll Lock", 0x91, 0x46);
        Add("Pause", 0x13, 0x0);

        // F1-F10 are contiguous, F11/F12 sit apart, F13-F24 are the extended-keyboard keys.
        ushort[] fScans = [0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44, 0x57, 0x58,
                           0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x76];
        for (var i = 1; i <= 24; i++) Add($"F{i}", 0x6F + i, fScans[i - 1]);
        const string letterRows = "QWERTYUIOPASDFGHJKLZXCVBNM";
        ushort[] letterScans = [0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19,
                                0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
                                0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32];
        for (var c = 'A'; c <= 'Z'; c++) Add(c.ToString(), c, letterScans[letterRows.IndexOf(c)]);
        for (var c = '0'; c <= '9'; c++) Add(c.ToString(), c, c == '0' ? 0x0B : 0x02 + (c - '1'));

        Add("Semicolon", 0xBA, 0x27);
        Add("Equals", 0xBB, 0xD);
        Add("Comma", 0xBC, 0x33);
        Add("Minus", 0xBD, 0xC);
        Add("Period", 0xBE, 0x34);
        Add("Slash", 0xBF, 0x35);
        Add("Backtick", 0xC0, 0x29);
        Add("Left Bracket", 0xDB, 0x1A);
        Add("Backslash", 0xDC, 0x2B);
        Add("Right Bracket", 0xDD, 0x1B);
        Add("Quote", 0xDE, 0x28);
        Add("ISO extra key (<>)", 0xE2, 0x56);

        Add("Num Lock", 0x90, 0x45, extended: true);
        // Numpad 7-8-9 / 4-5-6 / 1-2-3 / 0 in scan-code order.
        ushort[] numpadScans = [0x52, 0x4F, 0x50, 0x51, 0x4B, 0x4C, 0x4D, 0x47, 0x48, 0x49];
        for (var i = 0; i <= 9; i++) Add($"Numpad {i}", 0x60 + i, numpadScans[i]);
        Add("Numpad *", 0x6A, 0x37);
        Add("Numpad +", 0x6B, 0x4E);
        Add("Numpad -", 0x6D, 0x4A);
        Add("Numpad .", 0x6E, 0x53);
        Add("Numpad /", 0x6F, 0xE035, extended: true);
        Add("Numpad Enter", 0x0D, 0xE01C, extended: true);

        Add("Volume Mute", 0xAD, 0xE020, extended: true);
        Add("Volume Down", 0xAE, 0xE02E, extended: true);
        Add("Volume Up", 0xAF, 0xE030, extended: true);
        Add("Next Track", 0xB0, 0xE019, extended: true);
        Add("Previous Track", 0xB1, 0xE010, extended: true);
        Add("Stop Media", 0xB2, 0xE024, extended: true);
        Add("Play/Pause", 0xB3, 0xE022, extended: true);

        return keys;
    }
}
