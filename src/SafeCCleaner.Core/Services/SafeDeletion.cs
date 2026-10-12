using SafeCCleaner.Core.Safety;

namespace SafeCCleaner.Core.Services;

public sealed record DeletionStats(long FreedBytes, long DeletedFiles, long SkippedFiles);

public static class SafeDeletion
{
    public static Task<DeletionStats> DeleteContentsAsync(string allowedRoot, CancellationToken cancellationToken = default) =>
        Task.Run(() => DeleteContents(allowedRoot, cancellationToken), cancellationToken);

    public static Task<DeletionStats> DeleteMatchingFilesAsync(
        string allowedRoot,
        string pattern,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => DeleteMatchingFiles(allowedRoot, pattern, cancellationToken), cancellationToken);

    public static Task<DeletionStats> DeleteContentsOlderThanAsync(
        string allowedRoot,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => DeleteContentsOlderThan(allowedRoot, cutoffUtc, cancellationToken), cancellationToken);

    private static DeletionStats DeleteContents(string allowedRoot, CancellationToken cancellationToken)
    {
        PathGuard.EnsureSafeRoot(allowedRoot);
        if (!Directory.Exists(allowedRoot))
            return new DeletionStats(0, 0, 0);

        long bytes = 0, deleted = 0, skipped = 0;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(allowedRoot).ToArray(); }
        catch { return new DeletionStats(0, 0, 1); }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PathGuard.IsInside(entry, allowedRoot))
            {
                skipped++;
                continue;
            }

            try
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skipped++;
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                    DeleteDirectoryTree(entry, allowedRoot, ref bytes, ref deleted, ref skipped, cancellationToken);
                else
                    DeleteFile(entry, ref bytes, ref deleted, ref skipped);
            }
            catch (UnauthorizedAccessException) { skipped++; }
            catch (IOException) { skipped++; }
        }

        return new DeletionStats(bytes, deleted, skipped);
    }

    private static DeletionStats DeleteContentsOlderThan(
        string allowedRoot,
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        PathGuard.EnsureSafeRoot(allowedRoot);
        if (!Directory.Exists(allowedRoot))
            return new DeletionStats(0, 0, 0);

        long bytes = 0, deleted = 0, skipped = 0;
        var directories = new List<string>();
        var stack = new Stack<string>();
        stack.Push(allowedRoot);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            if (!PathGuard.IsInside(current, allowedRoot) &&
                !string.Equals(PathGuard.Canonicalize(current), PathGuard.Canonicalize(allowedRoot), StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            directories.Add(current);
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTimeUtc > cutoffUtc)
                            continue;
                        DeleteFile(file, ref bytes, ref deleted, ref skipped);
                    }
                    catch (UnauthorizedAccessException) { skipped++; }
                    catch (IOException) { skipped++; }
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attr = File.GetAttributes(dir);
                        if ((attr & FileAttributes.ReparsePoint) != 0)
                        {
                            skipped++;
                            continue;
                        }
                        if (PathGuard.IsInside(dir, allowedRoot))
                            stack.Push(dir);
                        else
                            skipped++;
                    }
                    catch (UnauthorizedAccessException) { skipped++; }
                    catch (IOException) { skipped++; }
                }
            }
            catch (UnauthorizedAccessException) { skipped++; }
            catch (IOException) { skipped++; }
        }

        foreach (var dir in directories.OrderByDescending(x => x.Length))
        {
            if (string.Equals(PathGuard.Canonicalize(dir), PathGuard.Canonicalize(allowedRoot), StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir, false);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return new DeletionStats(bytes, deleted, skipped);
    }

    private static DeletionStats DeleteMatchingFiles(string allowedRoot, string pattern, CancellationToken cancellationToken)
    {
        PathGuard.EnsureSafeRoot(allowedRoot);
        if (!Directory.Exists(allowedRoot))
            return new DeletionStats(0, 0, 0);

        long bytes = 0, deleted = 0, skipped = 0;
        foreach (var file in Directory.EnumerateFiles(allowedRoot, pattern, SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PathGuard.IsInside(file, allowedRoot))
            {
                skipped++;
                continue;
            }
            DeleteFile(file, ref bytes, ref deleted, ref skipped);
        }

        return new DeletionStats(bytes, deleted, skipped);
    }

    private static void DeleteDirectoryTree(
        string directory,
        string allowedRoot,
        ref long bytes,
        ref long deleted,
        ref long skipped,
        CancellationToken cancellationToken)
    {
        if (!PathGuard.IsInside(directory, allowedRoot))
        {
            skipped++;
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeleteFile(file, ref bytes, ref deleted, ref skipped);
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var attr = File.GetAttributes(child);
                    if ((attr & FileAttributes.ReparsePoint) != 0)
                    {
                        skipped++;
                        continue;
                    }
                    DeleteDirectoryTree(child, allowedRoot, ref bytes, ref deleted, ref skipped, cancellationToken);
                }
                catch (UnauthorizedAccessException) { skipped++; }
                catch (IOException) { skipped++; }
            }

            Directory.Delete(directory, false);
        }
        catch (UnauthorizedAccessException) { skipped++; }
        catch (IOException) { skipped++; }
    }

    private static void DeleteFile(string file, ref long bytes, ref long deleted, ref long skipped)
    {
        try
        {
            long length = 0;
            try { length = new FileInfo(file).Length; } catch { }

            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);

            File.Delete(file);
            bytes += length;
            deleted++;
        }
        catch (UnauthorizedAccessException) { skipped++; }
        catch (IOException) { skipped++; }
    }
}
