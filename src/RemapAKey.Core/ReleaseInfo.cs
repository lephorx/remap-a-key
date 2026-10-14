using System.Text.Json;
using System.Text.RegularExpressions;

namespace RemapAKey.Core;

/// <summary>A published release that the app can update to.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string ExeUrl, string Sha256Url, string PageUrl)
{
    public const string ExeAssetName = "RemapAKey.exe";
    public const string ChecksumAssetName = "RemapAKey.exe.sha256";

    /// <summary>Where the app asks for the newest release.</summary>
    public const string LatestApiUrl = "https://api.github.com/repos/lephorx/remap-a-key/releases/latest";

    /// <summary>
    /// Reads the GitHub "latest release" JSON. Null if it isn't a usable release: no parsable version, or the exe
    /// or its checksum is missing (an update without a checksum is never offered).
    /// </summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;

            var tag = root.GetProperty("tag_name").GetString();
            if (tag is null || !TryParseVersion(tag, out var version)) return null;

            string? exe = null, sha = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var url = asset.GetProperty("browser_download_url").GetString();
                switch (asset.GetProperty("name").GetString())
                {
                    case ExeAssetName: exe = url; break;
                    case ChecksumAssetName: sha = url; break;
                }
            }

            if (exe is null || sha is null) return null;

            var page = root.TryGetProperty("html_url", out var html) ? html.GetString() : null;
            return new ReleaseInfo(version, tag, exe, sha, page ?? "https://github.com/lephorx/remap-a-key/releases");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Parses "v1.2.3" or "1.2" into a comparable version. Pre-release suffixes ("1.2.3-beta") are rejected.</summary>
    public static bool TryParseVersion(string text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        var trimmed = text.Trim().TrimStart('v', 'V');
        if (!Regex.IsMatch(trimmed, @"^\d+(\.\d+){1,3}$") || !Version.TryParse(trimmed, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    /// <summary>Fills unset parts with 0, because <c>new Version("1.0.0") &lt; new Version("1.0.0.0")</c>.</summary>
    public static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    public static bool IsNewer(Version candidate, Version current) => Normalize(candidate) > Normalize(current);

    /// <summary>Extracts the SHA-256 from a <c>sha256sum</c>-style file ("hash  filename"), lower-cased. Null if absent.</summary>
    public static string? ParseChecksum(string text)
    {
        var match = Regex.Match(text, @"\b[0-9a-fA-F]{64}\b");
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }
}
