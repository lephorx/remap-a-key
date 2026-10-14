namespace RemapAKey.Core;

/// <summary>
/// "When <see cref="From"/> is pressed, act as <see cref="To"/>." A null <see cref="To"/> disables the key.
/// Keys are referred to by their <see cref="KeyCatalog"/> name so the config file stays readable.
/// </summary>
public sealed record KeyMapping(string From, string? To);
