// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal sealed record ThumbnailReadyEventArgs(string ArchivePath, long Offset);

internal sealed class ThumbnailCache : IDisposable
{
    private const int ThumbnailSize = 32;
    private const int CacheLimit = 512;
    private readonly ConcurrentDictionary<(string ArchivePath, long Offset), Bitmap> _cache = new();
    private readonly ConcurrentDictionary<(string ArchivePath, long Offset), byte> _pending = new();
    private readonly ConcurrentStack<TextureEntry> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cancel = new();
    private readonly SynchronizationContext? _sync;
    private readonly Task _worker;
    private readonly Bitmap _placeholder;

    public ThumbnailCache()
    {
        _sync = SynchronizationContext.Current;
        _placeholder = CreatePlaceholder();
        _worker = Task.Run(WorkAsync);
    }

    public event EventHandler<ThumbnailReadyEventArgs>? ThumbnailReady;

    public Bitmap Placeholder => _placeholder;

    public static int ThumbnailMip(BitmapHead head)
    {
        int best = 0;
        for (int mip = 0; mip < head.MipCount && mip < 32; mip++)
        {
            int width = Math.Max(1, head.Width >> mip);
            if (width >= ThumbnailSize)
            {
                best = mip;
            }
            else
            {
                break;
            }
        }

        return Math.Min(best, Math.Max(0, head.MipCount - 1));
    }

    public Bitmap GetCached(TextureEntry texture)
    {
        (string, long) key = Key(texture);
        return _cache.TryGetValue(key, out Bitmap? image) ? image : _placeholder;
    }

    public void Request(TextureEntry texture)
    {
        (string, long) key = Key(texture);
        if (_cache.ContainsKey(key) || !_pending.TryAdd(key, 0))
        {
            return;
        }

        _queue.Push(texture);
        _signal.Release();
    }

    public void Dispose()
    {
        _cancel.Cancel();
        _signal.Release();
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cancel.Dispose();
        _signal.Dispose();
        foreach (Bitmap image in _cache.Values)
        {
            image.Dispose();
        }

        _cache.Clear();
        _placeholder.Dispose();
    }

    private async Task WorkAsync()
    {
        while (!_cancel.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_cancel.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_queue.TryPop(out TextureEntry? texture))
            {
                continue;
            }

            (string, long) key = Key(texture);
            try
            {
                Bitmap image = Decode(texture);
                if (_cancel.IsCancellationRequested)
                {
                    image.Dispose();
                    return;
                }

                _cache[key] = image;
                Trim();
                _pending.TryRemove(key, out _);
                PostReady(key);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
                or IOException or UnauthorizedAccessException)
            {
                _pending.TryRemove(key, out _);
            }
        }
    }

    private static Bitmap Decode(TextureEntry texture)
    {
        using FileStream stream = new(
            texture.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        int mip = ThumbnailMip(texture.Head);
        DecodedTexture decoded = TextureDecoder.DecodeMip(stream, texture, mip);
        using Bitmap full = PreviewBitmap.Create(decoded);
        Bitmap thumbnail = new(ThumbnailSize, ThumbnailSize);
        using Graphics graphics = Graphics.FromImage(thumbnail);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(full, 0, 0, ThumbnailSize, ThumbnailSize);
        return thumbnail;
    }

    private void Trim()
    {
        if (_cache.Count <= CacheLimit)
        {
            return;
        }

        foreach ((string, long) key in _cache.Keys.Take(_cache.Count - CacheLimit))
        {
            if (_cache.TryRemove(key, out Bitmap? image))
            {
                image.Dispose();
            }
        }
    }

    private void PostReady((string ArchivePath, long Offset) key)
    {
        EventHandler<ThumbnailReadyEventArgs>? handler = ThumbnailReady;
        if (handler is null)
        {
            return;
        }

        void Raise() => handler(this, new ThumbnailReadyEventArgs(key.ArchivePath, key.Offset));
        if (_sync is null)
        {
            Raise();
        }
        else
        {
            _sync.Post(_ => Raise(), null);
        }
    }

    private static (string ArchivePath, long Offset) Key(TextureEntry texture) =>
        (texture.ArchivePath, texture.Row.BodyOffset);

    private static Bitmap CreatePlaceholder()
    {
        Bitmap bitmap = new(ThumbnailSize, ThumbnailSize);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Theme.Surface);
        using Pen border = new(Theme.Border);
        graphics.DrawRectangle(border, 0, 0, ThumbnailSize - 1, ThumbnailSize - 1);
        using Font font = new("Segoe UI", 10f);
        TextRenderer.DrawText(
            graphics,
            "?",
            font,
            new Rectangle(0, 0, ThumbnailSize, ThumbnailSize),
            Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return bitmap;
    }
}
