namespace AptStudio.Desktop;

internal static class GameFolderGuard
{
    public static bool IsInside(string root, string folder)
    {
        string fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullFolder = Path.GetFullPath(folder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullFolder.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullFolder.StartsWith(
                fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
