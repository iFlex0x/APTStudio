using System.Globalization;

namespace AptStudio.Desktop;

internal enum LinkKind
{
    Skin,
    Material,
}

internal sealed record LinkRow(LinkKind Kind, ulong Hash, string Archive, string Detail)
{
    public string KindName => Kind == LinkKind.Skin ? "Skin_Z" : "Material_Z";

    public string HashText => Hash.ToString("X16", CultureInfo.InvariantCulture);
}

internal sealed class LinkView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly DataGridView _grid = new();
    private readonly Button _back = new();
    private readonly Label _note = new();
    private IReadOnlyList<LinkRow> _rows = Array.Empty<LinkRow>();

    public LinkView()
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

        ConfigureGrid();
        _grid.CellValueNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count)
            {
                return;
            }

            LinkRow row = _rows[e.RowIndex];
            e.Value = e.ColumnIndex switch
            {
                0 => row.KindName,
                1 => row.HashText,
                2 => row.Archive,
                3 => row.Detail,
                _ => null,
            };
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                Activate();
            }
        };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Activate();
                e.Handled = true;
            }
        };

        _back.Text = "Back";
        _back.AutoSize = true;
        _back.FlatStyle = FlatStyle.Flat;
        _back.BackColor = Theme.Surface;
        _back.ForeColor = Theme.Text;
        _back.Padding = new Padding(8, 0, 8, 0);
        _back.Margin = new Padding(8, 3, 8, 3);
        _back.MinimumSize = new Size(0, 24);
        _back.UseVisualStyleBackColor = false;
        _back.FlatAppearance.BorderSize = 0;
        _back.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _back.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _back.Click += (_, _) => BackRequested?.Invoke();

        _note.AutoEllipsis = true;
        _note.ForeColor = Theme.Muted;
        _note.TextAlign = ContentAlignment.MiddleLeft;
        _note.Dock = DockStyle.Fill;
        _note.Text = "double-click a row to open it";

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
        bar.Controls.Add(_back, 0, 0);
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
        layout.Controls.Add(_grid, 0, 1);
        layout.Controls.Add(bar, 0, 2);
        Controls.Add(layout);
    }

    public event Action? BackRequested;

    public event Action<LinkRow>? LinkActivated;

    public void SetLinks(string title, string subtitle, IReadOnlyList<LinkRow> rows)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _rows = rows;
        _grid.RowCount = rows.Count;
        _grid.ClearSelection();
        if (rows.Count > 0)
        {
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
            _grid.Rows[0].Selected = true;
        }
    }

    public void SetBusy(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _rows = Array.Empty<LinkRow>();
        _grid.RowCount = 0;
    }

    public void Reset()
    {
        _title.Text = "";
        _subtitle.Text = "";
        _rows = Array.Empty<LinkRow>();
        _grid.RowCount = 0;
    }

    public void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        _title.Font = new Font("Segoe UI Semibold", 9.5f * scale);
        _grid.RowTemplate.Height = (int)Math.Round(20 * scale);
        _grid.ColumnHeadersHeight = (int)Math.Round(22 * scale);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f * scale);
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            if (column.Tag is int width)
            {
                column.Width = (int)Math.Round(width * scale);
            }
        }
    }

    private void Activate()
    {
        if (_grid.CurrentCell is { RowIndex: >= 0 } cell && cell.RowIndex < _rows.Count)
        {
            LinkActivated?.Invoke(_rows[cell.RowIndex]);
        }
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.VirtualMode = true;
        _grid.BackgroundColor = Theme.Background;
        _grid.GridColor = Theme.Border;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 22;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.RowTemplate.Height = 20;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Padding = new Padding(4, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(4, 0, 4, 0),
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        AddColumn("Type", 80, false);
        AddColumn("Name", 140, true);
        DataGridViewTextBoxColumn archive = AddColumn("Archive", 160, false);
        archive.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        AddColumn("Detail", 140, false);
    }

    internal int RowCount => _grid.RowCount;

    public void ActivateFirst()
    {
        if (_rows.Count > 0)
        {
            LinkActivated?.Invoke(_rows[0]);
        }
    }

    private DataGridViewTextBoxColumn AddColumn(string header, int width, bool mono)
    {
        DataGridViewTextBoxColumn column = new()
        {
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Tag = width,
        };
        if (mono)
        {
            column.DefaultCellStyle.Font = new Font("Consolas", 9f);
        }

        _grid.Columns.Add(column);
        return column;
    }

    public void SelectFirst()
    {
        if (_grid.RowCount > 0)
        {
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
            _grid.Rows[0].Selected = true;
        }
    }
}
