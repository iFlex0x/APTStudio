// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Animations;
using AptStudio.Core.Assets;
using AptStudio.Core.Containers;

namespace AptStudio.Core.Tests.Animations;

public sealed class AnimationSkinCandidatesTests
{
    [Fact]
    public void SortRanksMatchedThenSkeletonBonesThenSkinHash()
    {
        AnimationSkinCandidate first = Candidate(0x3000, 258, 294);
        AnimationSkinCandidate second = Candidate(0x2000, 258, 298);
        AnimationSkinCandidate third = Candidate(0x1000, 254, 283);

        IReadOnlyList<AnimationSkinCandidate> sorted =
            AnimationSkinCandidates.Sort([second, third, first]);

        Assert.Equal([first, second, third], sorted);
    }

    [Fact]
    public void SortKeepsArchiveAsFinalTieBreak()
    {
        AnimationSkinCandidate second = Candidate(0x1000, 258, 294, "B.DPC");
        AnimationSkinCandidate first = Candidate(0x1000, 258, 294, "A.DPC");

        IReadOnlyList<AnimationSkinCandidate> sorted =
            AnimationSkinCandidates.Sort([second, first]);

        Assert.Equal(["A.DPC", "B.DPC"], sorted.Select(row => row.SkinArchive));
    }

    private static AnimationSkinCandidate Candidate(
        ulong skin,
        int matched,
        int bones,
        string archive = "COMMON.DPC")
    {
        AssetEntry entry = new(
            archive,
            archive,
            false,
            new DpcEntry(skin, 0, 0, 0, 0, 0, 0, 0, 0),
            null,
            null);
        return new AnimationSkinCandidate(
            skin,
            archive,
            entry,
            0x9D330C091490A041,
            matched,
            275,
            bones);
    }
}
