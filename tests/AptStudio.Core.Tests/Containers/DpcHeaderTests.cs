using System.Buffers.Binary;
using AptStudio.Core.Containers;

namespace AptStudio.Core.Tests.Containers;

public sealed class DpcHeaderTests
{
    [Fact]
    public void ParsesLittleEndianFields()
    {
        byte[] buffer = new byte[DpcHeader.Size];
        buffer[0] = 1;
        WriteU32(buffer, 1, 2);
        WriteU32(buffer, 5, 32433);
        WriteU32(buffer, 9, 9413748);
        WriteU32(buffer, 13, 32435);
        WriteU32(buffer, 17, 0);
        WriteU32(buffer, 21, 0);

        DpcHeader header = DpcHeader.Parse(buffer);

        Assert.Equal(DpcKind.Normal, header.Kind);
        Assert.Equal(2u, header.OnePle);
        Assert.Equal(32433u, header.BlockDescriptionOffset);
        Assert.Equal(9413748u, header.ResourceBlockSize);
        Assert.Equal(32435u, header.ResourceBlockOffset);
        Assert.Equal(0u, header.MapSize);
        Assert.Equal(0u, header.MapOffset);
    }

    [Fact]
    public void RejectsShortBuffer()
    {
        Assert.Throws<ArgumentException>(() => DpcHeader.Parse(new byte[DpcHeader.Size - 1]));
    }

    private static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
