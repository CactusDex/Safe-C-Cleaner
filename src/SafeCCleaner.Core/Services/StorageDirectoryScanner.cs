using System.Collections.Concurrent;
using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Services;

public sealed class StorageDirectoryScanner
{
    private const int MaxParallelDirectories = 4;

    public async Task<IReadOnlyList<StorageDirectoryEntry>> ScanChildrenAsync(
        string rootPath,
        IProgress<StorageScanProgress>? progress = null,
        IEnumerable<string>? excludedPaths = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath) || IsReparsePoint(rootPath))
            return [];

        var exclusions = NormalizeExclusions(excludedPaths);
        if (IsExcluded(rootPath, exclusions))
            return [];

        string[] directories;
        string[] files;
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };
            directories = Directory.EnumerateDirectories(rootPath, "*", options)
                .Where(x => !IsReparsePoint(x) && !IsExcluded(x, exclusions))
                .ToArray();
            files = Directory.EnumerateFiles(rootPath, "*", options)
                .Where(x => !IsExcluded(x, exclusions))
                .ToArray();
        }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }

        var total = directories.Length + files.Length;
        var completed = 0;
        var limited = 0;
        var result = new ConcurrentBag<StorageDirectoryEntry>();

        await Parallel.ForEachAsync(
            directories,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDirectories, CancellationToken = cancellationToken },
            async (directory, token) =>
            {
                var measured = await MeasureDirectoryAsync(directory, exclusions, token);
                var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (measured.AccessLimited)
                    Interlocked.Increment(ref limited);

                result.Add(new StorageDirectoryEntry(
                    name,
                    directory,
                    measured.Bytes,
                    measured.Files,
                    measured.Directories,
                    true,
                    measured.AccessLimited));

                var done = Interlocked.Increment(ref completed);
                progress?.Report(new StorageScanProgress(name, done, total, Volatile.Read(ref limited)));
            });

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            try
            {
                var info = new FileInfo(file);
                result.Add(new StorageDirectoryEntry(info.Name, file, info.Length, 1, 0, false));
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref limited);
                result.Add(new StorageDirectoryEntry(name, file, 0, 0, 0, false, true));
            }
            catch (IOException) { }

            var done = Interlocked.Increment(ref completed);
            progress?.Report(new StorageScanProgress(name, done, total, Volatile.Read(ref limited)));
        }

        return result
            .OrderByDescending(x => x.Bytes)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static async Task<(long Bytes, long Files, long Directories, bool AccessLimited)> MeasureDirectoryAsync(
        string path,
        HashSet<string> exclusions,
        CancellationToken cancellationToken) =>
        await Task.Run(() => MeasureDirectory(path, exclusions, cancellationToken), cancellationToken);

    private static (long Bytes, long Files, long Directories, bool AccessLimited) MeasureDirectory(
        string path,
        HashSet<string> exclusions,
        CancellationToken cancellationToken)
    {
        long bytes = 0;
        long files = 0;
        long directories = 0;
        var accessLimited = false;
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsExcluded(file, exclusions))
                        continue;
                    try
                    {
                        var info = new FileInfo(file);
                        bytes += info.Length;
                        files++;
                    }
                    catch (UnauthorizedAccessException) { accessLimited = true; }
                    catch (IOException) { }
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsExcluded(dir, exclusions))
                        continue;
                    try
                    {
                        if (IsReparsePoint(dir))
                            continue;
                        directories++;
                        stack.Push(dir);
                    }
                    catch (UnauthorizedAccessException) { accessLimited = true; }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { accessLimited = true; }
            catch (IOException) { }
        }

        return (bytes, files, directories, accessLimited);
    }

    private static HashSet<string> NormalizeExclusions(IEnumerable<string>? paths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (paths is null)
            return result;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;
            try { result.Add(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim())).TrimEnd('\\', '/')); }
            catch { }
        }
        return result;
    }

    private static bool IsExcluded(string path, HashSet<string> exclusions)
    {
        if (exclusions.Count == 0)
            return false;
        string full;
        try { full = Path.GetFullPath(path).TrimEnd('\\', '/'); }
        catch { return false; }
        return exclusions.Any(excluded =>
            full.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(excluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (UnauthorizedAccessException) { return true; }
        catch (IOException) { return true; }
    }
}
