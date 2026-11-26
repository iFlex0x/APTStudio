using System.Buffers.Binary;
using AptStudio.Core.Skeletons;

namespace AptStudio.Core.Tests.Skeletons;

public sealed class SkeletonScannerTests
{
    [Fact]
    public void FindsSkeletonObject()
    {
        byte[] obj = SkeletonTestData.Object(SkeletonTestData.Body(new TestBone(0x11)));
        byte[] file = new byte[0x2000 + obj.Length];
        file[0x100] = 0;
        obj.CopyTo(file, 0x2000);
        string path = SkeletonTestData.TempFile(file);
        try
        {
            IReadOnlyList<SkeletonHit> hits = SkeletonScanner.Scan(path);

            SkeletonHit hit = Assert.Single(hits);
            Assert.Equal(0x2000, hit.Offset);
            Assert.Equal(SkeletonTestData.Name, hit.NameHash);
            Assert.Equal((uint)obj.Length, hit.TotalSize);
            Assert.Equal(1, hit.Version);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SkipsCommonArchives()
    {
        byte[] obj = SkeletonTestData.Object(SkeletonTestData.Body(new TestBone(0x11)));
        byte[] file = new byte[0x2000 + obj.Length];
        file[0x100] = 2;
        obj.CopyTo(file, 0x2000);
        string path = SkeletonTestData.TempFile(file);
        try
        {
            Assert.Empty(SkeletonScanner.Scan(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IgnoresTruncatedObject()
    {
        byte[] obj = SkeletonTestData.Object(SkeletonTestData.Body(new TestBone(0x11)));
        byte[] file = new byte[0x2000 + 0x40];
        file[0x100] = 0;
        obj.AsSpan(0, 0x40).CopyTo(file.AsSpan(0x2000));
        string path = SkeletonTestData.TempFile(file);
        try
        {
            Assert.Empty(SkeletonScanner.Scan(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CacheRoundTripsAndReopensChangedFile()
    {
        byte[] obj = SkeletonTestData.Object(SkeletonTestData.Body(new TestBone(0x11)));
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
            IReadOnlyList<SkeletonHit> first = SkeletonCache.LoadOrScan(
                path, false, out bool cached, root);
            Assert.False(cached);
            Assert.Single(first);

            IReadOnlyList<SkeletonHit> second = SkeletonCache.LoadOrScan(
                path, false, out cached, root);
            Assert.True(cached);
            Assert.Equal(first.Single().Offset, second.Single().Offset);
            Assert.Equal(first.Single().TotalSize, second.Single().TotalSize);

            byte[] changed = new byte[file.Length + 1];
            file.CopyTo(changed, 0);
            File.WriteAllBytes(path, changed);
            SkeletonCache.LoadOrScan(path, false, out cached, root);
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
        byte[] obj = SkeletonTestData.Object(SkeletonTestData.Body(new TestBone(0x11)));
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
            SkeletonCache.LoadOrScan(path, false, out bool cached, root);
            Assert.False(cached);
            string cache = Directory.GetFiles(root, "*.skel").Single();
            byte[] bytes = File.ReadAllBytes(cache);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0);
            File.WriteAllBytes(cache, bytes);

            SkeletonCache.LoadOrScan(path, false, out cached, root);
            Assert.False(cached);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
