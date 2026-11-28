// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using AptStudio.Core.Meshes;

namespace AptStudio.Desktop.Browser;

internal sealed record SkinPartDisplay(SkinPartRow Row, MeshGeometry? Geometry);

internal sealed class SkinPartPreview : Panel
{
    private readonly Label _contextLabel = new();
    private readonly SkinContextView _context = new();
    private readonly Label _selectedLabel = new();
    private readonly MeshView _isolated = new();
    private readonly Label _title = new();
    private readonly Label _hash = new();
    private readonly Label _meta = new();
    private readonly Label _meta2 = new();
    private readonly Button _openMesh = new();
    private IReadOnlyList<SkinPartDisplay> _parts = Array.Empty<SkinPartDisplay>();
    private ulong _selectedMeshHash;

    public event Action<ulong>? MeshRequested;

    public SkinPartPreview()
    {
        BackColor = Theme.Background;

        _contextLabel.Text = "IN CONTEXT";
        _contextLabel.Dock = DockStyle.Top;
        _contextLabel.Height = 18;
        _contextLabel.Font = BrowserVisual.Section;
        _contextLabel.ForeColor = BrowserVisual.Faint;
        _contextLabel.TextAlign = ContentAlignment.MiddleLeft;

        _context.Dock = DockStyle.Fill;

        _selectedLabel.Text = "SELECTED PART";
        _selectedLabel.Dock = DockStyle.Top;
        _selectedLabel.Height = 18;
        _selectedLabel.Font = BrowserVisual.Section;
        _selectedLabel.ForeColor = BrowserVisual.Faint;
        _selectedLabel.TextAlign = ContentAlignment.MiddleLeft;

        TableLayoutPanel details = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Theme.Surface,
            Padding = new Padding(10, 6, 8, 4),
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        ConfigureDetail(_title, 9.5f, Theme.Text, true);
        ConfigureDetail(_hash, 8.5f, Theme.Text, false, mono: true);
        ConfigureDetail(_meta, 7.9f, Theme.Muted, false);
        ConfigureDetail(_meta2, 7.9f, Theme.Muted, false);

        BrowserButtons.Style(
            _openMesh, BrowserButtonRole.Ordinary, BrowserVisual.SurfaceRaised);
        _openMesh.Text = "Open mesh";
        _openMesh.AutoSize = false;
        _openMesh.Dock = DockStyle.Fill;
        _openMesh.Height = 26;
        _openMesh.Margin = new Padding(0);
        _openMesh.Enabled = false;
        _openMesh.Click += (_, _) =>
        {
            if (_selectedMeshHash != 0)
            {
                MeshRequested?.Invoke(_selectedMeshHash);
            }
        };

        details.Controls.Add(_title, 0, 0);
        details.Controls.Add(_hash, 0, 1);
        details.Controls.Add(_meta, 0, 2);
        details.Controls.Add(_meta2, 0, 3);
        details.Controls.Add(_openMesh, 0, 4);

        Panel isolatedHost = new()
        {
            Dock = DockStyle.Left,
            Width = 152,
            BackColor = Theme.Background,
            Margin = new Padding(6, 6, 8, 6),
            Padding = new Padding(0),
        };
        isolatedHost.Paint += (_, e) =>
        {
            using Pen border = new(Theme.Border);
            e.Graphics.DrawRectangle(
                border, 0, 0, isolatedHost.ClientSize.Width - 1,
                isolatedHost.ClientSize.Height - 1);
        };
        _isolated.Dock = DockStyle.Fill;
        _isolated.ShowMessage("Select a part.", false);
        isolatedHost.Controls.Add(_isolated);

        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
        };
        card.Controls.Add(details);
        card.Controls.Add(isolatedHost);

        Panel bottom = new()
        {
            Dock = DockStyle.Bottom,
            Height = 172,
            BackColor = Theme.Background,
        };
        bottom.Controls.Add(card);
        bottom.Controls.Add(_selectedLabel);

        Controls.Add(_context);
        Controls.Add(_contextLabel);
        Controls.Add(bottom);
    }

    public void SetParts(IReadOnlyList<SkinPartDisplay> parts, int selected)
    {
        _parts = parts;
        _context.SetParts(
            parts.Select(part => part.Geometry).ToArray(),
            selected);
        SelectPart(selected);
    }

    public void SelectPart(int index)
    {
        if (index < 0 || index >= _parts.Count)
        {
            _selectedMeshHash = 0;
            _openMesh.Enabled = false;
            _isolated.ShowMessage("Select a part.", false);
            _title.Text = "";
            _hash.Text = "";
            _meta.Text = "";
            _meta2.Text = "";
            return;
        }

        SkinPartDisplay part = _parts[index];
        _selectedMeshHash = part.Row.MeshHash;
        _openMesh.Enabled = part.Row.MeshHash != 0;
        _isolated.ShowMessage("No geometry for this part.", true);
        if (part.Geometry is { Positions.Length: > 0 } geometry)
        {
            _isolated.SetMesh(geometry);
        }

        _title.Text = $"PART {part.Row.Index}";
        _hash.Text = $"0x{part.Row.MeshHash:X16}";
        _meta.Text =
            $"{part.Row.Vertices:N0} verts · stride {part.Row.Stride}";
        _meta2.Text = part.Row.PacketsParsed
            ? $"{part.Row.Packets} packet{(part.Row.Packets == 1 ? "" : "s")} · " +
                $"0x{part.Row.MaterialHash:X16}"
            : "packets ? · material ?";
        _context.SelectPart(index);
    }

    public void Reset()
    {
        _parts = Array.Empty<SkinPartDisplay>();
        _selectedMeshHash = 0;
        _openMesh.Enabled = false;
        _context.SetParts(Array.Empty<MeshGeometry?>(), -1);
        _isolated.ShowMessage("Select a part.", false);
        _title.Text = "";
        _hash.Text = "";
        _meta.Text = "";
        _meta2.Text = "";
    }

    private static void ConfigureDetail(
        Label label,
        float size,
        Color color,
        bool semibold,
        bool mono = false)
    {
        label.Dock = DockStyle.Fill;
        label.AutoEllipsis = true;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Font = mono
            ? new Font("Consolas", size)
            : new Font("Segoe UI", size, semibold ? FontStyle.Bold : FontStyle.Regular);
        label.ForeColor = color;
    }
}

internal sealed class SkinContextView : Control
{
    private IReadOnlyList<MeshGeometry?> _parts = Array.Empty<MeshGeometry?>();
    private int _selected = -1;
    private Bitmap? _target;
    private bool _hasGeometry;

    public SkinContextView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Background;
    }

    public void SetParts(IReadOnlyList<MeshGeometry?> parts, int selected)
    {
        _parts = parts;
        _selected = selected;
        _hasGeometry = parts.Any(part => part is { Positions.Length: > 0 });
        DisposeTarget();
        Invalidate();
    }

    public void SelectPart(int selected)
    {
        if (_selected == selected)
        {
            return;
        }

        _selected = selected;
        DisposeTarget();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        int width = ClientSize.Width;
        int height = ClientSize.Height;
        if (width <= 0 || height <= 0)
        {
            base.OnPaint(e);
            return;
        }

        if (_target is null
            || _target.Width != width
            || _target.Height != height)
        {
            DisposeTarget();
            _target = SkinPartRenderer.RenderContext(
                _parts, _selected, width, height);
        }

        e.Graphics.DrawImageUnscaled(_target, 0, 0);
        using Pen border = new(Theme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, width - 1, height - 1);
        if (!_hasGeometry)
        {
            TextRenderer.DrawText(
                e.Graphics,
                "No part geometry.",
                BrowserVisual.UiSmall,
                ClientRectangle,
                Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        base.OnPaint(e);
    }

    protected override void OnResize(EventArgs e)
    {
        DisposeTarget();
        base.OnResize(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeTarget();
        }

        base.Dispose(disposing);
    }

    private void DisposeTarget()
    {
        _target?.Dispose();
        _target = null;
    }
}
