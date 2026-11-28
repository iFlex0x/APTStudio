// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class MaterialNoticeTests
{
    [Fact]
    public void ZeroSlotNoticeKeepsOpenTextureDisabled()
    {
        using MaterialSlotsView slots = new();
        using MaterialSlotInspector inspector = new();

        slots.ShowNotice("No filled texture slots", false);
        inspector.ShowNotice(
            "No filled texture slots", "This material has no texture slots.", false);

        Assert.True(slots.NoticeActive);
        Assert.Equal("No filled texture slots", slots.NoticeText);
        Assert.True(inspector.NoticeActive);
        Assert.Equal("No filled texture slots", inspector.NoticeTitle);
        Assert.False(inspector.OpenTextureEnabled);
    }

    [Fact]
    public void ReadErrorNoticeIsDistinctAndDisablesOpenTexture()
    {
        using MaterialSlotsView slots = new();
        using MaterialSlotInspector inspector = new();

        slots.ShowNotice("Could not read this material", true);
        inspector.ShowNotice("Could not read this material", "short read at 42", true);

        Assert.True(slots.NoticeActive);
        Assert.Equal("Could not read this material", slots.NoticeText);
        Assert.True(inspector.NoticeActive);
        Assert.Equal("Could not read this material", inspector.NoticeTitle);
        Assert.False(inspector.OpenTextureEnabled);
    }
}
