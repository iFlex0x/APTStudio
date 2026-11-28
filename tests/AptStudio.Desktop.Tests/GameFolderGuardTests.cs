// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Games;

namespace AptStudio.Desktop.Tests;

public sealed class GameFolderGuardTests
{
    [Theory]
    [InlineData(@"C:\game", @"C:\game", true)]
    [InlineData(@"C:\game", @"C:\game\DATAS", true)]
    [InlineData(@"C:\game", @"C:\game\DATAS\", true)]
    [InlineData(@"C:\game", @"C:\game2", false)]
    [InlineData(@"C:\game", @"C:\games\DATAS", false)]
    [InlineData(@"C:\game", @"D:\exports", false)]
    public void ClassifiesFolders(string root, string folder, bool expected)
    {
        Assert.Equal(expected, GameFolderGuard.IsInside(root, folder));
    }
}
