// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop;

internal static class PreviewBitmap
{
    public static Bitmap Create(DecodedTexture image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentException("Texture dimensions must be positive.", nameof(image));
        }

        if (image.Rgba.LongLength != (long)image.Width * image.Height * 4)
        {
            throw new ArgumentException("RGBA length does not match the dimensions.", nameof(image));
        }

        Bitmap bitmap = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, image.Width, image.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = image.Width * 4;
            byte[] row = new byte[rowBytes];
            Span<uint> target = MemoryMarshal.Cast<byte, uint>(row.AsSpan());
            ReadOnlySpan<uint> source = MemoryMarshal.Cast<byte, uint>(image.Rgba.AsSpan());
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<uint> line = source.Slice(y * image.Width, image.Width);
                for (int x = 0; x < line.Length; x++)
                {
                    uint value = line[x];
                    target[x] = (value & 0xFF00FF00u)
                        | ((value & 0x00FF0000u) >> 16)
                        | ((value & 0x000000FFu) << 16);
                }

                Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
