// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using System.Globalization;
using AptStudio.Core.Assets;
using AptStudio.Core.Meshes;

namespace AptStudio.Desktop.Browser;

internal sealed class MeshInspector : Panel
{
    private readonly Label _type = new();
    private readonly Label _hash = new();
    private readonly Label _subtitle = new();
    private readonly MeshView _view = new();
    private readonly CheckBox _wire = new();
    private readonly Button _fit = new();
    private readonly Label _camera = new();
    private readonly Button _usedBy = new();
    private bool _suppressCamera;

    public MeshInspector()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(14, 13, 14, 10);

        _type.Text = "MESH_Z · MESH";
        _type.ForeColor = BrowserVisual.Faint;
        _type.Font = BrowserVisual.Section;
        _type.Dock = DockStyle.Top;
        _type.Height = 15;

        _hash.Font = BrowserVisual.MonoStrong;
        _hash.ForeColor = Theme.Text;
        _hash.Dock = DockStyle.Top;
        _hash.Height = 22;
        _hash.AutoEllipsis = true;

        _subtitle.Font = BrowserVisual.UiSmall;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 18;
        _subtitle.AutoEllipsis = true;

        _view.Dock = DockStyle.Fill;
        BrowserVisual.Frame(_view);
        _view.CameraChanged += (_, _) => OnCameraChanged();
        _view.WireframeChanged += (_, _) => WireframeChanged?.Invoke(this, EventArgs.Empty);

        _wire.Text = "Wireframe";
        _wire.AutoSize = true;
        _wire.ForeColor = Theme.Text;
        _wire.Font = BrowserVisual.RowSmall;
        _wire.FlatStyle = FlatStyle.Flat;
        _wire.Margin = new Padding(0, 6, 12, 3);
        _wire.CheckedChanged += (_, _) =>
        {
            if (_suppressCamera)
            {
                return;
            }

            _view.SetWireframe(_wire.Checked);
        };

        BrowserButtons.Style(_fit, BrowserButtonRole.Quiet, Theme.Surface);
        _fit.Text = "Fit";
        _fit.AutoSize = true;
        _fit.Margin = new Padding(0, 4, 0, 0);
        _fit.Click += (_, _) => _view.Fit();

        _camera.Font = BrowserVisual.RowSmall;
        _camera.ForeColor = BrowserVisual.Faint;
        _camera.AutoSize = true;
        _camera.Margin = new Padding(0, 7, 0, 0);

        BrowserButtons.Style(_usedBy, BrowserButtonRole.Primary, Theme.Surface);
        _usedBy.Text = "Used by 0";
        _usedBy.AutoSize = true;
        _usedBy.Enabled = false;
        _usedBy.Click += (_, _) => UsedByRequested?.Invoke(this, EventArgs.Empty);

        FlowLayoutPanel controls = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 4, 0, 0),
        };
        controls.Controls.Add(_wire);
        controls.Controls.Add(_fit);
        controls.Controls.Add(_camera);

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 8, 0, 0),
        };
        actions.Controls.Add(_usedBy);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
        };
        header.Controls.Add(_type, 0, 0);
        header.Controls.Add(_hash, 0, 1);
        header.Controls.Add(_subtitle, 0, 2);
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_view, 0, 1);
        layout.Controls.Add(controls, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);
        ShowEmpty();
    }

    public event EventHandler? UsedByRequested;

    public event EventHandler? CameraChanged;

    public event EventHandler? WireframeChanged;

    public string? MeshKey { get; private set; }

    public MeshCamera Camera => _view.Camera;

    public bool IsWireframe => _wire.Checked;

    public void ShowEmpty()
    {
        MeshKey = null;
        _hash.Text = "";
        _subtitle.Text = "Select a mesh.";
        _camera.Text = "";
        _usedBy.Text = "Used by 0";
        _usedBy.Enabled = false;
        _wire.Enabled = false;
        _fit.Enabled = false;
        _view.ShowMessage("Select a mesh.", false);
    }

    public void SetLoading(AssetEntry entry)
    {
        MeshKey = null;
        _hash.Text = $"0x{entry.NameHash:X16}";
        _subtitle.Text = $"{Path.GetFileName(entry.ArchivePath)} · loading...";
        _wire.Enabled = false;
        _fit.Enabled = false;
        _usedBy.Text = "Used by...";
        _usedBy.Enabled = true;
        _view.ShowMessage("Loading mesh...", false);
    }

    public void SetUsedByPending()
    {
        _usedBy.Text = "Used by...";
        _usedBy.Enabled = true;
    }

    public void SetMesh(
        AssetEntry entry,
        MeshGeometry geometry,
        MeshCamera? camera,
        bool wireframe)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(geometry);
        MeshKey = BrowserKeys.Mesh(entry);
        _hash.Text = $"0x{entry.NameHash:X16}";
        _subtitle.Text =
            $"{Path.GetFileName(entry.ArchivePath)} · " +
            $"{geometry.Positions.Length:N0} verts · " +
            $"{geometry.Indices.Length / 3:N0} tris";
        _usedBy.Text = "Used by 0";
        _usedBy.Enabled = false;
        _wire.Enabled = true;
        _fit.Enabled = true;
        _suppressCamera = true;
        try
        {
            _view.SetMesh(geometry);
            _wire.Checked = wireframe;
            _view.SetWireframe(wireframe);
            _view.SetCamera(camera ?? new MeshCamera(0.65f, 0.35f, 1f, PointF.Empty));
        }
        finally
        {
            _suppressCamera = false;
        }

        UpdateCamera();
    }

    public void SetCamera(MeshCamera camera)
    {
        _suppressCamera = true;
        try
        {
            _view.SetCamera(camera);
        }
        finally
        {
            _suppressCamera = false;
        }

        UpdateCamera();
    }

    public void SetWireframe(bool wireframe)
    {
        _suppressCamera = true;
        try
        {
            _wire.Checked = wireframe;
            _view.SetWireframe(wireframe);
        }
        finally
        {
            _suppressCamera = false;
        }
    }

    public void SetUsedBy(int count)
    {
        _usedBy.Text = $"Used by {count:N0}";
        _usedBy.Enabled = count > 0;
    }

    public void ShowMessage(string message, bool error) => _view.ShowMessage(message, error);

    private void OnCameraChanged()
    {
        UpdateCamera();
        if (!_suppressCamera)
        {
            CameraChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateCamera()
    {
        MeshCamera camera = _view.Camera;
        _camera.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"yaw {camera.Yaw:F2} · pitch {camera.Pitch:F2} · " +
            $"zoom {camera.Zoom:F2} · pan {camera.Pan.X:F0},{camera.Pan.Y:F0}");
    }
}
