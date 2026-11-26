using AptStudio.Core.Links;
using AptStudio.Core.Materials;
using AptStudio.Core.Tests.Materials;

namespace AptStudio.Core.Tests.Links;

public sealed class MaterialTextureIndexTests
{
    [Fact]
    public void LinksSlotsBackToMaterials()
    {
        byte[] first = MaterialTestData.Object(
            MaterialTestData.Body((2, 0x1111), (5, 0x2222)), name: 0xAAAA);
        byte[] second = MaterialTestData.Object(
            MaterialTestData.Body((0, 0x1111)), name: 0xBBBB);
        string path = MaterialTestData.TempFile([.. first, .. second]);
        try
        {
            MaterialEntry one = Entry(path, 0xAAAA, 0, first.Length);
            MaterialEntry two = Entry(path, 0xBBBB, first.Length, second.Length);
            MaterialTextureIndex index = MaterialTextureIndex.Build([one, two]);

            Assert.Empty(index.Failures);
            Assert.Equal(2, index.Materials);
            Assert.Equal(3, index.Slots);
            Assert.Equal(2, index.ByTexture.Count);

            IReadOnlyList<MaterialTextureUse> shared = index.ByTexture[0x1111];
            Assert.Equal(2, shared.Count);
            Assert.Equal([2], shared.Single(use => use.NameHash == 0xAAAA).Slots);
            Assert.Equal([0], shared.Single(use => use.NameHash == 0xBBBB).Slots);
            Assert.Single(index.ByTexture[0x2222]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BadMaterialIsANamedFailure()
    {
        string path = MaterialTestData.TempFile(new byte[64]);
        try
        {
            MaterialEntry entry = Entry(path, 0xAAAA, 0, 64);
            MaterialTextureIndex index = MaterialTextureIndex.Build([entry]);

            Assert.Empty(index.ByTexture);
            Assert.Single(index.Failures);
            Assert.Equal(0, index.Materials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MaterialEntry Entry(string path, ulong name, long offset, int size) =>
        new(path, new MaterialHit(
            offset, name, name, MaterialHead.MaterialClass, (uint)size));
}
