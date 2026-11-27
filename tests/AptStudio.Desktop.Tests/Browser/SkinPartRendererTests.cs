using System.Drawing;
using System.Numerics;
using AptStudio.Core.Meshes;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class SkinPartRendererTests
{
    private static readonly int Background = Theme.Background.ToArgb();

    [Fact]
    public void TintsEveryDisconnectedPartComponentAndNotTheGap()
    {
        MeshGeometry part = Join(
            Box(new Vector3(-1.4f, 0f, 0f), 0.5f),
            Box(new Vector3(1.4f, 0f, 0f), 0.5f));

        using Bitmap image = SkinPartRenderer.RenderContext([part], 0, 200, 160);

        Assert.True(CountTinted(image, 0, 0, 70, 160) > 20);
        Assert.True(CountTinted(image, 130, 0, 70, 160) > 20);
        Assert.Equal(0, CountTinted(image, 90, 0, 20, 160));
    }

    [Fact]
    public void ContextKeepsTheSameBodyBoundsWhenTheSelectionChanges()
    {
        MeshGeometry left = Box(new Vector3(-1.2f, 0f, 0f), 0.8f);
        MeshGeometry right = Box(new Vector3(1.2f, 0.2f, 0f), 0.6f);

        using Bitmap first = SkinPartRenderer.RenderContext([left, right], 0, 200, 160);
        using Bitmap second = SkinPartRenderer.RenderContext([left, right], 1, 200, 160);

        Assert.Equal(ForegroundBounds(first), ForegroundBounds(second));
    }

    [Fact]
    public void ContextWithoutGeometryIsJustBackground()
    {
        using Bitmap image = SkinPartRenderer.RenderContext([null, null], 0, 120, 90);

        Assert.Equal(Background, image.GetPixel(60, 45).ToArgb());
    }

    private static int CountTinted(Bitmap image, int x0, int y0, int width, int height)
    {
        int count = 0;
        for (int y = y0; y < y0 + height; y++)
        {
            for (int x = x0; x < x0 + width; x++)
            {
                int color = image.GetPixel(x, y).ToArgb();
                int red = (color >> 16) & 0xFF;
                int blue = color & 0xFF;
                if (blue - red > 55 && blue > 120)
                {
                    count++;
                }
            }
        }

        return count;
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

    private static MeshGeometry Join(params MeshGeometry[] parts)
    {
        List<Vector3> positions = new();
        List<int> indices = new();
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        foreach (MeshGeometry part in parts)
        {
            int offset = positions.Count;
            positions.AddRange(part.Positions);
            foreach (int index in part.Indices)
            {
                indices.Add(index + offset);
            }

            min = Vector3.Min(min, part.Min);
            max = Vector3.Max(max, part.Max);
        }

        return new MeshGeometry(positions.ToArray(), indices.ToArray(), min, max);
    }

    private static MeshGeometry Box(Vector3 center, float size)
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
