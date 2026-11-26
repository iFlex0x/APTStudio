namespace AptStudio.Core.Animations;

public static class AnimationSkeletonMatch
{
    public static int Count(IReadOnlyList<ulong> animationBones, IReadOnlySet<ulong> skeletonBones)
    {
        ArgumentNullException.ThrowIfNull(animationBones);
        ArgumentNullException.ThrowIfNull(skeletonBones);
        int matched = 0;
        foreach (ulong hash in animationBones)
        {
            if (skeletonBones.Contains(hash))
            {
                matched++;
            }
        }

        return matched;
    }
}
