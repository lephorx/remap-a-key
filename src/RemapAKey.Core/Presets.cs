namespace RemapAKey.Core;

public sealed record Preset(string Name, IReadOnlyList<KeyMapping> Mappings);

public static class Presets
{
    public static IReadOnlyList<Preset> All { get; } =
    [
        new("Swap Left Ctrl and Left Win",
        [
            new("Left Ctrl", "Left Win"),
            new("Left Win", "Left Ctrl"),
        ]),
        new("Swap Left Alt and Left Win (Mac-style layout)",
        [
            new("Left Alt", "Left Win"),
            new("Left Win", "Left Alt"),
        ]),
        new("Caps Lock acts as Left Ctrl",
        [
            new("Caps Lock", "Left Ctrl"),
        ]),
        new("Caps Lock acts as Esc",
        [
            new("Caps Lock", "Esc"),
        ]),
        new("Disable Caps Lock",
        [
            new("Caps Lock", null),
        ]),
    ];
}
