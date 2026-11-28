// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AptStudio.Desktop;

internal sealed record SkinPartRow(
    int Index,
    ulong MeshHash,
    int Vertices,
    int Stride,
    int Packets,
    bool PacketsParsed,
    ulong MaterialHash);
