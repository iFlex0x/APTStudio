// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Audio;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal static class ExportFailure
{
    public const string TextureTitle = "Export PNG failed";
    public const string AudioTitle = "Export audio failed";
    public const string BatchTitle = "Export failed";

    public static bool IsExpected(Exception exception) =>
        exception is InvalidDataException or NotSupportedException or IOException
            or UnauthorizedAccessException or ArgumentException
            or System.Security.SecurityException;

    public static string DescribeTexture(TextureOccurrence occurrence, string? path, string reason)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(reason);

        string archive = occurrence.IsExternal
            ? $"{occurrence.ArchiveName} (data in {occurrence.BodyArchiveName})"
            : occurrence.ArchiveName;
        return $"The mip 0 PNG was not written.{NewLine}{NewLine}" +
            $"Texture: 0x{occurrence.Hash:X16} · {archive}{NewLine}" +
            $"File: {Target(path)}{NewLine}{NewLine}" +
            reason;
    }

    public static string DescribeAudio(AudioEntry media, string? path, string reason)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(reason);

        return $"The Ogg file was not written.{NewLine}{NewLine}" +
            $"Audio: {media.IdText} · {media.PackName} · {media.Language}{NewLine}" +
            $"File: {Target(path)}{NewLine}{NewLine}" +
            reason;
    }

    public static string DescribeBatch(
        string folder,
        int total,
        int exported,
        int unsupported,
        int failed,
        BatchExportFailure? first)
    {
        ArgumentNullException.ThrowIfNull(folder);
        BatchExportFailure failure = first
            ?? throw new ArgumentNullException(nameof(first));

        int missing = unsupported + failed;
        string head = missing == 1
            ? $"1 of {total:N0} textures was not written."
            : $"{missing:N0} of {total:N0} textures were not written.";
        return $"{head}{NewLine}{NewLine}" +
            $"Folder: {folder}{NewLine}" +
            $"Exported: {exported:N0}{NewLine}" +
            $"Unsupported: {unsupported:N0}{NewLine}" +
            $"Failed: {failed:N0}{NewLine}{NewLine}" +
            $"First failure: {failure.FileName}{NewLine}" +
            $"{failure.Path}{NewLine}{NewLine}" +
            failure.Reason;
    }

    public static string DescribeFolder(string folder, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return $"The selected textures were not written.{NewLine}{NewLine}" +
            $"Folder: {Target(folder)}{NewLine}{NewLine}" +
            reason;
    }

    private static string Target(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "(no file selected)" : path;

    private static string NewLine => Environment.NewLine;
}

internal readonly record struct BatchExportFailure(string FileName, string Path, string Reason);
