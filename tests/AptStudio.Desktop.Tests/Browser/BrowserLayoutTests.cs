// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserLayoutTests
{
    [Theory]
    [InlineData(1099, true)]
    [InlineData(1100, false)]
    [InlineData(1440, false)]
    public void CollapsesOnlyBelowThreshold(int width, bool expected)
    {
        Assert.Equal(expected, BrowserLayout.ShouldCollapseSidebar(width));
    }

    [Theory]
    [InlineData(FormWindowState.Minimized, false, (int)BrowserResizeAction.Ignore)]
    [InlineData(FormWindowState.Minimized, true, (int)BrowserResizeAction.Ignore)]
    [InlineData(FormWindowState.Normal, false, (int)BrowserResizeAction.Apply)]
    [InlineData(FormWindowState.Maximized, false, (int)BrowserResizeAction.Apply)]
    [InlineData(FormWindowState.Normal, true, (int)BrowserResizeAction.Reapply)]
    [InlineData(FormWindowState.Maximized, true, (int)BrowserResizeAction.Reapply)]
    public void PlansResizeAroundMinimize(
        FormWindowState state,
        bool wasMinimized,
        int expected)
    {
        Assert.Equal(
            (BrowserResizeAction)expected,
            BrowserLayout.PlanResize(state, wasMinimized));
    }

    [Fact]
    public void ClampsSidebarAndInspectorToMinimums()
    {
        Assert.Equal(180, BrowserLayout.ClampSidebar(100, 1440));
        Assert.Equal(320, BrowserLayout.ClampInspector(100, 1440));
        Assert.Equal(680, BrowserLayout.ClampSidebar(900, 1440));
        Assert.Equal(760, BrowserLayout.ClampInspector(900, 1440));
    }
}
