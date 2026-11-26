using System.Globalization;

namespace AptStudio.Desktop;

internal sealed record SkinPartRow(
    int Index,
    ulong MeshHash,
    int Vertices,
    int Stride,
    int Packets,
    bool PacketsParsed,
    ulong MaterialHash);

internal sealed class SkinView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly DataGridView _parts = new();
    private readonly Button _open = new();
    private readonly Label _note = new();
    private float _dpiScale = 1f;

    public SkinView()
    {
        BackColor = Theme.Background;
        DoubleBuffered = true;

        _title.AutoEllipsis = true;
        _title.Font = new Font("Segoe UI Semibold", 9.5f);
        _title.ForeColor = Theme.Text;
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.Padding = new Padding(8, 0, 8, 0);
        _title.Dock = DockStyle.Top;
        _title.Height = 23;

        _subtitle.AutoEllipsis = true;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.TextAlign = ContentAlignment.MiddleLeft;
        _subtitle.Padding = new Padding(8, 0, 8, 0);
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 20;

        Panel header = new() { Dock = DockStyle.Fill, BackColor = Theme.Background, Height = 43 };
        header.Controls.Add(_subtitle);
        header.Controls.Add(_title);

        ConfigureParts();
        _parts.CurrentCellChanged += (_, _) => UpdateOpenEnabled();
        _parts.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                RequestMesh();
            }
        };

        _open.Text = "Open Mesh";
        _open.AutoSize = true;
        _open.FlatStyle = FlatStyle.Flat;
        _open.BackColor = Theme.Surface;
        _open.ForeColor = Theme.Text;
        _open.Padding = new Padding(8, 0, 8, 0);
        _open.Margin = new Padding(8, 3, 8, 3);
        _open.MinimumSize = new Size(0, 24);
        _open.UseVisualStyleBackColor = false;
        _open.FlatAppearance.BorderSize = 0;
        _open.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _open.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _open.Enabled = false;
        _open.Click += (_, _) => RequestMesh();

        _note.AutoEllipsis = true;
        _note.ForeColor = Theme.Muted;
        _note.TextAlign = ContentAlignment.MiddleLeft;
        _note.Dock = DockStyle.Fill;
        _note.Text = "double-click a part to open its mesh";

        TableLayoutPanel bar = new()
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
            Margin = Padding.Empty,
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.Controls.Add(_open, 0, 0);
        bar.Controls.Add(_note, 1, 0);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_parts, 0, 1);
        layout.Controls.Add(bar, 0, 2);
        Controls.Add(layout);
    }

    public event Action<ulong>? MeshRequested;

    public ulong? SelectedMeshHash
    {
        get
        {
            int row = _parts.CurrentCell?.RowIndex ?? -1;
            return row >= 0 && row < _parts.Rows.Count && _parts.Rows[row].Tag is ulong hash
                ? hash
                : null;
        }
    }

    public void SetSkin(string title, string subtitle, IReadOnlyList<SkinPartRow> parts)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _parts.Rows.Clear();
        foreach (SkinPartRow part in parts)
        {
            int index = _parts.Rows.Add(
                part.Index.ToString(CultureInfo.InvariantCulture),
                part.MeshHash.ToString("X16", CultureInfo.InvariantCulture),
                part.Vertices.ToString("N0", CultureInfo.InvariantCulture),
                part.Stride.ToString(CultureInfo.InvariantCulture),
                part.PacketsParsed
                    ? part.Packets.ToString(CultureInfo.InvariantCulture)
                    : "?",
                part.PacketsParsed
                    ? part.MaterialHash.ToString("X16", CultureInfo.InvariantCulture)
                    : "");
            _parts.Rows[index].Tag = part.MeshHash;
        }

        _parts.ClearSelection();
        if (_parts.Rows.Count > 0)
        {
            _parts.CurrentCell = _parts.Rows[0].Cells[0];
            _parts.Rows[0].Selected = true;
        }

        UpdateOpenEnabled();
    }

    public void SelectPart(int index)
    {
        if (index >= 0 && index < _parts.Rows.Count)
        {
            _parts.CurrentCell = _parts.Rows[index].Cells[0];
            _parts.Rows[index].Selected = true;
        }
    }

    public void Reset()
    {
        _title.Text = "";
        _subtitle.Text = "";
        _parts.Rows.Clear();
        UpdateOpenEnabled();
    }

    public void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        _dpiScale = scale;
        _title.Font = new Font("Segoe UI Semibold", 9.5f * scale);
        _parts.RowTemplate.Height = (int)Math.Round(20 * scale);
        _parts.ColumnHeadersHeight = (int)Math.Round(22 * scale);
        _parts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f * scale);
        foreach (DataGridViewColumn column in _parts.Columns)
        {
            if (column.Tag is int width)
            {
                column.Width = (int)Math.Round(width * scale);
            }
        }
    }

    private void ConfigureParts()
    {
        _parts.Dock = DockStyle.Fill;
        _parts.BackgroundColor = Theme.Background;
        _parts.GridColor = Theme.Border;
        _parts.BorderStyle = BorderStyle.None;
        _parts.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _parts.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _parts.EnableHeadersVisualStyles = false;
        _parts.ColumnHeadersHeight = 22;
        _parts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _parts.RowHeadersVisible = false;
        _parts.AllowUserToAddRows = false;
        _parts.AllowUserToDeleteRows = false;
        _parts.AllowUserToResizeRows = false;
        _parts.AllowUserToOrderColumns = false;
        _parts.ReadOnly = true;
        _parts.MultiSelect = false;
        _parts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _parts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _parts.RowTemplate.Height = 20;
        _parts.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Padding = new Padding(4, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _parts.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(4, 0, 4, 0),
        };
        _parts.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        AddColumn("#", 30, false, true);
        AddColumn("Mesh", 146, true);
        AddColumn("Verts", 58, false, true);
        AddColumn("Stride", 56, false, true);
        AddColumn("Packets", 64, false, true);
        AddColumn("Material packet", 150, true);
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

        _parts.Columns.Add(column);
    }

    private void UpdateOpenEnabled()
    {
        _open.Enabled = SelectedMeshHash is not null;
    }

    private void RequestMesh()
    {
        if (SelectedMeshHash is ulong hash)
        {
            MeshRequested?.Invoke(hash);
        }
    }
}
