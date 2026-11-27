using System.Numerics;
using AptStudio.Core.Animations;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Meshes;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Skins;

namespace AptStudio.Desktop.Browser;

internal sealed class PoseScene
{
    private PoseScene(
        SkinDeformer deformer,
        SkeletonData skeleton,
        MeshGeometry bindPose)
    {
        Deformer = deformer;
        Skeleton = skeleton;
        BindPose = bindPose;
    }

    public SkinDeformer Deformer { get; }

    public SkeletonData Skeleton { get; }

    public MeshGeometry BindPose { get; }

    public AnimationClipBinding? Binding { get; private set; }

    public static PoseScene Load(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skinRow,
        ulong skeletonRoot,
        CancellationToken cancellationToken)
    {
        SkeletonCatalog catalog = SkeletonCatalog.Load(
            profile, false, null, cancellationToken);
        SkeletonEntry skeletonEntry = catalog.Entries.FirstOrDefault(
            entry => entry.NameHash == skeletonRoot)
            ?? throw new InvalidDataException(
                $"skeleton 0x{skeletonRoot:X16} was not found.");
        SkeletonData skeleton;
        using (FileStream stream = new(
            skeletonEntry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            skeleton = SkeletonDecoder.Read(
                stream, skeletonEntry.ArchivePath, skeletonEntry.Hit);
        }

        Dictionary<ulong, AssetEntry> meshRows = new();
        foreach (AssetEntry entry in assets)
        {
            if (entry.ClassHash == AssetClass.Mesh)
            {
                meshRows.TryAdd(entry.NameHash, entry);
            }
        }

        Dictionary<ulong, MeshGeometry?> meshes = new();
        MeshGeometry? Geometry(ulong hash)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (meshes.TryGetValue(hash, out MeshGeometry? cached))
            {
                return cached;
            }

            if (!meshRows.TryGetValue(hash, out AssetEntry? row))
            {
                return null;
            }

            using FileStream stream = new(
                row.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            MeshData data = MeshDecoder.Read(stream, row.BodyArchivePath, row.Row);
            MeshGeometry mesh = data.Streams.Count == 0
                ? new MeshGeometry([], [], Vector3.Zero, Vector3.Zero)
                : data.ToGeometry();
            meshes.Add(hash, mesh);
            return mesh;
        }

        int? Verts(ulong hash) => Geometry(hash)?.Positions.Length;

        SkinData skin;
        using (FileStream stream = new(
            skinRow.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            skin = SkinDecoder.Read(stream, skinRow.BodyArchivePath, skinRow.Row, Verts);
        }

        SkinDeformer deformer = SkinDeformer.Create(skin, skeleton, Geometry);
        return new PoseScene(deformer, skeleton, deformer.Deform(null));
    }

    public AnimationClipBinding BindAnimation(AnimationClip clip)
    {
        Binding = AnimationClipBinding.Create(clip, Skeleton);
        return Binding;
    }

    public MeshGeometry Deform(int frame)
    {
        if (Binding is null)
        {
            return BindPose;
        }

        int clamped = Math.Clamp(frame, 0, Binding.FrameCount - 1);
        return Deformer.DeformAbsolute(Binding.Sample(clamped));
    }
}
