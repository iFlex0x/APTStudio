// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Containers;

namespace AptStudio.Core.Textures;

public sealed record TextureEntry(string ArchivePath, DpcEntry Row, BitmapHead Head)
{
    public long PayloadOffset => Row.BodyOffset + Head.PayloadOffset;
}
