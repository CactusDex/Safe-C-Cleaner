namespace SafeCCleaner.Core.Safety;

public static class PathGuard
{
    public static string Canonicalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static void EnsureSafeRoot(string root)
    {
        var canonical = Canonicalize(root);
        var pathRoot = Path.GetPathRoot(canonical)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(canonical, pathRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to operate on filesystem root: {canonical}");

        if (canonical.Length < 8)
            throw new InvalidOperationException($"Refusing suspiciously short cleanup root: {canonical}");

        foreach (var exact in GetProtectedExactRoots())
        {
            if (!string.IsNullOrWhiteSpace(exact) &&
                string.Equals(canonical, Canonicalize(exact), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing protected cleanup root: {canonical}");
        }

        foreach (var subtree in GetProtectedSubtrees())
        {
            if (string.IsNullOrWhiteSpace(subtree))
                continue;

            var protectedPath = Canonicalize(subtree);
            if (string.Equals(canonical, protectedPath, StringComparison.OrdinalIgnoreCase) ||
                IsInside(canonical, protectedPath))
                throw new InvalidOperationException($"Refusing protected system subtree: {canonical}");
        }

        if (Directory.Exists(canonical))
        {
            var attributes = File.GetAttributes(canonical);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Refusing cleanup root that is a reparse point: {canonical}");
        }
    }

    public static bool IsInside(string candidate, string allowedRoot)
    {
        var root = Canonicalize(allowedRoot) + Path.DirectorySeparatorChar;
        var full = Canonicalize(candidate) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetProtectedExactRoots()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        yield return windows;
        yield return programData;
        yield return user;
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(user))
            yield return Path.Combine(user, "Downloads");
    }

    private static IEnumerable<string> GetProtectedSubtrees()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrWhiteSpace(windows))
        {
            yield return Path.Combine(windows, "System32");
            yield return Path.Combine(windows, "WinSxS");
            yield return Path.Combine(windows, "Installer");
        }

        yield return programFiles;
        yield return programFilesX86;
    }
}
