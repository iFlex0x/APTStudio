// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace AptStudio.Core.Skeletons;

public sealed record SkeletonHead(
    ulong ClassHash,
    ulong NameHash,
    ulong LinkHash,
    uint LinkSize,
    uint BodySize,
    uint StoredSize,
    ushort Version)
{
    public const int HeaderSize = 0x33;
    public const int ExtraSize = 1;
    public const int MaxLinkSize = 256;
    public const uint MaxBodySize = 64 * 1024 * 1024;
    public const ulong SkeletonClass = 0xE79F968CBA6651AC;

    public long PayloadOffset => HeaderSize + LinkSize + ExtraSize;

    public long TotalSize => PayloadOffset + StoredSize;

    public static SkeletonHead Parse(ReadOnlySpan<byte> header, string source)
    {
        if (header.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"{source}: skeleton header is {header.Length} bytes, need {HeaderSize}.");
        }

        ulong classHash = BinaryPrimitives.ReadUInt64LittleEndian(header);
        if (classHash != SkeletonClass)
        {
            throw new InvalidDataException(
                $"{source}: class 0x{classHash:X16} is not Skel_Z.");
        }

        ulong nameHash = BinaryPrimitives.ReadUInt64LittleEndian(header[0x08..]);
        if (nameHash == 0)
        {
            throw new InvalidDataException($"{source}: skeleton name is zero.");
        }

        uint linkSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x24..]);
        uint bodySize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x28..]);
        uint storedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x2C..]);
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header[0x30..]);
        byte storage = header[0x32];
        if (version != 1)
        {
            throw new InvalidDataException($"{source}: skeleton version {version} is not measured.");
        }

        if (storage != 2)
        {
            throw new InvalidDataException($"{source}: skeleton storage {storage} is not measured.");
        }

        if (linkSize > MaxLinkSize)
        {
            throw new InvalidDataException($"{source}: link size {linkSize} is too large.");
        }

        uint maxStored = bodySize + bodySize / 255 + 16;
        if (bodySize is 0 or > MaxBodySize || storedSize <= 8 || storedSize > maxStored)
        {
            throw new InvalidDataException(
                $"{source}: body {bodySize} and stored {storedSize} sizes are not valid.");
        }

        return new SkeletonHead(
            classHash,
            nameHash,
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x10..]),
            linkSize,
            bodySize,
            storedSize,
            version);
    }
}
