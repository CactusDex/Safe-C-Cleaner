using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SafeCCleaner.App.Services;

internal sealed record GitHubReleaseInfo(Version Version, string Tag, string Notes, string Url,
    Uri? InstallerUrl, string? InstallerName, long InstallerSize, string? Sha256);

internal sealed class GitHubUpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/CactusDex/Safe-C-Cleaner/releases/latest";
    private const long MaxInstallerBytes = 500L * 1024 * 1024;
    private static readonly HttpClient Client = CreateClient();
    private static readonly HttpClient DownloadClient = CreateClient();

    static GitHubUpdateService() => DownloadClient.Timeout = TimeSpan.FromMinutes(15);

    public static Version InstalledVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SafeCCleaner", "0.6"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<GitHubReleaseInfo> GetLatestAsync(CancellationToken token = default)
    {
        using var response = await Client.GetAsync(ApiUrl, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? throw new FormatException("Release tag is empty.");
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
            throw new FormatException("Invalid release version: " + tag);
        var url = root.GetProperty("html_url").GetString() ?? "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var releaseUri) ||
            releaseUri.Scheme != Uri.UriSchemeHttps || releaseUri.Host != "github.com" ||
            !releaseUri.AbsolutePath.StartsWith("/CactusDex/Safe-C-Cleaner/releases/", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Unexpected release URL.");
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        Uri? assetUri = null;
        string? assetName = null, checksum = null;
        long size = 0;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.Equals($"SafeCCleaner-Setup-{version.ToString(3)}.exe", StringComparison.OrdinalIgnoreCase)) continue;
                var rawUrl = asset.GetProperty("browser_download_url").GetString();
                if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var candidate) ||
                    candidate.Scheme != Uri.UriSchemeHttps || candidate.Host != "github.com" ||
                    !candidate.AbsolutePath.StartsWith($"/CactusDex/Safe-C-Cleaner/releases/download/{tag}/", StringComparison.OrdinalIgnoreCase))
                    continue;
                var digest = asset.TryGetProperty("digest", out var dp) ? dp.GetString() : null;
                if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                    digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit)) continue;
                long candidateSize = asset.GetProperty("size").GetInt64();
                if (candidateSize <= 0 || candidateSize > MaxInstallerBytes) continue;
                assetUri = candidate;
                assetName = name;
                checksum = digest[7..];
                size = candidateSize;
                break;
            }
        }
        return new GitHubReleaseInfo(version, tag, notes, releaseUri.ToString(), assetUri, assetName, size, checksum);
    }

    // Download only; do not execute arbitrary EXE returned by GitHub API.
    public async Task<string> DownloadInstallerAsync(GitHubReleaseInfo release, IProgress<double> progress, CancellationToken token)
    {
        if (release.Version <= InstalledVersion || release.InstallerUrl is null ||
            release.InstallerName is null || release.Sha256 is null ||
            release.InstallerSize <= 0 || release.InstallerSize > MaxInstallerBytes)
            throw new InvalidOperationException("A verified newer installer is not available.");

        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var destination = Path.Combine(downloads, release.InstallerName);
        var stagingDir = Path.Combine(DiagnosticLog.DirectoryPath, "updates");
        Directory.CreateDirectory(stagingDir);
        var staging = Path.Combine(stagingDir, Guid.NewGuid().ToString("N") + ".download");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, release.InstallerUrl);
            using var response = await DownloadClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != release.InstallerSize)
                throw new InvalidDataException("Release asset length mismatch.");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true))
            {
                var buffer = new byte[1024 * 128];
                long received = 0;
                int count;
                var clock = Stopwatch.StartNew();
                while ((count = await input.ReadAsync(buffer, token)) != 0)
                {
                    received += count;
                    if (received > release.InstallerSize || received > MaxInstallerBytes)
                        throw new InvalidDataException("Downloaded data exceeds expected installer size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    if (clock.ElapsedMilliseconds >= 250)
                    {
                        progress.Report(100d * received / release.InstallerSize);
                        clock.Restart();
                    }
                }
                if (received != release.InstallerSize)
                    throw new InvalidDataException("Incomplete installer download.");
            }
            await using (var file = File.OpenRead(staging))
            {
                var sha = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
                if (!sha.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installer SHA-256 mismatch.");
            }
            token.ThrowIfCancellationRequested();
            // Never overwrite a pre-existing downloaded installer; prevent implicit replacement.
            if (File.Exists(destination))
                destination = Path.Combine(downloads, Path.GetFileNameWithoutExtension(release.InstallerName) + "-" + Guid.NewGuid().ToString("N")[..8] + ".exe");
            File.Move(staging, destination);
            progress.Report(100);
            return destination;
        }
        finally
        {
            try { if (File.Exists(staging)) File.Delete(staging); } catch { }
        }
    }
}
