using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Services;

public sealed class StorageAnalyzerService
{
    private const int MaxFileCandidates = 5000;
    private const int MaxDirectoryResults = 1000;

    public Task<StorageAnalyzerResult> AnalyzeAsync(
        string rootPath,
        IEnumerable<string>? excludedPaths = null,
        IProgress<StorageAnalyzerProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(rootPath, excludedPaths, progress, cancellationToken), cancellationToken);

    private static StorageAnalyzerResult Analyze(
        string rootPath,
        IEnumerable<string>? excludedPaths,
        IProgress<StorageAnalyzerProgress>? progress,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root.Length == 2 && root[1] == ':') root += Path.DirectorySeparatorChar;
        if (!Directory.Exists(root))
            return new StorageAnalyzerResult(root, 0, 0, 0, 0, TimeSpan.Zero, [], []);

        var exclusions = NormalizeExclusions(excludedPaths);
        var directoryBytes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var directoryParents = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var directoryTimes = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        var directoryLimited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        var fileCandidates = new PriorityQueue<StorageAnalyzerEntry, long>();

        directoryBytes[root] = 0;
        directoryParents[root] = null;
        directoryTimes[root] = SafeDirectoryTime(root);
        stack.Push(root);

        long filesScanned = 0;
        long directoriesScanned = 0;
        var accessLimited = 0;
        var highestProgress = 0d;
        var lastReport = DateTimeOffset.MinValue;

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            directoriesScanned++;

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsExcluded(file, exclusions)) continue;
                    try
                    {
                        var info = new FileInfo(file);
                        var bytes = info.Length;
                        filesScanned++;
                        directoryBytes[current] = directoryBytes.GetValueOrDefault(current) + bytes;

                        var entry = new StorageAnalyzerEntry(
                            info.Name,
                            info.FullName,
                            bytes,
                            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                            false,
                            CategorizeFile(info.Extension));
                        fileCandidates.Enqueue(entry, bytes);
                        if (fileCandidates.Count > MaxFileCandidates)
                            fileCandidates.Dequeue();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        accessLimited++;
                        directoryLimited.Add(current);
                    }
                    catch (IOException) { }
                    catch (System.Security.SecurityException)
                    {
                        accessLimited++;
                        directoryLimited.Add(current);
                    }
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsExcluded(dir, exclusions) || IsReparsePoint(dir)) continue;
                    try
                    {
                        var full = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (directoryBytes.ContainsKey(full)) continue;
                        directoryBytes[full] = 0;
                        directoryParents[full] = current;
                        directoryTimes[full] = SafeDirectoryTime(full);
                        stack.Push(full);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        accessLimited++;
                        directoryLimited.Add(current);
                    }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException)
            {
                accessLimited++;
                directoryLimited.Add(current);
            }
            catch (IOException) { }
            catch (System.Security.SecurityException)
            {
                accessLimited++;
                directoryLimited.Add(current);
            }

            var now = DateTimeOffset.UtcNow;
            if ((now - lastReport).TotalMilliseconds >= 180)
            {
                var pending = stack.Count;
                var raw = directoriesScanned / (double)Math.Max(1, directoriesScanned + pending) * 94d;
                highestProgress = Math.Max(highestProgress, Math.Min(94d, raw));
                progress?.Report(new StorageAnalyzerProgress(current, filesScanned, directoriesScanned, highestProgress, accessLimited));
                lastReport = now;
            }
        }

        // Roll each directory's own bytes into its parent once. This avoids walking all ancestors for every file.
        foreach (var path in directoryBytes.Keys.OrderByDescending(GetDepth).ToArray())
        {
            if (!directoryParents.TryGetValue(path, out var parent) || string.IsNullOrWhiteSpace(parent)) continue;
            directoryBytes[parent] = directoryBytes.GetValueOrDefault(parent) + directoryBytes[path];
            if (directoryLimited.Contains(path)) directoryLimited.Add(parent);
        }

        progress?.Report(new StorageAnalyzerProgress(root, filesScanned, directoriesScanned, 98, accessLimited));

        var topFiles = fileCandidates.UnorderedItems
            .Select(x => x.Element)
            .OrderByDescending(x => x.Bytes)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var topDirectories = directoryBytes
            .Where(x => !string.Equals(x.Key, root, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Value)
            .Take(MaxDirectoryResults)
            .Select(x => new StorageAnalyzerEntry(
                Path.GetFileName(x.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } name ? name : x.Key,
                x.Key,
                x.Value,
                directoryTimes.GetValueOrDefault(x.Key, DateTimeOffset.MinValue),
                true,
                "Папка",
                directoryLimited.Contains(x.Key)))
            .ToArray();

        var duration = DateTimeOffset.UtcNow - started;
        progress?.Report(new StorageAnalyzerProgress(root, filesScanned, directoriesScanned, 100, accessLimited));
        return new StorageAnalyzerResult(
            root,
            directoryBytes.GetValueOrDefault(root),
            filesScanned,
            Math.Max(0, directoriesScanned - 1),
            accessLimited,
            duration,
            topFiles,
            topDirectories);
    }

    private static int GetDepth(string path)
    {
        var depth = 0;
        foreach (var c in path)
            if (c is '\\' or '/') depth++;
        return depth;
    }

    private static DateTimeOffset SafeDirectoryTime(string path)
    {
        try { return new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero); }
        catch { return DateTimeOffset.MinValue; }
    }

    public static string CategorizeFile(string? extension)
    {
        var ext = (extension ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or
            ".txt" or ".rtf" or ".odt" or ".ods" or ".odp" or ".csv" or ".md" or ".epub" => "Документы",

            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".m4v" or
            ".mpeg" or ".mpg" or ".ts" or ".mts" or ".m2ts" or ".3gp" => "Видео",

            ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp" or ".tif" or
            ".tiff" or ".heic" or ".heif" or ".svg" or ".ico" or ".raw" or ".dng" => "Изображения",

            ".mp3" or ".flac" or ".wav" or ".ogg" or ".m4a" or ".aac" or ".wma" or
            ".opus" or ".alac" => "Музыка",

            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" or
            ".tgz" or ".cab" or ".zst" => "Архивы",

            ".iso" or ".img" or ".vhd" or ".vhdx" or ".wim" or ".esd" or ".dmg" => "Образы дисков",

            ".exe" or ".msi" or ".msix" or ".appx" or ".appxbundle" or ".msixbundle" or ".msu" => "Исполняемые файлы",

            _ => "Прочее"
        };
    }

    private static HashSet<string> NormalizeExclusions(IEnumerable<string>? paths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (paths is null) return result;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try { result.Add(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim())).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); }
            catch { }
        }
        return result;
    }

    private static bool IsExcluded(string path, HashSet<string> exclusions)
    {
        if (exclusions.Count == 0) return false;
        string full;
        try { full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return false; }
        return exclusions.Any(excluded =>
            full.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(excluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return true; }
    }
}
