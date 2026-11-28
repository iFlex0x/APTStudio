// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Animations;

namespace AptStudio.Core.Tests.Animations;

public sealed class AnimationSkeletonMatchTests
{
    [Fact]
    public void CountsSharedBones()
    {
        ulong[] animation = [1, 2, 3, 4];
        HashSet<ulong> skeleton = [2, 4, 9];

        Assert.Equal(2, AnimationSkeletonMatch.Count(animation, skeleton));
    }

    [Fact]
    public void CountsEveryAnimationBone()
    {
        ulong[] animation = [1, 1, 2];
        HashSet<ulong> skeleton = [1, 2];

        Assert.Equal(3, AnimationSkeletonMatch.Count(animation, skeleton));
    }

    [Fact]
    public void EmptyAnimationCountsZero()
    {
        Assert.Equal(0, AnimationSkeletonMatch.Count([], new HashSet<ulong> { 1, 2 }));
    }

    [Fact]
    public void DisjointBonesCountZero()
    {
        Assert.Equal(
            0,
            AnimationSkeletonMatch.Count([1, 2], new HashSet<ulong> { 3, 4 }));
    }

    [Fact]
    public void NullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => AnimationSkeletonMatch.Count(null!, new HashSet<ulong>()));
        Assert.Throws<ArgumentNullException>(
            () => AnimationSkeletonMatch.Count([], null!));
    }
}
