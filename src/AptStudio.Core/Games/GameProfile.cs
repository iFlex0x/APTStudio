namespace AptStudio.Core.Games;

public sealed class GameProfile
{
    private static readonly string[] DataFolderNames = ["DATAS", "FONT", "LEVELS"];
    private static readonly string[] ArchiveFolderNames = ["DATAS", "FONT", "LEVELS", "RTC", "RTE"];

    private GameProfile(
        GameKind kind,
        string root,
        IReadOnlyList<string> dataRoots,
        IReadOnlyList<string> assetRoots,
        string executableName)
    {
        Kind = kind;
        Root = root;
        DataRoots = dataRoots;
        AssetRoots = assetRoots;
        ExecutableName = executableName;
    }

    public GameKind Kind { get; }

    public string Root { get; }

    public IReadOnlyList<string> DataRoots { get; }

    public IReadOnlyList<string> AssetRoots { get; }

    public string ExecutableName { get; }

    public static bool TryDetect(string? root, out GameProfile? profile, out string? error)
    {
        profile = null;
        error = null;

        if (string.IsNullOrWhiteSpace(root))
        {
            error = "Pick a game folder.";
            return false;
        }

        string fullRoot;
        try
        {
            fullRoot = Path.GetFullPath(root);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "That path is not valid.";
            return false;
        }

        if (!Directory.Exists(fullRoot))
        {
            error = "Folder not found.";
            return false;
        }

        if (File.Exists(Path.Combine(fullRoot, "APlagueTaleRequiem_x64.exe")))
        {
            error = "Requiem is not supported yet.";
            return false;
        }

        if (!File.Exists(Path.Combine(fullRoot, "Resonance.exe")))
        {
            error = "Resonance.exe was not found in that folder.";
            return false;
        }

        foreach (string folder in DataFolderNames)
        {
            if (!Directory.Exists(Path.Combine(fullRoot, folder)))
            {
                error = $"{folder} was not found in that folder.";
                return false;
            }
        }

        profile = new GameProfile(
            GameKind.Resonance,
            fullRoot,
            Array.AsReadOnly(DataFolderNames),
            Array.AsReadOnly(ArchiveFolderNames.Where(folder =>
                Directory.Exists(Path.Combine(fullRoot, folder))).ToArray()),
            "Resonance.exe");
        return true;
    }
}
