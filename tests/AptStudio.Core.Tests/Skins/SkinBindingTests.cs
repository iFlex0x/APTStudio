using System.Buffers.Binary;
using AptStudio.Core.Containers;
using AptStudio.Core.Skins;

namespace AptStudio.Core.Tests.Skins;

public sealed class SkinBindingTests
{
    [Fact]
    public void ReadsSkeletonRootAndEntries()
    {
        byte[] block = Block(0x9999, [0xAA, 0xBB, 0xCC], 0xAA);

        SkinBinding binding = SkinBinding.Parse(block, (uint)block.Length, "test");

        Assert.Equal(6u, binding.Kind);
        Assert.True(binding.HasSkeleton);
        Assert.Equal(0xAAUL, binding.SkeletonRoot);
        Assert.Equal([0xAAUL, 0xBBUL, 0xCCUL], binding.Entries);
    }

    [Fact]
    public void ReadsMeshOnlyBlock()
    {
        byte[] block = Block(0x9999, [0xCC], 0);

        SkinBinding binding = SkinBinding.Parse(block, (uint)block.Length, "test");

        Assert.False(binding.HasSkeleton);
        Assert.Equal(0UL, binding.SkeletonRoot);
        Assert.Equal([0xCCUL], binding.Entries);
    }

    [Fact]
    public void RejectsRootThatIsNotFirstEntry()
    {
        byte[] block = Block(0x9999, [0xAA], 0xBB);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkinBinding.Parse(block, (uint)block.Length, "test"));
        Assert.Contains("root", error.Message);
    }

    [Fact]
    public void RejectsEntryCountPastBlock()
    {
        byte[] block = Block(0x9999, [0xAA], 0xAA);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(0x0C), 4097);

        Assert.Throws<InvalidDataException>(
            () => SkinBinding.Parse(block, (uint)block.Length, "test"));
    }

    [Fact]
    public void RejectsShortBlock()
    {
        Assert.Throws<InvalidDataException>(
            () => SkinBinding.Parse(new byte[0x10], 0x10, "test"));
    }

    [Fact]
    public void RejectsWrongKind()
    {
        byte[] block = Block(0x9999, [0xAA], 0xAA);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(0x08), 7);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkinBinding.Parse(block, (uint)block.Length, "test"));
        Assert.Contains("kind 7", error.Message);
    }

    [Fact]
    public void ReadsFromSkinHead()
    {
        byte[] block = Block(0x9999, [0xAA, 0xBB], 0xAA);
        byte[] head = SkinTestData.Header(0x1234, linkSize: (uint)block.Length);
        byte[] bytes = new byte[head.Length + block.Length];
        head.CopyTo(bytes, 0);
        block.CopyTo(bytes, head.Length);
        DpcEntry row = new(
            0x1234,
            SkinHead.SkinClass,
            0,
            0,
            (uint)bytes.Length,
            0,
            0,
            0,
            0);

        SkinBinding binding = SkinBinding.Read(new MemoryStream(bytes), "test", row);

        Assert.Equal(6u, binding.Kind);
        Assert.Equal(0xAAUL, binding.SkeletonRoot);
        Assert.Equal([0xAAUL, 0xBBUL], binding.Entries);
    }

    private static byte[] Block(ulong blockHash, ulong[] entries, ulong root)
    {
        int linkSize = 0x10 + entries.Length * 8 + 8;
        byte[] block = new byte[linkSize];
        BinaryPrimitives.WriteUInt64LittleEndian(block, blockHash);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(8), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(0x0C), (uint)entries.Length);
        for (int index = 0; index < entries.Length; index++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(
                block.AsSpan(0x10 + index * 8), entries[index]);
        }

        BinaryPrimitives.WriteUInt64LittleEndian(
            block.AsSpan(0x10 + entries.Length * 8), root);
        return block;
    }
}
