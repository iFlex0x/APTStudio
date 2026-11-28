// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace AptStudio.Desktop.Tests;

internal static class PostedInput
{
    private const uint KeyDownMessage = 0x0100;
    private const uint KeyUpMessage = 0x0101;
    private const uint MouseMoveMessage = 0x0200;
    private const uint LeftDownMessage = 0x0201;
    private const uint LeftUpMessage = 0x0202;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    public static void Key(Control control, Keys key)
    {
        PostMessage(control.Handle, KeyDownMessage, (nint)key, 0);
        PostMessage(control.Handle, KeyUpMessage, (nint)key, 0);
    }

    public static void MouseDown(Control control, int x, int y) =>
        PostMessage(control.Handle, LeftDownMessage, 1, Pack(x, y));

    public static void MouseMove(Control control, int x, int y) =>
        PostMessage(control.Handle, MouseMoveMessage, 1, Pack(x, y));

    public static void MouseUp(Control control, int x, int y) =>
        PostMessage(control.Handle, LeftUpMessage, 0, Pack(x, y));

    private static nint Pack(int x, int y) => (y << 16) | (x & 0xFFFF);
}
