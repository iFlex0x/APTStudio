// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using AptStudio.Core.Audio;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal enum BrowserView
{
    Textures,
    Material,
    Materials,
    LinkedTexture,
    Animations,
    AnimationLarge,
    Meshes,
    Skins,
    Skin,
    SkinPose,
    Skeletons,
    Skeleton,
    Audio,
}

internal enum AudioSort
{
    Pack,
    Id,
    Name,
    Length,
    Channels,
    Rate,
    Language,
    Bank,
}

internal enum TextureSort
{
    Archive,
    Hash,
    Size,
    Mips,
}

internal sealed record TextureFilter(
    TextureCodec? Codec = null,
    int? Width = null,
    int? Height = null,
    int? MipCount = null,
    bool PreviewableOnly = false)
{
    public static TextureFilter All { get; } = new();

    public bool IsAll =>
        Codec is null && Width is null && Height is null && MipCount is null && !PreviewableOnly;

    public bool Matches(BitmapHead head) =>
        (Codec is null || head.Format.Codec == Codec)
        && (Width is null || head.Width == Width)
        && (Height is null || head.Height == Height)
        && (MipCount is null || head.MipCount == MipCount)
        && (!PreviewableOnly || TextureDecoder.CanDecodeMip0(head));
}

internal sealed record TextureOccurrence(
    ulong Hash,
    string ArchivePath,
    string BodyArchivePath,
    long BodyOffset,
    BitmapHead Head)
{
    public string ArchiveName => Path.GetFileName(ArchivePath);

    public string BodyArchiveName => Path.GetFileName(BodyArchivePath);

    public bool IsExternal =>
        !ArchivePath.Equals(BodyArchivePath, StringComparison.OrdinalIgnoreCase);
}

internal sealed record MaterialOccurrence(
    ulong Hash,
    string ArchivePath,
    long BodyOffset,
    string Code)
{
    public string ArchiveName => Path.GetFileName(ArchivePath);
}

internal sealed record AnimationOccurrence(
    ulong Hash,
    string ArchivePath,
    string BodyArchivePath,
    long BodyOffset)
{
    public string ArchiveName => Path.GetFileName(ArchivePath);

    public (string ArchivePath, long Offset) BodyKey => (BodyArchivePath, BodyOffset);
}

internal sealed record MeshOccurrence(
    ulong Hash,
    string ArchivePath,
    string BodyArchivePath,
    long BodyOffset)
{
    public string ArchiveName => Path.GetFileName(ArchivePath);

    public string BodyArchiveName => Path.GetFileName(BodyArchivePath);

    public bool IsExternal =>
        !ArchivePath.Equals(BodyArchivePath, StringComparison.OrdinalIgnoreCase);
}

internal sealed record AnimationLargeState(
    ulong SkinHash,
    string SkinArchivePath,
    long SkinBodyOffset,
    ulong SkeletonRoot,
    int Frame,
    bool Applied,
    int Matched,
    int AnimationBones,
    int SkeletonBones,
    float Duration,
    int BoneCount);

internal sealed record SkinOccurrence(
    ulong Hash,
    string ArchivePath,
    string BodyArchivePath,
    long BodyOffset)
{
    public string ArchiveName => Path.GetFileName(ArchivePath);

    public string BodyArchiveName => Path.GetFileName(BodyArchivePath);

    public bool IsExternal =>
        !ArchivePath.Equals(BodyArchivePath, StringComparison.OrdinalIgnoreCase);
}

internal sealed record SkeletonOccurrence(
    ulong Hash,
    string ArchivePath,
    long Offset)
{
    public string ArchiveName => Path.GetFileName(ArchivePath);
}

internal sealed record BrowserState(
    BrowserView View,
    string ArchivePath,
    string Query,
    TextureFilter Filter,
    TextureOccurrence? Texture,
    int ListIndex,
    int Mip,
    ChannelView Channel,
    bool Fit,
    float Zoom,
    PointF Pan,
    MaterialOccurrence? Material,
    int MaterialSlot,
    TextureOccurrence? LinkedTexture,
    AnimationOccurrence? Animation,
    TextureSort Sort = TextureSort.Archive,
    bool SortDescending = false,
    MeshOccurrence? Mesh = null,
    MeshCamera? MeshCamera = null,
    bool MeshWireframe = false,
    SkinOccurrence? Skin = null,
    int SkinPart = 0,
    SkeletonOccurrence? Skeleton = null,
    AudioEntry? Audio = null,
    string? AudioPack = null,
    string? AudioLanguage = null,
    AudioSort AudioSort = AudioSort.Pack,
    bool AudioSortDescending = false,
    bool MaterialFromList = false,
    string? MaterialError = null,
    int ListScroll = 0,
    ulong PoseSkeletonRoot = 0,
    AnimationLargeState? AnimationLarge = null);
