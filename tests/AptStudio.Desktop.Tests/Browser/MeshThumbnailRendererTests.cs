// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using System.Numerics;
using AptStudio.Core.Meshes;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class MeshThumbnailRendererTests
{
    private static readonly int Background = Theme.Surface.ToArgb();

    [Fact]
    public void RendersShadedCubeAtThumbnailSize()
    {
        using Bitmap image = MeshThumbnailRenderer.Render(Cube(1f, Vector3.Zero))!;

        Assert.Equal(34, image.Width);
        Assert.Equal(34, image.Height);
        Assert.True(CountForeground(image) > 100);
        Assert.True(DistinctForegroundColors(image) >= 3);
    }

    [Fact]
    public void FramesScaledAndTranslatedMeshesTheSame()
    {
        using Bitmap first = MeshThumbnailRenderer.Render(Cube(1f, Vector3.Zero))!;
        using Bitmap second = MeshThumbnailRenderer.Render(
            Cube(100f, new Vector3(1200f, -640f, 310f)))!;

        Rectangle a = ForegroundBounds(first);
        Rectangle b = ForegroundBounds(second);

        Assert.InRange(Math.Abs(a.Left - b.Left), 0, 1);
        Assert.InRange(Math.Abs(a.Top - b.Top), 0, 1);
        Assert.InRange(Math.Abs(a.Right - b.Right), 0, 1);
        Assert.InRange(Math.Abs(a.Bottom - b.Bottom), 0, 1);
    }

    [Fact]
    public void EmptyMeshHasNoThumbnail()
    {
        Assert.Null(MeshThumbnailRenderer.Render(
            new MeshGeometry([], [], Vector3.Zero, Vector3.Zero)));
    }

    private static int CountForeground(Bitmap image)
    {
        int count = 0;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).ToArgb() != Background)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int DistinctForegroundColors(Bitmap image)
    {
        HashSet<int> colors = new();
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int color = image.GetPixel(x, y).ToArgb();
                if (color != Background)
                {
                    colors.Add(color);
                }
            }
        }

        return colors.Count;
    }

    private static Rectangle ForegroundBounds(Bitmap image)
    {
        int left = image.Width;
        int top = image.Height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).ToArgb() == Background)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        Assert.True(right >= left && bottom >= top);
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static MeshGeometry Cube(float size, Vector3 center)
    {
        float h = size * 0.5f;
        Vector3[] positions =
        [
            center + new Vector3(-h, -h, -h),
            center + new Vector3(h, -h, -h),
            center + new Vector3(h, h, -h),
            center + new Vector3(-h, h, -h),
            center + new Vector3(-h, -h, h),
            center + new Vector3(h, -h, h),
            center + new Vector3(h, h, h),
            center + new Vector3(-h, h, h),
        ];
        int[] indices =
        [
            4, 5, 6, 4, 6, 7,
            1, 0, 3, 1, 3, 2,
            0, 4, 7, 0, 7, 3,
            5, 1, 2, 5, 2, 6,
            3, 7, 6, 3, 6, 2,
            0, 1, 5, 0, 5, 4,
        ];
        return new MeshGeometry(
            positions,
            indices,
            center - new Vector3(h),
            center + new Vector3(h));
    }
}
