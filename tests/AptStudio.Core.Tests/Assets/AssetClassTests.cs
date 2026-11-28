// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Assets;

namespace AptStudio.Core.Tests.Assets;

public sealed class AssetClassTests
{
    [Theory]
    [InlineData("Bitmap_Z", AssetClass.Bitmap)]
    [InlineData("Mesh_Z", AssetClass.Mesh)]
    [InlineData("Skin_Z", AssetClass.Skin)]
    [InlineData("Animation_Z", AssetClass.Animation)]
    [InlineData("Material_Z", AssetClass.Material)]
    public void NamesKnownClasses(string name, ulong hash)
    {
        Assert.Equal(name, AssetClass.Name(hash));
        Assert.Equal(hash, AsoboHash.Compute(name));
    }

    [Fact]
    public void KeepsUnknownHashesVisible()
    {
        Assert.Equal("0x0123456789ABCDEF", AssetClass.Name(0x0123456789ABCDEF));
    }
}
