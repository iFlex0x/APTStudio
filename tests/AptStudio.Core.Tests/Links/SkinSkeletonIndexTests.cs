// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Links;
using AptStudio.Core.Skins;
using AptStudio.Core.Tests.Skins;

namespace AptStudio.Core.Tests.Links;

public sealed class SkinSkeletonIndexTests
{
    [Fact]
    public void BuildsSkeletonIndex()
    {
        string path = SkinObject(0xAA);
        try
        {
            SkinSkeletonIndex index = SkinSkeletonIndex.Build([Entry(path, "A.DPC")]);

            Assert.Single(index.Entries);
            Assert.Equal(1, index.BoundCount);
            Assert.Equal(0, index.NoSkeleton);
            Assert.Equal(6u, index.Entries[0].Kind);
            Assert.Single(index.BySkeleton[0xAA]);
            Assert.Empty(index.Failures);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CountsMeshOnlyBodies()
    {
        string path = SkinObject(0);
        try
        {
            SkinSkeletonIndex index = SkinSkeletonIndex.Build([Entry(path, "A.DPC")]);

            Assert.Single(index.Entries);
            Assert.Equal(0, index.BoundCount);
            Assert.Equal(1, index.NoSkeleton);
            Assert.Empty(index.BySkeleton);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DedupesByBody()
    {
        string path = SkinObject(0xAA);
        try
        {
            SkinSkeletonIndex index = SkinSkeletonIndex.Load(
                [Entry(path, "A.DPC"), Entry(path, "B.DPC")]);

            Assert.Single(index.Entries);
            Assert.Equal("A.DPC", index.Entries[0].Row.ArchivePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RecordsFailure()
    {
        string path = Path.Combine(
            Path.GetTempPath(), $"aptstudio-skin-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, new byte[0x80]);
        try
        {
            SkinSkeletonIndex index = SkinSkeletonIndex.Build([Entry(path, "A.DPC")]);

            Assert.Empty(index.Entries);
            Assert.Single(index.Failures);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static AssetEntry Entry(string bodyPath, string archive)
    {
        byte[] bytes = File.ReadAllBytes(bodyPath);
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
        return new AssetEntry(archive, bodyPath, false, row, null, null);
    }

    private static string SkinObject(ulong root)
    {
        byte[] block = Block(root);
        byte[] head = SkinTestData.Header(0x1234, linkSize: (uint)block.Length);
        byte[] bytes = new byte[head.Length + block.Length];
        head.CopyTo(bytes, 0);
        block.CopyTo(bytes, head.Length);
        string path = Path.Combine(
            Path.GetTempPath(), $"aptstudio-skin-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Block(ulong root)
    {
        ulong[] entries = root != 0 ? [root, 0xBB] : [0xBB];
        int linkSize = 0x10 + entries.Length * 8 + 8;
        byte[] block = new byte[linkSize];
        BinaryPrimitives.WriteUInt64LittleEndian(block, 0x9999);
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
