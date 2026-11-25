using System.Buffers.Binary;

namespace AptStudio.Core.Containers;

public sealed record DpcHeader(
    byte KindRaw,
    uint OnePle,
    uint BlockDescriptionOffset,
    uint ResourceBlockSize,
    uint ResourceBlockOffset,
    uint MapSize,
    uint MapOffset)
{
    public const int Offset = 0x100;
    public const int Size = 25;

    public DpcKind Kind => (DpcKind)KindRaw;

    public static DpcHeader Parse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < Size)
        {
            throw new ArgumentException($"A DPC header needs {Size} bytes.", nameof(buffer));
        }

        return new DpcHeader(
            buffer[0],
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[1..]),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[5..]),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[9..]),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[13..]),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[17..]),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[21..]));
    }
}
