// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

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
