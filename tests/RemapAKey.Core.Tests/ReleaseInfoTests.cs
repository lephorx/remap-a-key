using RemapAKey.Core;

namespace RemapAKey.Core.Tests;

public class ReleaseInfoTests
{
    private const string ReleaseJson = """
        {
          "tag_name": "v1.2.0",
          "html_url": "https://github.com/lephorx/remap-a-key/releases/tag/v1.2.0",
          "draft": false,
          "prerelease": false,
          "assets": [
            { "name": "RemapAKey.exe", "browser_download_url": "https://example.test/RemapAKey.exe" },
            { "name": "RemapAKey.exe.sha256", "browser_download_url": "https://example.test/RemapAKey.exe.sha256" }
          ]
        }
        """;

    [Fact]
    public void ParsesALatestReleaseResponse()
    {
        var release = ReleaseInfo.Parse(ReleaseJson);

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 2, 0, 0), release.Version);
        Assert.Equal("https://example.test/RemapAKey.exe", release.ExeUrl);
        Assert.Equal("https://example.test/RemapAKey.exe.sha256", release.Sha256Url);
    }

    [Fact]
    public void ReleaseWithoutChecksumIsNotOffered()
    {
        var json = ReleaseJson.Replace("RemapAKey.exe.sha256\"", "other.txt\"");
        Assert.Null(ReleaseInfo.Parse(json));
    }

    [Fact]
    public void ReleaseWithoutExeIsNotOffered()
    {
        var json = ReleaseJson.Replace("\"RemapAKey.exe\"", "\"other.exe\"");
        Assert.Null(ReleaseInfo.Parse(json));
    }

    [Theory]
    [InlineData("\"draft\": false", "\"draft\": true")]
    [InlineData("\"prerelease\": false", "\"prerelease\": true")]
    [InlineData("v1.2.0", "v1.2.0-beta.1")]
    [InlineData("v1.2.0", "nightly")]
    public void DraftsPrereleasesAndUnversionedTagsAreIgnored(string from, string to) =>
        Assert.Null(ReleaseInfo.Parse(ReleaseJson.Replace(from, to)));

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"message\":\"Not Found\"}")]
    public void GarbageIsIgnored(string json) => Assert.Null(ReleaseInfo.Parse(json));

    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.1", "1.0.9", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0.0", false)] // a plain Version("1.0.0") would sort below Version("1.0.0.0")
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.10.0", "1.9.0", true)]   // numeric, not alphabetical
    public void ComparesVersions(string candidate, string current, bool expected)
    {
        Assert.True(ReleaseInfo.TryParseVersion(candidate, out var c));
        Assert.True(ReleaseInfo.TryParseVersion(current, out var cur));
        Assert.Equal(expected, ReleaseInfo.IsNewer(c, cur));
    }

    [Fact]
    public void ReadsChecksumFromSha256sumOutput()
    {
        var hash = new string('a', 64);
        Assert.Equal(hash, ReleaseInfo.ParseChecksum($"{hash.ToUpperInvariant()}  RemapAKey.exe\n"));
        Assert.Equal(hash, ReleaseInfo.ParseChecksum(hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>404</html>")]
    [InlineData("abc123")]
    public void RejectsMalformedChecksum(string text) => Assert.Null(ReleaseInfo.ParseChecksum(text));
}
