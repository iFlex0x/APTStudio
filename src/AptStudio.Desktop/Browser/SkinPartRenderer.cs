using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using AptStudio.Core.Meshes;

namespace AptStudio.Desktop.Browser;

internal static class SkinPartRenderer
{
    private const float Yaw = 0.38f;
    private const float Pitch = 0.18f;
    private const float FieldOfView = 0.7f;
    private static readonly Vector3 LightDirection =
        Vector3.Normalize(new Vector3(0.35f, 0.8f, 0.5f));

    public static Bitmap RenderContext(
        IReadOnlyList<MeshGeometry?> parts,
        int selected,
        int width,
        int height)
    {
        int w = Math.Max(1, width);
        int h = Math.Max(1, height);
        Bitmap bitmap = new(w, h, PixelFormat.Format32bppArgb);
        int[] pixels = new int[w * h];
        float[] depth = new float[w * h];
        bool[] selectedMask = new bool[w * h];
        Array.Fill(pixels, Theme.Background.ToArgb());
        Array.Fill(depth, float.PositiveInfinity);

        if (TryBounds(parts, out Vector3 center, out float radius))
        {
            Matrix4x4 rotation =
                Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateRotationX(Pitch);
            Matrix4x4 matrix = Matrix4x4.CreateTranslation(-center)
                * Matrix4x4.CreateScale(1f / radius)
                * rotation
                * Matrix4x4.CreateTranslation(0f, 0f, -3f)
                * Matrix4x4.CreatePerspectiveFieldOfView(
                    FieldOfView, w / (float)h, 0.05f, 20f);

            for (int part = 0; part < parts.Count; part++)
            {
                MeshGeometry? geometry = parts[part];
                if (geometry is null
                    || geometry.Positions.Length == 0
                    || geometry.Indices.Length < 3)
                {
                    continue;
                }

                int count = geometry.Positions.Length;
                float[] sx = new float[count];
                float[] sy = new float[count];
                float[] sz = new float[count];
                bool[] valid = new bool[count];
                for (int index = 0; index < count; index++)
                {
                    Vector4 clip = Vector4.Transform(
                        new Vector4(geometry.Positions[index], 1f), matrix);
                    if (clip.W <= 0.0001f)
                    {
                        continue;
                    }

                    float inverse = 1f / clip.W;
                    sx[index] = (clip.X * inverse * 0.5f + 0.5f) * w;
                    sy[index] = (0.5f - clip.Y * inverse * 0.5f) * h;
                    sz[index] = clip.Z * inverse;
                    valid[index] = sz[index] is >= 0f and <= 1f;
                }

                bool isSelected = part == selected;
                for (int triangle = 0; triangle + 2 < geometry.Indices.Length; triangle += 3)
                {
                    int i0 = geometry.Indices[triangle];
                    int i1 = geometry.Indices[triangle + 1];
                    int i2 = geometry.Indices[triangle + 2];
                    if (!valid[i0] || !valid[i1] || !valid[i2])
                    {
                        continue;
                    }

                    Vector3 p0 = geometry.Positions[i0];
                    Vector3 p1 = geometry.Positions[i1];
                    Vector3 p2 = geometry.Positions[i2];
                    Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                    if (normal.LengthSquared() < 1e-12f)
                    {
                        continue;
                    }

                    normal = Vector3.Normalize(Vector3.TransformNormal(normal, rotation));
                    float light = 0.5f + 0.5f * MathF.Abs(Vector3.Dot(normal, LightDirection));
                    int color = Shade(light);
                    if (isSelected)
                    {
                        color = Tint(color);
                    }

                    Rasterize(
                        (sx[i0], sy[i0], sz[i0]),
                        (sx[i1], sy[i1], sz[i1]),
                        (sx[i2], sy[i2], sz[i2]),
                        color,
                        isSelected,
                        pixels,
                        depth,
                        selectedMask,
                        w,
                        h);
                }
            }
        }

        Outline(pixels, selectedMask, w, h);

        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, w, h),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bitmap.UnlockBits(data);
        return bitmap;
    }

    private static bool TryBounds(
        IReadOnlyList<MeshGeometry?> parts,
        out Vector3 center,
        out float radius)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool any = false;
        foreach (MeshGeometry? geometry in parts)
        {
            if (geometry is null || geometry.Positions.Length == 0)
            {
                continue;
            }

            min = Vector3.Min(min, geometry.Min);
            max = Vector3.Max(max, geometry.Max);
            any = true;
        }

        if (!any)
        {
            center = Vector3.Zero;
            radius = 1f;
            return false;
        }

        center = (min + max) * 0.5f;
        radius = MathF.Max((max - min).Length() * 0.5f, 1e-6f);
        return true;
    }

    private static void Rasterize(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        int color,
        bool selected,
        int[] pixels,
        float[] depth,
        bool[] selectedMask,
        int width,
        int height)
    {
        float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (MathF.Abs(area) < 0.001f)
        {
            return;
        }

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        float inverseArea = 1f / area;
        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float w0 = (c.X - b.X) * (py - b.Y) - (c.Y - b.Y) * (px - b.X);
                float w1 = (a.X - c.X) * (py - c.Y) - (a.Y - c.Y) * (px - c.X);
                float w2 = (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);
                float l0 = w0 * inverseArea;
                float l1 = w1 * inverseArea;
                float l2 = w2 * inverseArea;
                if (l0 < 0f || l1 < 0f || l2 < 0f)
                {
                    continue;
                }

                float z = l0 * a.Z + l1 * b.Z + l2 * c.Z;
                int index = row + x;
                if (z >= depth[index])
                {
                    continue;
                }

                depth[index] = z;
                pixels[index] = color;
                selectedMask[index] = selected;
            }
        }
    }

    private static void Outline(int[] pixels, bool[] mask, int width, int height)
    {
        int accent = Theme.Accent.ToArgb();
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = row + x;
                if (!mask[index])
                {
                    continue;
                }

                bool edge = x == 0
                    || x == width - 1
                    || y == 0
                    || y == height - 1
                    || !mask[index - 1]
                    || !mask[index + 1]
                    || !mask[index - width]
                    || !mask[index + width];
                if (edge)
                {
                    pixels[index] = accent;
                }
            }
        }
    }

    private static int Tint(int color)
    {
        int red = (color >> 16) & 0xFF;
        int green = (color >> 8) & 0xFF;
        int blue = color & 0xFF;
        red = (red * 8 + 79 * 2) / 10;
        green = (green * 8 + 195 * 2) / 10;
        blue = (blue * 8 + 247 * 2) / 10;
        return unchecked((int)0xFF000000) | (red << 16) | (green << 8) | blue;
    }

    private static int Shade(float light)
    {
        byte value = (byte)Math.Clamp(light * 220f, 0f, 255f);
        int red = (int)(value * 0.75f);
        int green = (int)(value * 0.85f);
        return unchecked((int)0xFF000000) | (red << 16) | (green << 8) | value;
    }
}
