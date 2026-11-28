// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Containers;

namespace AptStudio.Core.Skins;

public sealed record SkinBinding(
    uint Kind,
    IReadOnlyList<ulong> Entries,
    ulong SkeletonRoot)
{
    public const int MaxEntries = 4096;
    public const uint MinLinkSize = 0x18;
    public const uint MaxLinkSize = 4096;

    public bool HasSkeleton => SkeletonRoot != 0;

    public static SkinBinding Read(Stream stream, string archivePath, DpcEntry row)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(row);
        if (row.ClassHash != SkinHead.SkinClass)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(archivePath)} 0x{row.NameHash:X16}: class is not Skin_Z.");
        }

        string source = $"{Path.GetFileName(archivePath)} 0x{row.NameHash:X16}";
        byte[] header = ReadExactly(stream, row.BodyOffset, SkinHead.HeaderSize, source);
        SkinHead head = SkinHead.Parse(header, source);
        if (head.NameHash != row.NameHash)
        {
            throw new InvalidDataException(
                $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
        }

        if (head.LinkSize is < MinLinkSize or > MaxLinkSize
            || SkinHead.HeaderSize + head.LinkSize > row.CompressedSize)
        {
            throw new InvalidDataException(
                $"{source}: link size {head.LinkSize} does not fit in the row.");
        }

        byte[] block = ReadExactly(
            stream,
            row.BodyOffset + SkinHead.HeaderSize,
            (int)head.LinkSize,
            source);
        return Parse(block, head.LinkSize, source);
    }

    public static SkinBinding Parse(ReadOnlySpan<byte> block, uint linkSize, string source)
    {
        if (linkSize is < MinLinkSize or > MaxLinkSize || block.Length < linkSize)
        {
            throw new InvalidDataException(
                $"{source}: skin link block is {block.Length} bytes, expected {linkSize}.");
        }

        uint kind = BinaryPrimitives.ReadUInt32LittleEndian(block[0x08..]);
        if (kind != 6)
        {
            throw new InvalidDataException(
                $"{source}: skin link kind {kind} is not 6.");
        }

        uint entryCount = BinaryPrimitives.ReadUInt32LittleEndian(block[0x0C..]);
        long rootAt = 0x10L + (long)entryCount * 8;
        if (entryCount > MaxEntries || rootAt + 8 > linkSize)
        {
            throw new InvalidDataException(
                $"{source}: {entryCount} skin link entries do not fit in {linkSize} bytes.");
        }

        ulong[] entries = new ulong[entryCount];
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index] = BinaryPrimitives.ReadUInt64LittleEndian(
                block[(0x10 + index * 8)..]);
        }

        ulong root = BinaryPrimitives.ReadUInt64LittleEndian(block[(int)rootAt..]);
        if (root != 0 && (entries.Length == 0 || entries[0] != root))
        {
            throw new InvalidDataException(
                $"{source}: skeleton root 0x{root:X16} is not the first link entry.");
        }

        return new SkinBinding(kind, entries, root);
    }

    private static byte[] ReadExactly(Stream stream, long offset, int count, string source)
    {
        byte[] buffer = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n == 0)
            {
                throw new InvalidDataException($"{source}: short read at {offset}.");
            }

            read += n;
        }

        return buffer;
    }
}
