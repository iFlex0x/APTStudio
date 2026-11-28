namespace AptStudio.Core.IO;

public static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Output path is empty.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Output path has no folder.", nameof(path));
        Directory.CreateDirectory(directory);

        // A temp name that inherits a long target name breaks Windows' 255-char component cap.
        string temp = Path.Combine(directory, $".apts-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
