// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Materials;

namespace AptStudio.Core.Tests.Materials;

public sealed class MaterialScannerTests
{
    [Fact]
    public void FindsMaterialMagic()
    {
        byte[] obj = MaterialTestData.Object(MaterialTestData.Body((0, 0x21), (13, 0x22)));
        byte[] file = new byte[0x2000 + obj.Length];
        file[0x100] = 0;
        obj.CopyTo(file, 0x2000);
        string path = MaterialTestData.TempFile(file);
        try
        {
            IReadOnlyList<MaterialHit> hits = MaterialScanner.Scan(path);

            MaterialHit hit = Assert.Single(hits);
            Assert.Equal(0x2000, hit.Offset);
            Assert.Equal(MaterialTestData.Name, hit.NameHash);
            Assert.Equal(MaterialHead.MaterialClass, hit.ClassHash);
            Assert.Equal((uint)obj.Length, hit.TotalSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SkipsCommonArchives()
    {
        byte[] obj = MaterialTestData.Object(MaterialTestData.Body((0, 0x31)));
        byte[] file = new byte[0x2000 + obj.Length];
        file[0x100] = 2;
        obj.CopyTo(file, 0x2000);
        string path = MaterialTestData.TempFile(file);
        try
        {
            Assert.Empty(MaterialScanner.Scan(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IgnoresTruncatedObject()
    {
        byte[] obj = MaterialTestData.Object(MaterialTestData.Body((0, 0x41)));
        byte[] file = new byte[0x2000 + 0x40];
        file[0x100] = 0;
        obj.AsSpan(0, 0x40).CopyTo(file.AsSpan(0x2000));
        string path = MaterialTestData.TempFile(file);
        try
        {
            Assert.Empty(MaterialScanner.Scan(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CacheRoundTripsAndReopensChangedFile()
    {
        byte[] obj = MaterialTestData.Object(MaterialTestData.Body((0, 0x51)));
        byte[] file = new byte[0x2000 + obj.Length];
        file[0x100] = 0;
        obj.CopyTo(file, 0x2000);
        string root = Path.Combine(
            Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "TEST.DPC");
        File.WriteAllBytes(path, file);
        try
        {
            IReadOnlyList<MaterialHit> first = MaterialCache.LoadOrScan(
                path, false, out bool cached, root);
            Assert.False(cached);
            Assert.Single(first);

            IReadOnlyList<MaterialHit> second = MaterialCache.LoadOrScan(
                path, false, out cached, root);
            Assert.True(cached);
            Assert.Equal(first.Single().Offset, second.Single().Offset);

            byte[] changed = new byte[file.Length + 1];
            file.CopyTo(changed, 0);
            File.WriteAllBytes(path, changed);
            MaterialCache.LoadOrScan(path, false, out cached, root);
            Assert.False(cached);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CacheRejectsOldParserVersion()
    {
        byte[] obj = MaterialTestData.Object(MaterialTestData.Body((0, 0x61)));
        byte[] file = new byte[0x2000 + obj.Length];
        file[0x100] = 0;
        obj.CopyTo(file, 0x2000);
        string root = Path.Combine(
            Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "TEST.DPC");
        File.WriteAllBytes(path, file);
        try
        {
            MaterialCache.LoadOrScan(path, false, out bool cached, root);
            Assert.False(cached);
            string cache = Directory.GetFiles(root, "*.mat").Single();
            byte[] bytes = File.ReadAllBytes(cache);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(15), 0);
            File.WriteAllBytes(cache, bytes);

            MaterialCache.LoadOrScan(path, false, out cached, root);
            Assert.False(cached);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
