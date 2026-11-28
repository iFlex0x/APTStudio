// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using AptStudio.Core.Assets;

namespace AptStudio.Core.Tests.Assets;

public sealed class AsoboHashTests
{
    [Theory]
    [InlineData("bitmap_z", "E9659CD1C3F3326D")]
    [InlineData("mesh_z", "7703A856BA5776DD")]
    [InlineData("skin_z", "53E24BDB95306A09")]
    [InlineData("animation_z", "622C802CCCFE934F")]
    [InlineData("material_z", "310BB7F1CFC1D4FF")]
    public void MatchesMeasuredClassHashes(string name, string expectedHex)
    {
        ulong expected = ulong.Parse(expectedHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        Assert.Equal(expected, AsoboHash.Compute(name));
    }

    [Fact]
    public void LowercasesAsciiBeforeHashing()
    {
        Assert.Equal(AsoboHash.Compute("bitmap_z"), AsoboHash.Compute("Bitmap_Z"));
        Assert.Equal(AsoboHash.Compute("mesh_z"), AsoboHash.Compute("MESH_Z"));
    }

    [Fact]
    public void RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => AsoboHash.Compute(null!));
    }
}
