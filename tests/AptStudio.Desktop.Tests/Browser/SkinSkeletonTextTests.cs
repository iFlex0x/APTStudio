// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Skins;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class SkinSkeletonTextTests
{
    [Fact]
    public void ExplicitRootIsNamed()
    {
        SkinBinding binding = new(6, [0x314127CD0F66EADA], 0x314127CD0F66EADA);

        Assert.Equal(
            "skeleton 0x314127CD0F66EADA",
            SkinSkeletonText.Text(binding, null));
    }

    [Fact]
    public void RootZeroIsExplicitlyNoBinding()
    {
        SkinBinding binding = new(6, [], 0);

        Assert.Equal("No explicit skeleton binding", SkinSkeletonText.Text(binding, null));
    }

    [Fact]
    public void BindingErrorIsReportedSeparately()
    {
        Assert.Equal(
            "Skin binding unavailable",
            SkinSkeletonText.Text(null, "bad link block"));
    }

    [Fact]
    public void MissingNonzeroRootHasItsOwnMessage()
    {
        Assert.Equal(
            "Skeleton 0x314127CD0F66EADA was not found.",
            SkinSkeletonText.Missing(0x314127CD0F66EADA));
    }
}
