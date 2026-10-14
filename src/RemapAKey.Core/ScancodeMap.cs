using System.Buffers.Binary;

namespace RemapAKey.Core;

/// <summary>
/// Builds the binary value for the Windows "Scancode Map" registry setting
/// (HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout). Windows applies it inside the keyboard driver,
/// so every program sees ordinary hardware key presses - unlike software remapping, which re-sends keys as
/// "injected" input that some programs ignore. The price: it needs admin rights and a restart.
///
/// Layout (all little endian): 8 zero bytes, a DWORD entry count (including the terminator), then one DWORD
/// per entry (low word = key to produce, high word = physical key), then a zero DWORD terminator.
/// </summary>
public static class ScancodeMap
{
    private const int HeaderSize = 8;
    private const int EntrySize = 4;

    /// <summary>
    /// Returns the registry value for the mappings, or null if none of them can be expressed (remove the value
    /// in that case). Mappings that can't be expressed are returned in <paramref name="skipped"/>.
    /// </summary>
    public static byte[]? Build(IEnumerable<KeyMapping> mappings, out IReadOnlyList<KeyMapping> skipped)
    {
        var entries = new Dictionary<ushort, ushort>(); // physical scan code -> produced scan code (0 = nothing)
        var skippedList = new List<KeyMapping>();

        foreach (var mapping in mappings)
        {
            var from = KeyCatalog.FindByName(mapping.From);
            var to = mapping.To is null ? null : KeyCatalog.FindByName(mapping.To);
            var scanTo = to?.Scan ?? 0;

            if (from is null || from.Scan == 0 || (mapping.To is not null && (to is null || to.Scan == 0)))
            {
                skippedList.Add(mapping);
                continue;
            }

            if (from.Scan == scanTo) continue;
            entries[from.Scan] = scanTo;
        }

        skipped = skippedList;
        if (entries.Count == 0) return null;

        var data = new byte[HeaderSize + 4 + (entries.Count + 1) * EntrySize];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(HeaderSize), (uint)entries.Count + 1);

        var offset = HeaderSize + 4;
        foreach (var (from, to) in entries.OrderBy(e => e.Key))
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), to);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2), from);
            offset += EntrySize;
        }

        return data; // the terminating zero entry is already there
    }

    /// <summary>Checks that the bytes are a well-formed map, so a privileged writer never stores garbage.</summary>
    public static bool IsValid(byte[] data)
    {
        if (data.Length < HeaderSize + 4 + EntrySize) return false;
        if (data.AsSpan(0, HeaderSize).IndexOfAnyExcept((byte)0) >= 0) return false;

        var count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(HeaderSize));
        if (count < 2 || count > 1000) return false;
        if (data.Length != HeaderSize + 4 + count * EntrySize) return false;

        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(data.Length - EntrySize)) == 0;
    }

    /// <summary>Reads a map back into physical scan code -> produced scan code pairs.</summary>
    public static bool TryParse(byte[] data, out Dictionary<ushort, ushort> entries)
    {
        entries = new Dictionary<ushort, ushort>();
        if (!IsValid(data)) return false;

        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(HeaderSize)) - 1;
        for (var i = 0; i < count; i++)
        {
            var offset = HeaderSize + 4 + i * EntrySize;
            entries[BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2))] =
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
        }

        return true;
    }

    public static string ToHex(byte[] data) => Convert.ToHexString(data);

    public static byte[]? FromHex(string hex)
    {
        try { return Convert.FromHexString(hex); }
        catch (FormatException) { return null; }
    }
}
