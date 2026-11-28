// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class SkinPartViewTests
{
    [Fact]
    public void BindingStripFitsTheActionButtonsForBoundAndRootZeroSkins()
    {
        using SkinPartView view = new() { Size = new Size(960, 600) };
        view.CreateControl();

        SetSkin(
            view,
            "0x84316AA817823056",
            "HUG · 14 parts",
            "skeleton 0xC81A1F5B0C7D7DBB",
            0xC81A1F5B0C7D7DBB);

        Button open = FindButton(view, "Open Skeleton");
        Button pose = FindButton(view, "Pose skin");
        FlowLayoutPanel strip = (FlowLayoutPanel)open.Parent!;

        Assert.True(open.Enabled);
        Assert.True(pose.Enabled);
        Assert.Equal(26, open.Height);
        Assert.Equal(26, pose.Height);
        Assert.True(open.Bottom <= strip.ClientSize.Height, HeightSummary(strip, open, pose));
        Assert.True(pose.Bottom <= strip.ClientSize.Height, HeightSummary(strip, open, pose));

        SetSkin(
            view,
            "0x273234AA43A754AA1",
            "HUG · 14 parts",
            "No explicit skeleton binding",
            0);

        Assert.False(open.Enabled);
        Assert.False(pose.Enabled);
        Assert.True(open.Bottom <= strip.ClientSize.Height, HeightSummary(strip, open, pose));
    }

    private static void SetSkin(
        SkinPartView view,
        string title,
        string subtitle,
        string binding,
        ulong root)
    {
        view.SetSkin(
            title,
            subtitle,
            binding,
            root,
            Array.Empty<SkinPartDisplay>(),
            0,
            "");
        for (int pass = 0; pass < 3; pass++)
        {
            view.PerformLayout();
            foreach (Control child in view.Controls)
            {
                child.PerformLayout();
                foreach (Control nested in child.Controls)
                {
                    nested.PerformLayout();
                }
            }
        }
    }

    private static Button FindButton(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            Button? nested = FindButtonOrNull(child, text);
            if (nested is not null)
            {
                return nested;
            }
        }

        throw new InvalidOperationException($"{text} not found.");
    }

    private static Button? FindButtonOrNull(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            Button? nested = FindButtonOrNull(child, text);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string HeightSummary(Control strip, params Control[] buttons) =>
        $"strip {strip.ClientSize.Height}px; "
        + string.Join(", ", buttons.Select(button => $"{button.Text} bottom {button.Bottom}px"));
}
