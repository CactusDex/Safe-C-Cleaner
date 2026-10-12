namespace SafeCCleaner.Core.Services;

public sealed class DirectorySizer
{
    public Task<(long Bytes, long Files)> MeasureAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Measure(path, cancellationToken), cancellationToken);

    public Task<(long Bytes, long Files)> MeasureOlderThanAsync(
        string path,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => MeasureOlderThan(path, cutoffUtc, cancellationToken), cancellationToken);

    private static (long Bytes, long Files) Measure(string path, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
            return (0, 0);

        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return (0, 0);
        }
        catch (UnauthorizedAccessException) { return (0, 0); }
        catch (IOException) { return (0, 0); }

        long bytes = 0;
        long files = 0;
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
                    try
                    {
                        var info = new FileInfo(file);
                        bytes += info.Length;
                        files++;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(dir);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            continue;
                        stack.Push(dir);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return (bytes, files);
    }

    private static (long Bytes, long Files) MeasureOlderThan(
        string path,
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
            return (0, 0);

        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return (0, 0);
        }
        catch (UnauthorizedAccessException) { return (0, 0); }
        catch (IOException) { return (0, 0); }

        long bytes = 0;
        long files = 0;
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
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTimeUtc > cutoffUtc)
                            continue;
                        bytes += info.Length;
                        files++;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(dir);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            continue;
                        stack.Push(dir);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return (bytes, files);
    }

}
