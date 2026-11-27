namespace AptStudio.Desktop.Browser;

internal sealed record AssetRow(string Key, string[] Cells, object? Tag);

internal sealed record AssetColumn(
    string Header,
    int Width,
    bool Flexible = false,
    bool Right = false,
    bool Mono = false,
    bool Muted = false,
    bool MiddleEllipsis = false);
