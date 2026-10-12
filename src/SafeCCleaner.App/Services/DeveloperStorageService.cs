using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.App.Services;

internal sealed record DeveloperStorageSnapshot(
    string DockerSummary,
    bool DockerAvailable,
    string WslSummary,
    bool WslAvailable,
    string NpmSummary,
    bool NpmAvailable,
    string NuGetSummary,
    bool NuGetAvailable,
    string AndroidSummary,
    bool AndroidAvailable,
    string? AndroidSdkPath,
    long TotalDetectedBytes);

internal sealed record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
    public string Combined => string.Join(Environment.NewLine, new[] { StdOut, StdErr }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
}

internal sealed class DeveloperStorageService
{
    public async Task<DeveloperStorageSnapshot> AnalyzeAsync(CancellationToken cancellationToken)
    {
        var dockerTask = AnalyzeDockerAsync(cancellationToken);
        var wslTask = AnalyzeWslAsync(cancellationToken);
        var npmTask = AnalyzeNpmAsync(cancellationToken);
        var nugetTask = AnalyzeNuGetAsync(cancellationToken);
        var androidTask = Task.Run(() => AnalyzeAndroidSdk(cancellationToken), cancellationToken);

        await Task.WhenAll(dockerTask, wslTask, npmTask, nugetTask, androidTask);

        var docker = await dockerTask;
        var wsl = await wslTask;
        var npm = await npmTask;
        var nuget = await nugetTask;
        var android = await androidTask;

        return new DeveloperStorageSnapshot(
            docker.Summary, docker.Available,
            wsl.Summary, wsl.Available,
            npm.Summary, npm.Available,
            nuget.Summary, nuget.Available,
            android.Summary, android.Available, android.Path,
            wsl.Bytes + npm.Bytes + nuget.Bytes + android.Bytes);
    }

    public Task<CommandResult> CleanDockerBuildCacheAsync(CancellationToken ct) =>
        RunResolvedAsync(FindDockerExecutable(), "builder prune -f", "Docker CLI не найден.", ct, 10 * 60_000);

    public Task<CommandResult> CleanDockerDanglingImagesAsync(CancellationToken ct) =>
        RunResolvedAsync(FindDockerExecutable(), "image prune -f", "Docker CLI не найден.", ct, 10 * 60_000);

    public Task<CommandResult> CleanDockerStoppedContainersAsync(CancellationToken ct) =>
        RunResolvedAsync(FindDockerExecutable(), "container prune -f", "Docker CLI не найден.", ct, 10 * 60_000);

    public Task<CommandResult> CleanNpmCacheAsync(CancellationToken ct) =>
        RunResolvedAsync(FindNpmExecutable(), "cache clean --force", "npm CLI не найден.", ct, 5 * 60_000);

    public Task<CommandResult> CleanNuGetCachesAsync(CancellationToken ct) =>
        RunResolvedAsync(FindDotNetExecutable(), "nuget locals all --clear", "dotnet CLI не найден.", ct, 5 * 60_000);

    private async Task<(string Summary, bool Available)> AnalyzeDockerAsync(CancellationToken ct)
    {
        var docker = FindDockerExecutable();
        if (docker is null)
            return ("Docker CLI не найден. Установите Docker Desktop, если он нужен на этом ПК.", false);

        var result = await RunAsync(docker, "system df", ct, 45_000);
        if (!result.Success)
        {
            var message = NormalizeOutput(result.Combined);
            if (LooksLikeDockerDaemonUnavailable(message))
                return ("Docker установлен, но Docker Desktop сейчас не запущен или движок недоступен. Запустите Docker Desktop и нажмите «Обновить».", false);

            return (string.IsNullOrWhiteSpace(message)
                ? "Docker CLI найден, но получить статистику не удалось."
                : $"Docker недоступен: {FirstLine(message)}", false);
        }

        var lines = NormalizeOutput(result.StdOut).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var compact = lines.Length <= 1
            ? "Docker доступен."
            : string.Join(" · ", lines.Skip(1).Take(4).Select(CompactWhitespace));
        return (compact, true);
    }

    private async Task<(string Summary, bool Available, long Bytes)> AnalyzeWslAsync(CancellationToken ct)
    {
        var wsl = FindWslExecutable();
        if (wsl is null)
            return ("WSL не установлен.", false, 0);

        var result = await RunAsync(wsl, "--list --quiet", ct, 20_000);
        if (!result.Success)
            return ("WSL установлен, но список дистрибутивов сейчас недоступен.", false, 0);

        var names = NormalizeOutput(result.StdOut)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var diskSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
            if (root is not null)
            {
                foreach (var subName in root.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    using var sub = root.OpenSubKey(subName);
                    var distro = sub?.GetValue("DistributionName") as string;
                    var basePath = sub?.GetValue("BasePath") as string;
                    if (string.IsNullOrWhiteSpace(distro) || string.IsNullOrWhiteSpace(basePath)) continue;
                    basePath = Environment.ExpandEnvironmentVariables(basePath);
                    var vhdx = Path.Combine(basePath, "ext4.vhdx");
                    if (!File.Exists(vhdx)) continue;
                    try { diskSizes[distro] = new FileInfo(vhdx).Length; } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not inspect WSL registry storage.", ex);
        }

        var userDistros = names.Where(x => !IsDockerWslDistribution(x)).ToArray();
        var dockerDistros = names.Where(IsDockerWslDistribution).ToArray();
        long total = 0;
        var parts = new List<string>();

        if (userDistros.Length == 0)
        {
            parts.Add("пользовательских дистрибутивов нет");
        }
        else
        {
            parts.Add($"Linux-дистрибутивов: {userDistros.Length}");
            foreach (var distro in userDistros.Take(4))
            {
                if (diskSizes.TryGetValue(distro, out var bytes))
                {
                    total += bytes;
                    parts.Add($"{distro}: {ByteSizeFormatter.Format(bytes)}");
                }
                else
                {
                    parts.Add($"{distro}: VHDX не найден");
                }
            }
        }

        long dockerBytes = 0;
        foreach (var distro in dockerDistros)
        {
            if (!diskSizes.TryGetValue(distro, out var bytes)) continue;
            dockerBytes += bytes;
            total += bytes;
        }
        if (dockerDistros.Length > 0)
            parts.Add(dockerBytes > 0 ? $"Docker WSL backend: {ByteSizeFormatter.Format(dockerBytes)}" : "Docker WSL backend обнаружен");

        var unaccounted = names.Count(n => !diskSizes.ContainsKey(n));
        if (unaccounted > 0 && names.Length > 0)
            parts.Add($"без доступного VHDX: {unaccounted}");

        return (string.Join(" · ", parts), true, total);
    }

    private async Task<(string Summary, bool Available, long Bytes)> AnalyzeNpmAsync(CancellationToken ct)
    {
        var npm = FindNpmExecutable();
        if (npm is null)
        {
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache");
            var fallbackBytes = await MeasureDirectoryAsync(fallback, ct);
            return fallbackBytes > 0
                ? ($"npm CLI не найден · обнаружен кэш {ByteSizeFormatter.Format(fallbackBytes)} · {fallback}", false, fallbackBytes)
                : ("npm не найден.", false, 0);
        }

        var result = await RunAsync(npm, "config get cache", ct, 20_000);
        if (!result.Success)
            return ("npm найден, но путь кэша получить не удалось.", false, 0);

        var path = NormalizeOutput(result.StdOut).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path)) return ("npm найден, путь кэша не определён.", true, 0);
        var bytes = await MeasureDirectoryAsync(path, ct);
        return ($"{ByteSizeFormatter.Format(bytes)} · {path}", true, bytes);
    }

    private async Task<(string Summary, bool Available, long Bytes)> AnalyzeNuGetAsync(CancellationToken ct)
    {
        var dotnet = FindDotNetExecutable();
        if (dotnet is null)
        {
            var fallback = await AnalyzeDefaultNuGetFoldersAsync(ct);
            return fallback.Bytes > 0
                ? ($"dotnet CLI не найден · обнаружено NuGet-кэшей {ByteSizeFormatter.Format(fallback.Bytes)}", false, fallback.Bytes)
                : ("dotnet/NuGet не найден.", false, 0);
        }

        var result = await RunAsync(dotnet, "nuget locals all --list", ct, 25_000);
        if (!result.Success)
            return ("dotnet найден, но список NuGet caches получить не удалось.", false, 0);

        long total = 0;
        var parts = new List<string>();
        foreach (var raw in NormalizeOutput(result.StdOut).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            ct.ThrowIfCancellationRequested();
            var colon = raw.IndexOf(':');
            if (colon <= 0) continue;
            var label = raw[..colon].Trim();
            var path = raw[(colon + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(path)) continue;
            var bytes = await MeasureDirectoryAsync(path, ct);
            total += bytes;
            parts.Add($"{label}: {ByteSizeFormatter.Format(bytes)}");
        }
        return (parts.Count == 0 ? "NuGet найден." : string.Join(" · ", parts), true, total);
    }

    private static async Task<(long Bytes, IReadOnlyList<string> Parts)> AnalyzeDefaultNuGetFoldersAsync(CancellationToken ct)
    {
        var candidates = new[]
        {
            ("global-packages", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")),
            ("http-cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "v3-cache")),
            ("temp", Path.Combine(Path.GetTempPath(), "NuGetScratch"))
        };
        long total = 0;
        var parts = new List<string>();
        foreach (var (label, path) in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var bytes = await MeasureDirectoryAsync(path, ct);
            if (bytes <= 0) continue;
            total += bytes;
            parts.Add($"{label}: {ByteSizeFormatter.Format(bytes)}");
        }
        return (total, parts);
    }

    private static (string Summary, bool Available, string? Path, long Bytes) AnalyzeAndroidSdk(CancellationToken ct)
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
        };
        var sdk = candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x));
        if (string.IsNullOrWhiteSpace(sdk)) return ("Android SDK не найден.", false, null, 0);

        long total = 0;
        var parts = new List<string>();
        foreach (var name in new[] { "system-images", "platforms", "build-tools", "emulator" })
        {
            ct.ThrowIfCancellationRequested();
            var path = Path.Combine(sdk, name);
            if (!Directory.Exists(path)) continue;
            var bytes = MeasureDirectory(path, ct);
            total += bytes;
            parts.Add($"{name}: {ByteSizeFormatter.Format(bytes)}");
        }
        return ($"{ByteSizeFormatter.Format(total)} · {string.Join(" · ", parts)}", true, sdk, total);
    }

    public async Task<IReadOnlyList<string>> GetAndroidInstalledPackagesAsync(string sdkPath, CancellationToken ct)
    {
        var sdkManager = FindSdkManager(sdkPath);
        if (sdkManager is null) return Array.Empty<string>();
        var result = await RunAsync(sdkManager, "--list_installed", ct, 60_000);
        if (!result.Success) return Array.Empty<string>();

        var packages = new List<string>();
        foreach (var raw in NormalizeOutput(result.StdOut).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pipe = raw.IndexOf('|');
            if (pipe <= 0) continue;
            var id = raw[..pipe].Trim();
            if (id.Equals("Path", StringComparison.OrdinalIgnoreCase) || id.StartsWith("-", StringComparison.Ordinal)) continue;
            if (IsSafeAndroidPackageId(id)) packages.Add(id);
        }
        return packages.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public Task<CommandResult> UninstallAndroidPackageAsync(string sdkPath, string packageId, CancellationToken ct)
    {
        if (!IsSafeAndroidPackageId(packageId))
            return Task.FromResult(new CommandResult(-1, string.Empty, "Недопустимый package ID."));
        var sdkManager = FindSdkManager(sdkPath);
        if (sdkManager is null)
            return Task.FromResult(new CommandResult(-1, string.Empty, "sdkmanager не найден. Установите Android SDK Command-line Tools."));
        return RunAsync(sdkManager, $"--uninstall \"{packageId}\"", ct, 10 * 60_000);
    }

    private static bool IsSafeAndroidPackageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 220) return false;
        return value.All(ch => char.IsLetterOrDigit(ch) || ch is ';' or '-' or '_' or '.' or ':' or '+');
    }

    private static bool IsDockerWslDistribution(string name) =>
        name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeDockerDaemonUnavailable(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return message.Contains("dockerDesktop", StringComparison.OrdinalIgnoreCase)
            || message.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)
            || message.Contains("daemon", StringComparison.OrdinalIgnoreCase)
            || message.Contains("npipe", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot connect", StringComparison.OrdinalIgnoreCase)
            || message.Contains("system cannot find the file specified", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindDockerExecutable() => FindExecutable(
        "docker.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "resources", "bin", "docker.exe"));

    private static string? FindWslExecutable() => FindExecutable(
        "wsl.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "wsl.exe"));

    private static string? FindNpmExecutable() => FindExecutable(
        "npm.cmd",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "npm.cmd"));

    private static string? FindDotNetExecutable() => FindExecutable(
        "dotnet.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "dotnet.exe"));

    private static string? FindExecutable(string fileName, params string?[] preferredPaths)
    {
        foreach (var candidate in preferredPaths)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return candidate;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var raw in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var directory = raw.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(directory)) continue;
            try
            {
                var candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static string? FindSdkManager(string sdkPath)
    {
        var candidates = new List<string>
        {
            Path.Combine(sdkPath, "cmdline-tools", "latest", "bin", "sdkmanager.bat"),
            Path.Combine(sdkPath, "tools", "bin", "sdkmanager.bat")
        };
        var cmdline = Path.Combine(sdkPath, "cmdline-tools");
        if (Directory.Exists(cmdline))
        {
            try
            {
                candidates.AddRange(Directory.EnumerateDirectories(cmdline)
                    .Select(x => Path.Combine(x, "bin", "sdkmanager.bat"))
                    .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase));
            }
            catch { }
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<long> MeasureDirectoryAsync(string path, CancellationToken ct) =>
        await Task.Run(() => MeasureDirectory(path, ct), ct);

    private static long MeasureDirectory(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path)) return 0;
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    ct.ThrowIfCancellationRequested();
                    try { total += new FileInfo(file).Length; } catch { }
                }
                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    try
                    {
                        if ((new DirectoryInfo(dir).Attributes & FileAttributes.ReparsePoint) == 0)
                            pending.Push(dir);
                    }
                    catch { }
                }
            }
            catch { }
        }
        return total;
    }

    private static Task<CommandResult> RunResolvedAsync(string? executable, string arguments, string missingMessage, CancellationToken ct, int timeoutMs)
    {
        if (string.IsNullOrWhiteSpace(executable))
            return Task.FromResult(new CommandResult(-1, string.Empty, missingMessage));
        return RunAsync(executable, arguments, ct, timeoutMs);
    }

    private static async Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken ct, int timeoutMs)
    {
        var isBatch = fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        var actualFile = isBatch ? (Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe") : fileName;
        var actualArguments = isBatch ? $"/d /s /c \"\"{fileName}\" {arguments}\"" : arguments;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = actualFile,
                Arguments = actualArguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeoutMs);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                if (ct.IsCancellationRequested) throw;
                return new CommandResult(-2, string.Empty, "Операция превысила допустимое время.");
            }
            return new CommandResult(process.ExitCode, NormalizeOutput(await stdoutTask), NormalizeOutput(await stderrTask));
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, string.Empty, ex.Message);
        }
    }

    private static string NormalizeOutput(string value) => value.Replace("\0", string.Empty).Trim();
    private static string FirstLine(string value) => NormalizeOutput(value).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? NormalizeOutput(value);
    private static string CompactWhitespace(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
