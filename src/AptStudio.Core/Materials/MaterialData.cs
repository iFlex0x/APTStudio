// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;

namespace AptStudio.Core.Materials;

public sealed record MaterialSlot(int Index, ulong TextureHash)
{
    public MaterialSlotRole Role => MaterialSlotRoles.Role(Index);
}

public sealed record MaterialData(
    MaterialHead Head,
    uint Version,
    string Code,
    ulong Mask,
    uint Refs,
    IReadOnlyList<MaterialSlot> Slots)
{
    public const int MinBodySize = 0x36C;

    private const int VersionOffset = 0x130;
    private const int CodeLengthOffset = 0x18C;
    private const int CodeOffset = 0x190;
    private const int TableSize = 12 + MaterialSlotRoles.Count * 8;

    public bool IsAnimation => Head.IsAnimation;

    public static MaterialData Parse(MaterialHead head, byte[] body, string source)
    {
        if (body.Length < MinBodySize)
        {
            throw new InvalidDataException(
                $"{source}: material body is {body.Length} bytes, need {MinBodySize}.");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(VersionOffset));
        if (version != 5)
        {
            throw new InvalidDataException($"{source}: material version {version} is not measured.");
        }

        int codeLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(CodeLengthOffset));
        if (codeLength is < 1 or > 256 || CodeOffset + codeLength > body.Length)
        {
            throw new InvalidDataException($"{source}: code length {codeLength} is invalid.");
        }

        if (body[CodeOffset + codeLength - 1] != 0)
        {
            throw new InvalidDataException($"{source}: code string is not NUL-terminated.");
        }

        int tableAt = CodeOffset + codeLength;
        if (tableAt + TableSize > body.Length)
        {
            throw new InvalidDataException(
                $"{source}: bitmap table at {tableAt} overruns {body.Length} bytes.");
        }

        ulong mask = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(tableAt));
        uint refs = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(tableAt + 8));
        ulong bitset = 0;
        List<MaterialSlot> slots = new();
        for (int index = 0; index < MaterialSlotRoles.Count; index++)
        {
            ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(
                body.AsSpan(tableAt + 12 + index * 8));
            if (hash == 0)
            {
                continue;
            }

            bitset |= 1UL << index;
            slots.Add(new MaterialSlot(index, hash));
        }

        if ((mask & bitset) != bitset)
        {
            throw new InvalidDataException(
                $"{source}: bitmap mask 0x{mask:X16} is missing bits 0x{bitset & ~mask:X16}.");
        }

        string code = Encoding.ASCII.GetString(body, CodeOffset, codeLength - 1);
        return new MaterialData(head, version, code, mask, refs, slots);
    }
}
