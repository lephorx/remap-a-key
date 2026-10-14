namespace RemapAKey.Core;

/// <summary>
/// A physical key as reported by the Windows keyboard hook: a virtual-key code plus the
/// "extended key" flag, which is what tells apart e.g. Enter from Numpad Enter.
/// </summary>
public readonly record struct KeyStroke(ushort Vk, bool Extended);
