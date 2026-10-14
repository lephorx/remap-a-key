using System.Security.Cryptography;

namespace RemapAKey.Core;

/// <summary>The file-level steps of a self-update, kept free of Windows APIs so they can be tested anywhere.</summary>
public static class UpdateInstaller
{
    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="path"/> and checks its SHA-256 and that it is an
    /// executable. On any failure the partial file is deleted and an exception thrown.
    /// </summary>
    public static async Task DownloadVerifiedAsync(
        HttpClient http, string url, string path, string expectedSha256, IProgress<double>? progress, CancellationToken cancel)
    {
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long done = 0;

            await using (var source = await response.Content.ReadAsStreamAsync(cancel))
            await using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                int read;
                while ((read = await source.ReadAsync(buffer, cancel)) > 0)
                {
                    if (done == 0 && (read < 2 || buffer[0] != 'M' || buffer[1] != 'Z'))
                        throw new InvalidDataException("The download isn't a Windows program.");

                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancel);
                    done += read;
                    if (total > 0) progress?.Report((double)done / total.Value);
                }
            }

            if (done == 0) throw new InvalidDataException("The download was empty.");
            if (!string.Equals(Convert.ToHexString(hash.GetHashAndReset()), expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The download doesn't match its published checksum, so it was discarded.");
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    /// <summary>
    /// Puts <paramref name="newFile"/> at <paramref name="target"/>, moving the existing file to
    /// <paramref name="aside"/> first (a running exe can be renamed but not overwritten). If the second move
    /// fails the original is put back, so <paramref name="target"/> is never left missing.
    /// </summary>
    public static void Replace(string target, string newFile, string aside)
    {
        TryDelete(aside);
        File.Move(target, aside);
        try
        {
            File.Move(newFile, target);
        }
        catch
        {
            File.Move(aside, target);
            throw;
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Locked by antivirus or a process that's still exiting; the next start tries again.
        }
    }
}
