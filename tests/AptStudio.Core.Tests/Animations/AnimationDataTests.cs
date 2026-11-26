using AptStudio.Core.Animations;

namespace AptStudio.Core.Tests.Animations;

public sealed class AnimationDataTests
{
    [Fact]
    public void ParsesPackedFooterAndBones()
    {
        AnimationHead head = AnimationHead.Parse(AnimationTestData.Header(0x1234), "test");
        AnimationData data = AnimationData.Parse(
            head, AnimationTestData.PackedBody(), AnimationTestData.LinkBlock(1f), "test");

        Assert.Equal(AnimationEncoding.Packed, data.Encoding);
        Assert.Equal(1f, data.Duration);
        Assert.Equal(30u, data.KeyCount);
        Assert.Equal(128u, data.PackedBits);
        Assert.Equal(28, data.PackedStreamOffset);
        Assert.Equal(2, data.Bones.Count);
        Assert.Equal(0xAAAA111122223333UL, data.Bones[0].NameHash);
        Assert.Equal(0u, data.Bones[0].BitOffset);
        Assert.Equal(64u, data.Bones[0].BitLength);
        Assert.Equal(0xBBBB444455556666UL, data.Bones[1].NameHash);
        Assert.Equal(64u, data.Bones[1].BitOffset);
        Assert.Equal(64u, data.Bones[1].BitLength);
    }

    [Fact]
    public void FallsBackToLinkDurationForFrameBodies()
    {
        AnimationHead head = AnimationHead.Parse(AnimationTestData.Header(0x1234), "test");
        AnimationData data = AnimationData.Parse(
            head, AnimationTestData.FrameBody(), AnimationTestData.LinkBlock(0.5f), "test");

        Assert.Equal(AnimationEncoding.Frames, data.Encoding);
        Assert.Equal(0.5f, data.Duration);
        Assert.Equal(4, data.FrameEntries);
        Assert.Equal(2, data.FrameTracks);
        Assert.Equal(2u, data.KeyCount);
        Assert.Empty(data.Bones);
    }

    [Fact]
    public void RejectsShortBodies()
    {
        AnimationHead head = AnimationHead.Parse(AnimationTestData.Header(0x1234), "test");
        Assert.Throws<InvalidDataException>(
            () => AnimationData.Parse(head, new byte[10], null, "test"));
    }
}
