// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Animations;

namespace AptStudio.Core.Tests.Animations;

public sealed class AnimationHeadTests
{
    [Fact]
    public void ReadsExtraAndPayload()
    {
        AnimationHead head = AnimationHead.Parse(
            AnimationTestData.Header(0x1234, linkSize: 26, extra: 11), "test");
        Assert.Equal(0x1234UL, head.NameHash);
        Assert.Equal((ushort)11, head.ExtraSize);
        Assert.Equal(0x33 + 26 + 11, head.PayloadOffset);
        Assert.Equal(AnimationStorage.Lz4, head.Storage);
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)15)]
    public void AcceptsMeasuredExtraSizes(ushort extra)
    {
        AnimationHead head = AnimationHead.Parse(AnimationTestData.Header(1, extra: extra), "test");
        Assert.Equal(extra, head.ExtraSize);
    }

    [Fact]
    public void RejectsOtherClasses()
    {
        Assert.Throws<InvalidDataException>(
            () => AnimationHead.Parse(
                AnimationTestData.Header(1, classHash: 0x7703A856BA5776DD), "test"));
    }

    [Fact]
    public void RejectsUnmeasuredExtraSize()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => AnimationHead.Parse(AnimationTestData.Header(1, extra: 16), "test"));
        Assert.Contains("extra size 16", error.Message);
    }
}
