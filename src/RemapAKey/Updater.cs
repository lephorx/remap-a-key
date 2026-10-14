using System.Diagnostics;
using System.Net.Http.Headers;
using RemapAKey.Core;

namespace RemapAKey;

/// <summary>
/// Finds newer releases on GitHub and replaces the running exe with one. A running exe can't be overwritten but
/// can be renamed, so the update is: download next to it, check the SHA-256 published with the release, move the
/// running exe aside, move the new one into place and start it. The new process waits for this one to exit.
/// </summary>
internal static class Updater
{
    public const string RelaunchArg = "--updated-from";

    private const string ExeFileName = "RemapAKey.exe";
    public static Version CurrentVersion { get; } =
        ReleaseInfo.Normalize(typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0));

    // Declared after CurrentVersion: CreateClient reads it.
    private static readonly HttpClient Http = CreateClient();

    /// <summary>False when running some other way (e.g. <c>dotnet run</c>), where replacing the host process would be wrong.</summary>
    public static bool CanSelfUpdate =>
        string.Equals(Path.GetFileName(Environment.ProcessPath), ExeFileName, StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RemapAKey", CurrentVersion.ToString(3)));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>The newest release if it's newer than this one, otherwise null. Throws if GitHub can't be reached.</summary>
    public static async Task<ReleaseInfo?> CheckAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var json = await Http.GetStringAsync(ReleaseInfo.LatestApiUrl, timeout.Token);
        var release = ReleaseInfo.Parse(json);
        return release is not null && ReleaseInfo.IsNewer(release.Version, CurrentVersion) ? release : null;
    }

    /// <summary>
    /// Downloads and installs the release, then starts it. The caller must exit right after this returns.
    /// Throws if anything fails, in which case the current exe is left exactly as it was.
    /// </summary>
    public static async Task InstallAsync(ReleaseInfo release, IProgress<double> progress, CancellationToken cancel)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Can't tell where Remap a Key is installed.");
        var download = exe + ".new";
        var aside = exe + ".old";

        try
        {
            var checksumText = await Http.GetStringAsync(release.Sha256Url, cancel);
            var expected = ReleaseInfo.ParseChecksum(checksumText)
                ?? throw new InvalidOperationException("The release's checksum file is unreadable.");

            await UpdateInstaller.DownloadVerifiedAsync(Http, release.ExeUrl, download, expected, progress, cancel);
            UpdateInstaller.Replace(exe, download, aside);
        }
        catch
        {
            UpdateInstaller.TryDelete(download);
            throw;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"{AutoStart.MinimizedArg} {RelaunchArg} {Environment.ProcessId}",
            UseShellExecute = false,
        });
    }

    /// <summary>
    /// When started by an update, wait for the previous process to release the keyboard hook and the
    /// single-instance mutex. Must run before the mutex is taken.
    /// </summary>
    public static void WaitForPredecessor(string[] args)
    {
        var at = Array.IndexOf(args, RelaunchArg);
        if (at < 0 || at + 1 >= args.Length || !int.TryParse(args[at + 1], out var pid)) return;

        try
        {
            using var previous = Process.GetProcessById(pid);
            previous.WaitForExit(TimeSpan.FromSeconds(15));
        }
        catch (ArgumentException)
        {
            // already gone
        }
    }

    public static bool WasJustUpdated(string[] args) => args.Contains(RelaunchArg);

    /// <summary>Removes the previous version left behind by the last update (it was still running when we renamed it).</summary>
    public static void CleanUpLeftovers()
    {
        if (Environment.ProcessPath is not { } exe) return;
        UpdateInstaller.TryDelete(exe + ".old");
        UpdateInstaller.TryDelete(exe + ".new");
    }
}
