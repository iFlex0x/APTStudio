// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using AptStudio.Core.Meshes;

namespace AptStudio.Desktop.Browser;

internal static class MeshThumbnailRenderer
{
    private const int Supersample = 2;
    private const float Yaw = 0.65f;
    private const float Pitch = 0.35f;
    private const float FieldOfView = 0.7f;
    private static readonly Vector3 LightDirection =
        Vector3.Normalize(new Vector3(0.35f, 0.8f, 0.5f));

    public static Bitmap? Render(MeshGeometry mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Positions.Length == 0 || mesh.Indices.Length < 3)
        {
            return null;
        }

        int size = BrowserVisual.ThumbnailSize;
        int scale = size * Supersample;
        int[] pixels = new int[scale * scale];
        float[] depth = new float[scale * scale];
        Array.Fill(pixels, Theme.Surface.ToArgb());
        Array.Fill(depth, float.PositiveInfinity);

        Vector3 center = (mesh.Min + mesh.Max) * 0.5f;
        float radius = MathF.Max((mesh.Max - mesh.Min).Length() * 0.5f, 1e-6f);
        Matrix4x4 rotation =
            Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateRotationX(Pitch);
        Matrix4x4 matrix = Matrix4x4.CreateTranslation(-center)
            * Matrix4x4.CreateScale(1f / radius)
            * rotation
            * Matrix4x4.CreateTranslation(0f, 0f, -3f)
            * Matrix4x4.CreatePerspectiveFieldOfView(
                FieldOfView, 1f, 0.05f, 20f);

        int count = mesh.Positions.Length;
        float[] sx = new float[count];
        float[] sy = new float[count];
        float[] sz = new float[count];
        bool[] valid = new bool[count];
        for (int index = 0; index < count; index++)
        {
            Vector4 clip = Vector4.Transform(
                new Vector4(mesh.Positions[index], 1f), matrix);
            if (clip.W <= 0.0001f)
            {
                continue;
            }

            float inverse = 1f / clip.W;
            sx[index] = (clip.X * inverse * 0.5f + 0.5f) * scale;
            sy[index] = (0.5f - clip.Y * inverse * 0.5f) * scale;
            sz[index] = clip.Z * inverse;
            valid[index] = sz[index] is >= 0f and <= 1f;
        }

        for (int triangle = 0; triangle + 2 < mesh.Indices.Length; triangle += 3)
        {
            int i0 = mesh.Indices[triangle];
            int i1 = mesh.Indices[triangle + 1];
            int i2 = mesh.Indices[triangle + 2];
            if (!valid[i0] || !valid[i1] || !valid[i2])
            {
                continue;
            }

            Vector3 p0 = mesh.Positions[i0];
            Vector3 p1 = mesh.Positions[i1];
            Vector3 p2 = mesh.Positions[i2];
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
            if (normal.LengthSquared() < 1e-12f)
            {
                continue;
            }

            normal = Vector3.Normalize(Vector3.TransformNormal(normal, rotation));
            float light = 0.5f + 0.5f * MathF.Abs(Vector3.Dot(normal, LightDirection));
            Rasterize(
                (sx[i0], sy[i0], sz[i0]),
                (sx[i1], sy[i1], sz[i1]),
                (sx[i2], sy[i2], sz[i2]),
                Shade(light),
                pixels,
                depth,
                scale);
        }

        using Bitmap large = new(scale, scale, PixelFormat.Format32bppArgb);
        BitmapData data = large.LockBits(
            new Rectangle(0, 0, scale, scale),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        large.UnlockBits(data);

        Bitmap thumbnail = new(size, size, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(thumbnail);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(large, 0, 0, size, size);
        return thumbnail;
    }

    private static void Rasterize(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        int color,
        int[] pixels,
        float[] depth,
        int size)
    {
        float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (MathF.Abs(area) < 0.001f)
        {
            return;
        }

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int maxX = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int maxY = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        float inverseArea = 1f / area;
        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            int row = y * size;
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
            }
        }
    }

    private static int Shade(float light)
    {
        byte value = (byte)Math.Clamp(light * 220f, 0f, 255f);
        int red = (int)(value * 0.75f);
        int green = (int)(value * 0.85f);
        return unchecked((int)0xFF000000) | (red << 16) | (green << 8) | value;
    }
}
