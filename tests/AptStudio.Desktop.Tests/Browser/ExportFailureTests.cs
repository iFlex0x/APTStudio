// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Audio;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class ExportFailureTests
{
    [Fact]
    public void AudioMessageNamesTheClipFileAndReason()
    {
        AudioEntry media = new(
            @"C:\game\SOUNDBANKS\VO_MEDIA.PC.PCK",
            "VO_MEDIA.PC.PCK",
            0x1234,
            0x1EEAB9D4,
            2497360640,
            576565,
            3,
            "french(france)",
            new WemInfo(0xFFFF, 1, 48000, 2720000, null),
            null);

        string message = ExportFailure.DescribeAudio(
            media,
            @"C:\exports\VO_MEDIA_1EEAB9D4_french.ogg",
            "The game folder is read-only for APTStudio. Choose a folder outside it.");

        Assert.Contains("The Ogg file was not written.", message);
        Assert.Contains("0x1EEAB9D4", message);
        Assert.Contains("VO_MEDIA.PC.PCK", message);
        Assert.Contains("french(france)", message);
        Assert.Contains(@"C:\exports\VO_MEDIA_1EEAB9D4_french.ogg", message);
        Assert.Contains("Choose a folder outside it.", message);
    }

    [Fact]
    public void BatchMessageKeepsCountsAndFirstFailure()
    {
        BatchExportFailure first = new(
            "GREECE_0000000000001111.png",
            @"C:\exports\GREECE_0000000000001111.png",
            "Access to the path is denied.");

        string message = ExportFailure.DescribeBatch(
            @"C:\exports", total: 10, exported: 6, unsupported: 2, failed: 2, first);

        Assert.Contains("4 of 10 textures were not written.", message);
        Assert.Contains(@"Folder: C:\exports", message);
        Assert.Contains("Exported: 6", message);
        Assert.Contains("Unsupported: 2", message);
        Assert.Contains("Failed: 2", message);
        Assert.Contains("First failure: GREECE_0000000000001111.png", message);
        Assert.Contains(@"C:\exports\GREECE_0000000000001111.png", message);
        Assert.Contains("Access to the path is denied.", message);
    }

    [Fact]
    public void BatchMessageUsesSingularWordingForOneFile()
    {
        BatchExportFailure first = new("A.png", @"C:\exports\A.png", "denied");

        string message = ExportFailure.DescribeBatch(
            @"C:\exports", total: 3, exported: 2, unsupported: 0, failed: 1, first);

        Assert.Contains("1 of 3 textures was not written.", message);
    }

    [Fact]
    public void FolderMessageNamesTheDestinationAndReason()
    {
        string message = ExportFailure.DescribeFolder(
            @"C:\game\DATAS", "The game folder is read-only for APTStudio.");

        Assert.Contains("The selected textures were not written.", message);
        Assert.Contains(@"Folder: C:\game\DATAS", message);
        Assert.Contains("The game folder is read-only for APTStudio.", message);
    }

    [Fact]
    public void TreatsPathAndIoErrorsAsExpected()
    {
        Assert.True(ExportFailure.IsExpected(new PathTooLongException()));
        Assert.True(ExportFailure.IsExpected(new IOException()));
        Assert.True(ExportFailure.IsExpected(new UnauthorizedAccessException()));
        Assert.True(ExportFailure.IsExpected(new NotSupportedException()));
        Assert.False(ExportFailure.IsExpected(new InvalidOperationException()));
    }
}
