namespace AptStudio.Core.Images;

internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    public static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        uint crc = 0xFFFFFFFF;
        crc = Append(crc, first);
        crc = Append(crc, second);
        return ~crc;
    }

    private static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (int i = 0; i < table.Length; i++)
        {
            uint value = (uint)i;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
