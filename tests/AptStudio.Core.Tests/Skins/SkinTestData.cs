using System.Buffers.Binary;
using System.Text;

namespace AptStudio.Core.Tests.Skins;

internal static class SkinTestData
{
    public const uint VertexFlags = 0x00200102;

    public static byte[] Header(
        ulong name,
        uint linkSize = 12,
        uint bodySize = 256,
        uint storedSize = 128,
        ushort version = 11,
        ulong classHash = 0x53E24BDB95306A09)
    {
        byte[] header = new byte[0x33];
        BinaryPrimitives.WriteUInt64LittleEndian(header, classHash);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), name);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x10), name);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x24), linkSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x28), bodySize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x2C), storedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x30), version);
        header[0x32] = 2;
        return header;
    }

    public static byte[] Body(params TestPart[] parts) => BodyWithBones(
        parts,
        parts.Select((_, index) => (0x2000UL + (ulong)index, index)).ToArray());

    public static byte[] BodyWithBones(TestPart[] parts, (ulong Name, int Index)[] bones)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write((uint)parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            writer.Write(0x1000UL + (ulong)i);
        }

        writer.Write((uint)parts.Length);
        foreach (TestPart part in parts)
        {
            if (part.Vertices == 0)
            {
                continue;
            }

            writer.Write((uint)part.Packets.Length);
            foreach (ulong packet in part.Packets)
            {
                writer.Write(packet);
                writer.Write(0u);
                writer.Write(0u);
            }

            writer.Write(1u);
            writer.Write((uint)part.Vertices);
            writer.Write((uint)part.Stride);
            writer.Write(VertexFlags);
            writer.Write(part.Stream);
            writer.Write(part.Weights);
            writer.Write(new byte[60]);
        }

        writer.Write((uint)bones.Length);
        foreach ((ulong name, int index) in bones)
        {
            writer.Write(name);
            writer.Write(index);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static byte[] Weights16(int vertices, ushort bone = 3)
    {
        byte[] data = new byte[vertices * 16];
        for (int v = 0; v < vertices; v++)
        {
            int at = v * 16;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), bone);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at + 2), (ushort)(bone + 1));
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at + 8), 0x3FFF);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at + 10), 0xBFFF);
        }

        return data;
    }

    public static byte[] Weights32(int vertices, ushort bone = 7)
    {
        byte[] data = new byte[vertices * 32];
        for (int v = 0; v < vertices; v++)
        {
            int at = v * 32;
            for (int j = 0; j < 8; j++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(
                    data.AsSpan(at + j * 2), (ushort)(bone + j));
                BinaryPrimitives.WriteUInt16LittleEndian(
                    data.AsSpan(at + 16 + j * 2), (ushort)(0x2000));
            }
        }

        return data;
    }

    public static byte[] Rigid(int vertices, uint bone = 2)
    {
        byte[] data = new byte[vertices * 12];
        for (int v = 0; v < vertices; v++)
        {
            int at = v * 12;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), bone);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(at + 8), 1f);
        }

        return data;
    }
}

internal sealed record TestPart(
    int Vertices,
    int Stride,
    ulong Stream,
    ulong[] Packets,
    byte[] Weights);
