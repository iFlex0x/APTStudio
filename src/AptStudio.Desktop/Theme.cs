// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AptStudio.Desktop;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(30, 30, 30);
    public static readonly Color Surface = Color.FromArgb(37, 37, 38);
    public static readonly Color Text = Color.FromArgb(230, 230, 230);
    public static readonly Color Muted = Color.FromArgb(150, 150, 150);
    public static readonly Color Accent = Color.FromArgb(79, 195, 247);
    public static readonly Color Border = Color.FromArgb(60, 60, 60);
    public static readonly Color GridHeader = Color.FromArgb(45, 45, 48);
    public static readonly Color GridAlt = Color.FromArgb(34, 34, 34);
    public static readonly Color Selection = Color.FromArgb(9, 71, 113);
    public static readonly Color SelectionInactive = Color.FromArgb(55, 55, 61);
    public static readonly Color CheckerLight = Color.FromArgb(56, 56, 56);
    public static readonly Color CheckerDark = Color.FromArgb(42, 42, 42);
    public static readonly Color Error = Color.FromArgb(224, 108, 117);

    public static Icon AppIcon { get; } = LoadAppIcon();
    public static Bitmap AppMark { get; } = LoadAppMark();

    private static Bitmap LoadAppMark()
    {
        using Stream stream = typeof(Theme).Assembly.GetManifestResourceStream(
            "AptStudio.Desktop.Mark.png")
            ?? throw new InvalidOperationException("The APTStudio mark is missing.");
        using Image image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                ?? SystemIcons.Application;
        }
        catch (ArgumentException)
        {
            return SystemIcons.Application;
        }
    }
}
