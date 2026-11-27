using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Links;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Skins;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Animations;

public sealed record AnimationSkinCandidate(
    ulong SkinHash,
    string SkinArchive,
    AssetEntry Skin,
    ulong SkeletonRoot,
    int Matched,
    int AnimationBones,
    int SkeletonBones);

public static class AnimationSkinCandidates
{
    public static IReadOnlyList<AnimationSkinCandidate> Load(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry animation,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(animation);

        AnimationBody body;
        using (FileStream stream = new(
            animation.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            body = AnimationDecoder.ReadBody(stream, animation.BodyArchivePath, animation.Row);
        }

        AnimationData data = body.Data;
        if (data.Encoding != AnimationEncoding.Packed || data.Bones.Count == 0)
        {
            return Array.Empty<AnimationSkinCandidate>();
        }

        ulong[] animationBones = data.Bones.Select(bone => bone.NameHash).ToArray();
        SkinSkeletonIndex skins = SkinSkeletonIndex.Load(assets, progress, cancellationToken);
        SkeletonCatalog catalog = SkeletonCatalog.Load(
            profile, false, progress, cancellationToken);
        Dictionary<ulong, SkeletonEntry> entries = new();
        foreach (SkeletonEntry entry in catalog.Entries)
        {
            entries.TryAdd(entry.NameHash, entry);
        }

        List<AnimationSkinCandidate> rows = new();
        foreach ((ulong skeletonRoot, IReadOnlyList<SkinSkeletonUse> uses) in skins.BySkeleton)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entries.TryGetValue(skeletonRoot, out SkeletonEntry? entry))
            {
                continue;
            }

            SkeletonData skeleton;
            using (FileStream stream = new(
                entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                skeleton = SkeletonDecoder.Read(stream, entry.ArchivePath, entry.Hit);
            }

            HashSet<ulong> skeletonBones =
                skeleton.Bones.Select(bone => bone.NameHash).ToHashSet();
            int matched = AnimationSkeletonMatch.Count(animationBones, skeletonBones);
            if (matched == 0)
            {
                continue;
            }

            foreach (SkinSkeletonUse use in uses)
            {
                rows.Add(new AnimationSkinCandidate(
                    use.NameHash,
                    use.ArchiveName,
                    use.Row,
                    skeletonRoot,
                    matched,
                    animationBones.Length,
                    skeleton.Bones.Count));
            }
        }

        return Sort(rows);
    }

    public static IReadOnlyList<AnimationSkinCandidate> Sort(
        IEnumerable<AnimationSkinCandidate> rows)
    {
        List<AnimationSkinCandidate> sorted = rows.ToList();
        sorted.Sort((left, right) =>
        {
            int order = right.Matched.CompareTo(left.Matched);
            if (order != 0)
            {
                return order;
            }

            order = left.SkeletonBones.CompareTo(right.SkeletonBones);
            if (order != 0)
            {
                return order;
            }

            order = left.SkinHash.CompareTo(right.SkinHash);
            return order != 0
                ? order
                : string.Compare(
                    left.SkinArchive,
                    right.SkinArchive,
                    StringComparison.OrdinalIgnoreCase);
        });
        return sorted;
    }
}
