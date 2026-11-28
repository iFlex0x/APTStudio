// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AptStudio.Desktop.Browser;

internal sealed class LoadGeneration
{
    private int _current;

    public int Begin() => Interlocked.Increment(ref _current);

    public bool IsCurrent(int token) => Volatile.Read(ref _current) == token;
}
