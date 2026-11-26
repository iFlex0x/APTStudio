using AptStudio.Core.Skeletons;

namespace AptStudio.Core.Tests.Skeletons;

public sealed class SkeletonHeadTests
{
    [Fact]
    public void ReadsHeader()
    {
        SkeletonHead head = SkeletonHead.Parse(
            SkeletonTestData.Header(name: 0x1234, linkSize: 20, bodySize: 256, storedSize: 128),
            "test");

        Assert.Equal(SkeletonHead.SkeletonClass, head.ClassHash);
        Assert.Equal(0x1234UL, head.NameHash);
        Assert.Equal(SkeletonTestData.Link, head.LinkHash);
        Assert.Equal(20u, head.LinkSize);
        Assert.Equal(256u, head.BodySize);
        Assert.Equal(128u, head.StoredSize);
        Assert.Equal(1, head.Version);
        Assert.Equal(0x33 + 20 + 1, head.PayloadOffset);
        Assert.Equal(0x33 + 20 + 1 + 128, head.TotalSize);
    }

    [Fact]
    public void RejectsOtherClasses()
    {
        Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(
                SkeletonTestData.Header(classHash: 0x53E24BDB95306A09), "test"));
    }

    [Fact]
    public void RejectsUnmeasuredVersions()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(SkeletonTestData.Header(version: 2), "test"));
        Assert.Contains("version 2", error.Message);
    }

    [Fact]
    public void RejectsUnmeasuredStorage()
    {
        byte[] header = SkeletonTestData.Header();
        header[0x32] = 0;
        Assert.Throws<InvalidDataException>(() => SkeletonHead.Parse(header, "test"));
    }

    [Fact]
    public void RejectsZeroName()
    {
        Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(SkeletonTestData.Header(name: 0), "test"));
    }

    [Fact]
    public void RejectsOversizedLink()
    {
        Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(
                SkeletonTestData.Header(linkSize: SkeletonHead.MaxLinkSize + 1), "test"));
    }

    [Fact]
    public void RejectsBadSizes()
    {
        Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(SkeletonTestData.Header(bodySize: 0), "test"));
        Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(
                SkeletonTestData.Header(bodySize: 8, storedSize: 100), "test"));
        Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(SkeletonTestData.Header(storedSize: 8), "test"));
    }

    [Fact]
    public void RejectsShortHeader()
    {
        Assert.Throws<InvalidDataException>(
            () => SkeletonHead.Parse(new byte[0x20], "test"));
    }
}
