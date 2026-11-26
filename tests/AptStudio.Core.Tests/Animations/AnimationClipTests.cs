using AptStudio.Core.Animations;

namespace AptStudio.Core.Tests.Animations;

public sealed class AnimationClipTests
{
    private const int StreamOffset = 16;
    private const ulong BoneHash = 0xAAAA111122223333;

    [Fact]
    public void DecodesSingleGroupAsAbsoluteLocals()
    {
        const int bits = 8;
        const uint length = 142 + 93 * bits;
        byte[] body = Body(length);
        long stream = StreamOffset * 8L;
        Write(body, stream, 16, 32768);
        Write(body, stream + 16, 16, 32768);
        Write(body, stream + 32, 16, 32768);
        for (int frame = 0; frame < 31; frame++)
        {
            Write(body, stream + 48 + (frame * 3 + 0) * bits, bits, 1);
            Write(body, stream + 48 + (frame * 3 + 1) * bits, bits, 0x81);
            Write(body, stream + 48 + (frame * 3 + 2) * bits, bits, 0);
        }

        AnimationClip clip = AnimationClipReader.Read(Data(32, length), body);

        Assert.Equal(32, clip.FrameCount);
        Assert.Equal(1, clip.GroupCount);
        Assert.Single(clip.Bones);
        Assert.Equal(bits, clip.Bones[0].ValueBits);
        Assert.Equal(32, clip.Bones[0].Rotations.Count);
        Assert.True(clip.Bones[0].Rotations[^1].X > clip.Bones[0].Rotations[0].X);
        Assert.True(clip.Bones[0].Rotations[^1].Y < clip.Bones[0].Rotations[0].Y);
    }

    [Fact]
    public void DecodesOneContinuousStreamAcrossGroups()
    {
        const int bits = 4;
        const uint keys = 63;
        const uint length = 48 * 2 + 94 + 3 * (keys - 2) * bits;
        byte[] body = Body(length);
        long stream = StreamOffset * 8L;
        Write(body, stream, 16, 32768);
        Write(body, stream + 16, 16, 32768);
        Write(body, stream + 32, 16, 32768);

        AnimationClip clip = AnimationClipReader.Read(Data(keys, length), body);

        Assert.Equal(2, clip.GroupCount);
        Assert.Equal(62, clip.FrameCount);
        Assert.Equal(62, clip.Bones[0].Rotations.Count);
    }

    [Fact]
    public void StaticBlockKeepsOneKey()
    {
        byte[] body = Body(142);
        long stream = StreamOffset * 8L;
        Write(body, stream, 16, 32768);
        Write(body, stream + 16, 16, 32768);
        Write(body, stream + 32, 16, 32768);

        AnimationClip clip = AnimationClipReader.Read(Data(63, 142), body);

        Assert.Equal(62, clip.FrameCount);
        Assert.Single(clip.Bones[0].Rotations);
    }

    [Fact]
    public void RejectsBlocksOutsideTheLengthLaw()
    {
        byte[] body = Body(150);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => AnimationClipReader.Read(Data(63, 150), body));

        Assert.Contains("no readable rotation blocks", error.Message);
    }

    [Fact]
    public void RejectsFrameEncodedBodies()
    {
        byte[] body = Body(0);
        AnimationData data = new(
            AnimationHead.Parse(AnimationTestData.Header(0x1234), "test"),
            AnimationEncoding.Frames,
            1f,
            30,
            0,
            0,
            -1,
            [],
            0,
            0,
            0);

        Assert.Throws<InvalidDataException>(() => AnimationClipReader.Read(data, body));
    }

    private static AnimationData Data(uint keys, uint length) => new(
        AnimationHead.Parse(AnimationTestData.Header(0x1234), "test"),
        AnimationEncoding.Packed,
        1f,
        keys,
        length,
        0,
        StreamOffset,
        [new AnimationBone(BoneHash, 0, length)],
        0,
        0,
        0);

    private static byte[] Body(uint bits) =>
        new byte[StreamOffset + (int)((bits + 7) / 8) + AnimationData.FooterSize];

    private static void Write(byte[] body, long bit, int width, uint value)
    {
        for (int index = 0; index < width; index++)
        {
            if (((value >> index) & 1) == 0)
            {
                continue;
            }

            int at = (int)((bit + index) >> 3);
            int offset = (int)((bit + index) & 7);
            body[at] |= (byte)(1 << offset);
        }
    }
}
