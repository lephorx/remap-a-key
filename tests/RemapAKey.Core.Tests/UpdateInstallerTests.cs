using System.Net;
using System.Security.Cryptography;
using RemapAKey.Core;

namespace RemapAKey.Core.Tests;

public sealed class UpdateInstallerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("remapakey-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static byte[] Exe(int size = 300_000)
    {
        var bytes = new byte[size];
        new Random(1).NextBytes(bytes);
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        return bytes;
    }

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static HttpClient ServerReturning(byte[] body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new StubHandler(status, body));

    private sealed class StubHandler(HttpStatusCode status, byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    [Fact]
    public async Task DownloadsAndKeepsAFileThatMatchesItsChecksum()
    {
        var exe = Exe();
        var progress = new List<double>();

        await UpdateInstaller.DownloadVerifiedAsync(
            ServerReturning(exe), "http://x/RemapAKey.exe", Path_("new"), Sha(exe).ToUpperInvariant(),
            new SyncProgress(progress.Add), CancellationToken.None);

        Assert.Equal(exe, await File.ReadAllBytesAsync(Path_("new")));
        Assert.Equal(1.0, progress[^1]);
    }

    [Fact]
    public async Task ChecksumMismatchDiscardsTheDownload()
    {
        var exe = Exe();
        var wrong = Sha(Exe(1000));

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.DownloadVerifiedAsync(
            ServerReturning(exe), "http://x/", Path_("new"), wrong, null, CancellationToken.None));

        Assert.False(File.Exists(Path_("new")));
    }

    [Fact]
    public async Task NonExecutableIsRejectedEvenWithAMatchingChecksum()
    {
        var html = "<html>nope</html>"u8.ToArray();

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.DownloadVerifiedAsync(
            ServerReturning(html), "http://x/", Path_("new"), Sha(html), null, CancellationToken.None));

        Assert.False(File.Exists(Path_("new")));
    }

    [Fact]
    public async Task HttpErrorLeavesNothingBehind()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => UpdateInstaller.DownloadVerifiedAsync(
            ServerReturning([], HttpStatusCode.NotFound), "http://x/", Path_("new"), Sha(Exe()), null, CancellationToken.None));

        Assert.False(File.Exists(Path_("new")));
    }

    [Fact]
    public async Task EmptyDownloadIsRejected()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.DownloadVerifiedAsync(
            ServerReturning([]), "http://x/", Path_("new"), Sha([]), null, CancellationToken.None));
    }

    [Fact]
    public void ReplaceSwapsInTheNewFileAndKeepsTheOldOneAside()
    {
        File.WriteAllText(Path_("app.exe"), "old");
        File.WriteAllText(Path_("app.exe.new"), "new");

        UpdateInstaller.Replace(Path_("app.exe"), Path_("app.exe.new"), Path_("app.exe.old"));

        Assert.Equal("new", File.ReadAllText(Path_("app.exe")));
        Assert.Equal("old", File.ReadAllText(Path_("app.exe.old")));
        Assert.False(File.Exists(Path_("app.exe.new")));
    }

    [Fact]
    public void ReplaceOverwritesALeftoverAsideFile()
    {
        File.WriteAllText(Path_("app.exe"), "old");
        File.WriteAllText(Path_("app.exe.new"), "new");
        File.WriteAllText(Path_("app.exe.old"), "ancient");

        UpdateInstaller.Replace(Path_("app.exe"), Path_("app.exe.new"), Path_("app.exe.old"));

        Assert.Equal("old", File.ReadAllText(Path_("app.exe.old")));
    }

    [Fact]
    public void ReplaceRestoresTheOriginalIfTheNewFileIsMissing()
    {
        File.WriteAllText(Path_("app.exe"), "old");

        Assert.Throws<FileNotFoundException>(() =>
            UpdateInstaller.Replace(Path_("app.exe"), Path_("missing.new"), Path_("app.exe.old")));

        Assert.Equal("old", File.ReadAllText(Path_("app.exe")));
    }

    // Progress<T> posts to the thread pool; this one runs inline so the test can read the values straight away.
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
