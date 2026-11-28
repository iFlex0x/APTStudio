// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Drawing;
using AptStudio.Core.Assets;
using AptStudio.Core.Meshes;

namespace AptStudio.Desktop.Browser;

internal sealed class MeshThumbnailCache : IDisposable
{
    private const int CacheLimit = 256;
    private readonly object _gate = new();
    private readonly Dictionary<string, Bitmap> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, LinkedListNode<string>> _nodes =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private readonly List<AssetEntry> _visible = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cancel = new();
    private readonly SynchronizationContext? _sync;
    private readonly Task _worker;
    private readonly Bitmap _placeholder;

    public MeshThumbnailCache()
    {
        _sync = SynchronizationContext.Current;
        _placeholder = CreatePlaceholder();
        _worker = Task.Run(WorkAsync);
    }

    public event EventHandler? ThumbnailReady;

    public Bitmap Placeholder => _placeholder;

    public Bitmap GetCached(AssetEntry entry)
    {
        string key = BrowserKeys.Mesh(entry);
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out Bitmap? image))
            {
                Touch(key);
                return image;
            }
        }

        return _placeholder;
    }

    public void SetVisible(IReadOnlyList<AssetEntry> entries)
    {
        int queued;
        lock (_gate)
        {
            _visible.Clear();
            foreach (AssetEntry entry in entries)
            {
                string key = BrowserKeys.Mesh(entry);
                if (!_cache.ContainsKey(key)
                    && !_pending.Contains(key)
                    && !_failed.Contains(key))
                {
                    _visible.Add(entry);
                }
            }

            queued = _visible.Count;
        }

        BrowserTiming.Write($"mesh thumb queue {queued}");
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
        lock (_gate)
        {
            foreach (Bitmap image in _cache.Values)
            {
                image.Dispose();
            }

            _cache.Clear();
            _nodes.Clear();
            _order.Clear();
        }

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

            while (!_cancel.IsCancellationRequested)
            {
                AssetEntry? entry;
                string key;
                lock (_gate)
                {
                    if (_visible.Count == 0)
                    {
                        break;
                    }

                    entry = _visible[0];
                    _visible.RemoveAt(0);
                    key = BrowserKeys.Mesh(entry);
                    if (_cache.ContainsKey(key)
                        || _pending.Contains(key)
                        || _failed.Contains(key))
                    {
                        continue;
                    }

                    _pending.Add(key);
                }

                try
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    MeshGeometry? geometry = Load(entry);
                    if (geometry is null)
                    {
                        MarkFailed(key);
                        continue;
                    }

                    Bitmap? image = MeshThumbnailRenderer.Render(geometry);
                    if (image is null)
                    {
                        MarkFailed(key);
                        continue;
                    }

                    watch.Stop();
                    bool ready;
                    lock (_gate)
                    {
                        _pending.Remove(key);
                        ready = !_cancel.IsCancellationRequested;
                        if (ready)
                        {
                            Add(key, image);
                        }
                    }

                    if (!ready)
                    {
                        image.Dispose();
                        return;
                    }

                    BrowserTiming.Write(
                        $"mesh thumb {Path.GetFileName(entry.ArchivePath)} " +
                        $"{entry.NameHash:X16} {watch.ElapsedMilliseconds}ms");
                    PostReady();
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
                    or IOException or UnauthorizedAccessException)
                {
                    MarkFailed(key);
                }
            }
        }
    }

    private static MeshGeometry? Load(AssetEntry entry)
    {
        using FileStream stream = new(
            entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        MeshData data = MeshDecoder.Read(stream, entry.BodyArchivePath, entry.Row);
        return data.Streams.Count == 0 ? null : data.ToGeometry();
    }

    private void MarkFailed(string key)
    {
        lock (_gate)
        {
            _pending.Remove(key);
            _failed.Add(key);
        }
    }

    private void Add(string key, Bitmap image)
    {
        if (_cache.TryGetValue(key, out Bitmap? existing))
        {
            image.Dispose();
            Touch(key);
            return;
        }

        _cache[key] = image;
        _nodes[key] = _order.AddFirst(key);
        Trim();
    }

    private void Touch(string key)
    {
        if (!_nodes.TryGetValue(key, out LinkedListNode<string>? node))
        {
            return;
        }

        _order.Remove(node);
        _order.AddFirst(node);
    }

    private void Trim()
    {
        while (_cache.Count > CacheLimit && _order.Last is { } last)
        {
            _order.RemoveLast();
            _nodes.Remove(last.Value);
            if (_cache.Remove(last.Value, out Bitmap? image))
            {
                image.Dispose();
            }
        }
    }

    private void PostReady()
    {
        EventHandler? handler = ThumbnailReady;
        if (handler is null)
        {
            return;
        }

        void Raise() => handler(this, EventArgs.Empty);
        if (_sync is null)
        {
            Raise();
        }
        else
        {
            _sync.Post(_ => Raise(), null);
        }
    }

    private static Bitmap CreatePlaceholder()
    {
        Bitmap bitmap = new(BrowserVisual.ThumbnailSize, BrowserVisual.ThumbnailSize);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Theme.Surface);
        return bitmap;
    }
}
