// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class AnimationLookupTests
{
    [Fact]
    public void SameBodyResolvesTheSourceArchiveOccurrence()
    {
        AssetEntry common = Entry(@"C:\game\DATAS\COMMON.DPC", @"C:\game\DATAS\COMMON.DPC");
        AssetEntry abyss = Entry(@"C:\game\LEVELS\ABYSS.DPC", @"C:\game\DATAS\COMMON.DPC");
        AssetEntry[] assets = [common, abyss];
        AnimationOccurrence occurrence = new(
            0x00040ED3868672F5,
            @"C:\game\LEVELS\ABYSS.DPC",
            @"C:\game\DATAS\COMMON.DPC",
            3200);

        AssetEntry? found = BrowserForm.FindAnimationEntry(assets, occurrence);

        Assert.Same(abyss, found);
    }

    [Fact]
    public void DifferentBodyOffsetDoesNotMatch()
    {
        AssetEntry abyss = Entry(@"C:\game\LEVELS\ABYSS.DPC", @"C:\game\DATAS\COMMON.DPC");
        AnimationOccurrence occurrence = new(
            0x00040ED3868672F5,
            @"C:\game\LEVELS\ABYSS.DPC",
            @"C:\game\DATAS\COMMON.DPC",
            6400);

        Assert.Null(BrowserForm.FindAnimationEntry([abyss], occurrence));
    }

    private static AssetEntry Entry(string archivePath, string bodyArchivePath) => new(
        archivePath,
        bodyArchivePath,
        archivePath != bodyArchivePath,
        new DpcEntry(0x00040ED3868672F5, AssetClass.Animation, 0, 100, 1, 0, 0, 0, 0), null,
        null);
}
