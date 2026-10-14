using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using RemapAKey.Core;

namespace RemapAKey;

/// <summary>
/// Reads and writes the system-wide Scancode Map. Writing needs admin rights, so the main app (which runs
/// unelevated) re-launches itself with a UAC prompt to do the one-line registry write.
/// </summary>
internal static class SystemKeyMap
{
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layout";
    private const string ValueName = "Scancode Map";

    public const string SetArg = "--set-scancode-map";
    public const string ClearArg = "--clear-scancode-map";

    private const int ErrorCancelled = 1223; // user clicked "No" on the UAC prompt

    /// <summary>The map currently stored in Windows, or null if there isn't one. Readable without admin rights.</summary>
    public static byte[]? ReadInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as byte[];
    }

    public static bool IsElevatedCommand(string[] args) =>
        args.Length > 0 && args[0] is SetArg or ClearArg;

    /// <summary>Entry point of the elevated helper process. Returns the process exit code.</summary>
    public static int RunElevatedCommand(string[] args)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath, writable: true);
            if (key is null) return 3;

            if (args[0] == ClearArg)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return 0;
            }

            // Never write something we wouldn't have produced ourselves.
            var map = args.Length == 2 ? ScancodeMap.FromHex(args[1]) : null;
            if (map is null || !ScancodeMap.IsValid(map)) return 4;

            key.SetValue(ValueName, map, RegistryValueKind.Binary);
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 2;
        }
    }

    /// <summary>Installs the map (or removes it if null) via a UAC prompt. False if declined or failed.</summary>
    public static bool ApplyElevated(byte[]? map)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            Arguments = map is null ? ClearArg : $"{SetArg} {ScancodeMap.ToHex(map)}",
            UseShellExecute = true, // required for the "runas" verb
            Verb = "runas",
        };

        try
        {
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }
}
