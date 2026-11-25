namespace AptStudio.Core.Assets;

// CRC-64/ECMA-182, non-reflected, init 0. The game lowercases names before hashing.
public static class AsoboHash
{
    private const ulong Polynomial = 0x42F0E1EBA9EA3693;

    public static ulong Compute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ulong crc = 0;
        foreach (char c in text)
        {
            byte value = c is >= 'A' and <= 'Z' ? (byte)(c + 32) : (byte)c;
            crc ^= (ulong)value << 56;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000000000000000) != 0
                    ? (crc << 1) ^ Polynomial
                    : crc << 1;
            }
        }

        return crc;
    }
}
