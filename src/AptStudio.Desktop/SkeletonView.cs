using System.Globalization;
using System.Numerics;
using AptStudio.Core.Skeletons;

namespace AptStudio.Desktop;

internal sealed class SkeletonView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly TextBox _boneFilter = new();
    private readonly System.Windows.Forms.Timer _boneFilterTimer = new() { Interval = 150 };
    private readonly Button _usedBy = new();
    private readonly DataGridView _bones = new();
    private readonly SkeletonCanvas _canvas = new();
    private readonly SplitContainer _split = new();
    private SkeletonData? _data;
    private bool _suppressBoneFilter;
    private float _dpiScale = 1f;

    public SkeletonView()
    {
        BackColor = Theme.Background;
        DoubleBuffered = true;

        _title.AutoEllipsis = true;
        _title.Font = new Font("Segoe UI Semibold", 9.5f);
        _title.ForeColor = Theme.Text;
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.Padding = new Padding(8, 0, 8, 0);
        _title.Dock = DockStyle.Fill;

        _usedBy.Text = "Used by";
        _usedBy.FlatStyle = FlatStyle.Flat;
        _usedBy.BackColor = Theme.Surface;
        _usedBy.ForeColor = Theme.Accent;
        _usedBy.Font = new Font("Segoe UI Semibold", 8f);
        _usedBy.AutoSize = true;
        _usedBy.MinimumSize = new Size(0, 22);
        _usedBy.Padding = new Padding(8, 1, 8, 1);
        _usedBy.Margin = new Padding(0, 0, 8, 0);
        _usedBy.UseVisualStyleBackColor = false;
        _usedBy.FlatAppearance.BorderSize = 1;
        _usedBy.FlatAppearance.BorderColor = Theme.Border;
        _usedBy.Visible = false;
        _usedBy.Click += (_, _) => UsedByRequested?.Invoke(this, EventArgs.Empty);

        TableLayoutPanel titleRow = new()
        {
            Dock = DockStyle.Top,
            Height = 23,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleRow.Controls.Add(_title, 0, 0);
        titleRow.Controls.Add(_usedBy, 1, 0);

        _subtitle.AutoEllipsis = true;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.TextAlign = ContentAlignment.MiddleLeft;
        _subtitle.Padding = new Padding(8, 0, 8, 0);
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 20;

        _boneFilter.BorderStyle = BorderStyle.FixedSingle;
        _boneFilter.BackColor = Theme.Surface;
        _boneFilter.ForeColor = Theme.Text;
        _boneFilter.Font = new Font("Segoe UI", 8.5f);
        _boneFilter.PlaceholderText = "filter bones";
        _boneFilter.Dock = DockStyle.Fill;
        _boneFilter.Margin = new Padding(0);
        _boneFilter.TextChanged += (_, _) =>
        {
            if (_suppressBoneFilter)
            {
                return;
            }

            _boneFilterTimer.Stop();
            _boneFilterTimer.Start();
        };
        _boneFilterTimer.Tick += (_, _) =>
        {
            _boneFilterTimer.Stop();
            FilterBones(_boneFilter.Text);
        };

        Panel filterRow = new()
        {
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(8, 2, 8, 4),
            BackColor = Theme.Background,
        };
        filterRow.Controls.Add(_boneFilter);

        Panel header = new() { Dock = DockStyle.Top, BackColor = Theme.Background, Height = 71 };
        header.Controls.Add(filterRow);
        header.Controls.Add(_subtitle);
        header.Controls.Add(titleRow);

        ConfigureBones();
        _bones.CurrentCellChanged += (_, _) => _canvas.Highlight(SelectedBone);
        _canvas.Dock = DockStyle.Fill;

        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterWidth = 4;
        _split.BackColor = Theme.Border;
        _split.Panel1.BackColor = Theme.Background;
        _split.Panel2.BackColor = Theme.Background;
        _split.Panel1.Controls.Add(_bones);
        _split.Panel2.Controls.Add(_canvas);

        Controls.Add(_split);
        Controls.Add(header);
    }

    public int SelectedBone
    {
        get
        {
            int row = _bones.CurrentCell?.RowIndex ?? -1;
            return row >= 0 && row < _bones.Rows.Count && _bones.Rows[row].Tag is int index
                ? index
                : -1;
        }
    }

    public event EventHandler? UsedByRequested;

    public void SetUsedBy(int count)
    {
        if (count < 0)
        {
            _usedBy.Text = "Used by...";
            _usedBy.Enabled = false;
            _usedBy.Visible = true;
            return;
        }

        if (count == 0)
        {
            _usedBy.Visible = false;
            return;
        }

        _usedBy.Text = $"Used by {count:N0}";
        _usedBy.Enabled = true;
        _usedBy.Visible = true;
    }

    public void SetSkeleton(string title, string subtitle, SkeletonData data)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _usedBy.Visible = false;
        _data = data;
        RenderBones();
        _canvas.SetBones(data.Bones, data.Head.NameHash);
    }

    internal void FilterBones(string text)
    {
        if (!_boneFilter.Text.Equals(text, StringComparison.Ordinal))
        {
            _suppressBoneFilter = true;
            _boneFilter.Text = text;
            _suppressBoneFilter = false;
        }

        RenderBones();
    }

    private void RenderBones()
    {
        _bones.Rows.Clear();
        if (_data is not null)
        {
            string needle = _boneFilter.Text.Trim();
            for (int index = 0; index < _data.Bones.Count; index++)
            {
                SkeletonBone bone = _data.Bones[index];
                string hash = bone.NameHash.ToString("X16", CultureInfo.InvariantCulture);
                if (needle.Length > 0
                    && !hash.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Vector3 position = bone.Position;
                int row = _bones.Rows.Add(
                    hash,
                    bone.ParentIndex.ToString(CultureInfo.InvariantCulture),
                    bone.FirstChildIndex.ToString(CultureInfo.InvariantCulture),
                    position.X.ToString("F3", CultureInfo.InvariantCulture),
                    position.Y.ToString("F3", CultureInfo.InvariantCulture),
                    position.Z.ToString("F3", CultureInfo.InvariantCulture));
                _bones.Rows[row].Tag = index;
            }
        }

        _bones.ClearSelection();
        if (_bones.Rows.Count > 0)
        {
            _bones.CurrentCell = _bones.Rows[0].Cells[0];
            _bones.Rows[0].Selected = true;
        }

        _canvas.Highlight(SelectedBone);
    }

    public void Reset()
    {
        _title.Text = "";
        _subtitle.Text = "";
        _usedBy.Visible = false;
        _data = null;
        _bones.Rows.Clear();
        FilterBones("");
        _canvas.SetBones(Array.Empty<SkeletonBone>(), 0);
    }

    public void SetMessage(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _usedBy.Visible = false;
        _data = null;
        _bones.Rows.Clear();
        FilterBones("");
        _canvas.SetBones(Array.Empty<SkeletonBone>(), 0);
    }

    public void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        _dpiScale = scale;
        _title.Font = new Font("Segoe UI Semibold", 9.5f * scale);
        _boneFilter.Font = new Font("Segoe UI", 8.5f * scale);
        _bones.RowTemplate.Height = (int)Math.Round(20 * scale);
        _bones.ColumnHeadersHeight = (int)Math.Round(22 * scale);
        _bones.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8f * scale);
        foreach (DataGridViewColumn column in _bones.Columns)
        {
            if (column.Tag is int width)
            {
                column.Width = (int)Math.Round(width * scale);
            }
        }

        _canvas.SetScale(scale);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (_split.Width > 240)
        {
            _split.SplitterDistance = Math.Clamp(
                _split.Width * 29 / 50, 120, _split.Width - 120);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boneFilterTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ConfigureBones()
    {
        _bones.Dock = DockStyle.Fill;
        _bones.BackgroundColor = Theme.Background;
        _bones.GridColor = Theme.Border;
        _bones.BorderStyle = BorderStyle.None;
        _bones.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _bones.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _bones.EnableHeadersVisualStyles = false;
        _bones.ColumnHeadersHeight = 22;
        _bones.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _bones.RowHeadersVisible = false;
        _bones.AllowUserToAddRows = false;
        _bones.AllowUserToDeleteRows = false;
        _bones.AllowUserToResizeRows = false;
        _bones.AllowUserToOrderColumns = false;
        _bones.ReadOnly = true;
        _bones.MultiSelect = false;
        _bones.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _bones.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _bones.RowTemplate.Height = 20;
        _bones.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8f),
            Padding = new Padding(4, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _bones.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(4, 0, 4, 0),
        };
        _bones.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        AddColumn("Bone", 138, true);
        AddColumn("Parent", 52, false, true);
        AddColumn("First", 44, false, true);
        AddColumn("X", 46, false, true);
        AddColumn("Y", 46, false, true);
        AddColumn("Z", 50, false, true);
    }

    private void AddColumn(string header, int width, bool mono, bool right = false)
    {
        DataGridViewTextBoxColumn column = new()
        {
            HeaderText = header,
            Width = (int)Math.Round(width * _dpiScale),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
            Tag = width,
        };
        if (mono)
        {
            column.DefaultCellStyle.Font = new Font("Consolas", 8.5f);
        }

        if (right)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        _bones.Columns.Add(column);
    }
}

internal sealed class SkeletonCanvas : Control
{
    private IReadOnlyList<SkeletonBone> _bones = Array.Empty<SkeletonBone>();
    private ulong _name;
    private float _yaw = 0.6f;
    private float _zoom = 1f;
    private Point _drag;
    private bool _dragging;
    private int _highlight = -1;
    private float _scale = 1f;

    public SkeletonCanvas()
    {
        BackColor = Theme.Background;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint,
            true);
    }

    public void SetBones(IReadOnlyList<SkeletonBone> bones, ulong name)
    {
        _bones = bones;
        _name = name;
        _highlight = -1;
        _yaw = 0.6f;
        _zoom = 1f;
        Invalidate();
    }

    public void Highlight(int index)
    {
        _highlight = index;
        Invalidate();
    }

    public void SetScale(float scale)
    {
        _scale = scale > 0f ? scale : 1f;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Theme.Background);
        if (_bones.Count == 0)
        {
            TextRenderer.DrawText(
                g,
                "Select a skeleton.",
                Font,
                ClientRectangle,
                Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        float cos = MathF.Cos(_yaw);
        float sin = MathF.Sin(_yaw);
        Vector2[] projected = new Vector2[_bones.Count];
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int index = 0; index < _bones.Count; index++)
        {
            Vector3 position = _bones[index].Position;
            float x = position.X * cos - position.Z * sin;
            float y = position.Y;
            projected[index] = new Vector2(x, y);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        float spanX = Math.Max(maxX - minX, 1e-3f);
        float spanY = Math.Max(maxY - minY, 1e-3f);
        float margin = 24f * _scale;
        float fit = Math.Min(
            (ClientSize.Width - margin * 2) / spanX,
            (ClientSize.Height - margin * 2) / spanY);
        fit *= _zoom;
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        PointF Map(Vector2 point) => new(
            ClientSize.Width * 0.5f + (point.X - centerX) * fit,
            ClientSize.Height * 0.5f - (point.Y - centerY) * fit);

        using Pen edge = new(Theme.Muted, 1f * _scale);
        using Pen root = new(Theme.Accent, 1.4f * _scale);
        using SolidBrush dot = new(Theme.Text);
        using SolidBrush highlight = new(Theme.Accent);
        for (int index = 0; index < _bones.Count; index++)
        {
            int parent = _bones[index].ParentIndex;
            if (parent < 0 || parent >= _bones.Count)
            {
                continue;
            }

            PointF a = Map(projected[parent]);
            PointF b = Map(projected[index]);
            g.DrawLine(index == _highlight ? root : edge, a, b);
        }

        for (int index = 0; index < _bones.Count; index++)
        {
            PointF point = Map(projected[index]);
            float radius = (index == _highlight ? 3.2f : 2.2f) * _scale;
            g.FillEllipse(index == _highlight ? highlight : dot, point.X - radius, point.Y - radius, radius * 2, radius * 2);
        }

        TextRenderer.DrawText(
            g,
            $"0x{_name:X16} | {_bones.Count:N0} bones",
            Font,
            new Rectangle(8, 6, ClientSize.Width - 16, 20),
            Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(
            g,
            "drag to turn, wheel to zoom",
            Font,
            new Rectangle(8, 24, ClientSize.Width - 16, 20),
            Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _drag = e.Location;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
        {
            return;
        }

        _yaw += (e.X - _drag.X) * 0.01f;
        _drag = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.1f : 1f / 1.1f), 0.2f, 8f);
        Invalidate();
    }
}
