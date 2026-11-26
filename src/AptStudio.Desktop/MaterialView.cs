using System.Globalization;

namespace AptStudio.Desktop;

internal sealed record MaterialSlotRow(
    int Index,
    string Authoring,
    string Binding,
    ulong TextureHash,
    string Texture,
    string Archive);

internal sealed class MaterialView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly TextureGrid _slots = new();
    private readonly PreviewControl _preview = new();
    private readonly Button _open = new();
    private readonly Label _note = new();
    private bool _suppressSlot;
    private float _dpiScale = 1f;

    public MaterialView()
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

        ConfigureSlots();
        _slots.CurrentCellChanged += (_, _) => OnSlotChanged();
        _slots.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                RequestTexture();
            }
        };

        _open.Text = "Open Texture";
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
        _open.Click += (_, _) => RequestTexture();

        _note.AutoEllipsis = true;
        _note.ForeColor = Theme.Muted;
        _note.TextAlign = ContentAlignment.MiddleLeft;
        _note.Dock = DockStyle.Fill;

        TableLayoutPanel bar = new()
        {
            Dock = DockStyle.Fill,
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

        _preview.Dock = DockStyle.Fill;
        _preview.ShowMessage("Select a material.", false);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_slots, 0, 1);
        layout.Controls.Add(bar, 0, 2);
        layout.Controls.Add(_preview, 0, 3);
        Controls.Add(layout);
    }

    public event Action<ulong>? SlotSelected;

    public event Action<ulong>? TextureRequested;

    public ulong? SelectedHash
    {
        get
        {
            int row = _slots.CurrentCell?.RowIndex ?? -1;
            return row >= 0 && row < _slots.Rows.Count && _slots.Rows[row].Tag is ulong hash
                ? hash
                : null;
        }
    }

    public void SetMaterial(
        string title,
        string subtitle,
        IReadOnlyList<MaterialSlotRow> slots)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _suppressSlot = true;
        try
        {
            _slots.Rows.Clear();
            foreach (MaterialSlotRow slot in slots)
            {
                int index = _slots.Rows.Add(
                    slot.Index.ToString(CultureInfo.InvariantCulture),
                    slot.Authoring,
                    slot.Binding,
                    slot.Texture,
                    slot.Archive);
                _slots.Rows[index].Tag = slot.TextureHash;
            }

            _slots.ClearSelection();
            if (_slots.Rows.Count > 0)
            {
                _slots.CurrentCell = _slots.Rows[0].Cells[0];
                _slots.Rows[0].Selected = true;
            }
        }
        finally
        {
            _suppressSlot = false;
        }

        UpdateOpenEnabled();
    }

    public void SetPreview(Bitmap image, string note)
    {
        _preview.SetImage(image);
        _note.Text = note;
    }

    public void ShowPreviewMessage(string text, bool error)
    {
        _preview.ShowMessage(text, error);
        _note.Text = "";
    }

    public void SelectSlot(int index)
    {
        if (index >= 0 && index < _slots.Rows.Count)
        {
            _slots.CurrentCell = _slots.Rows[index].Cells[0];
            _slots.Rows[index].Selected = true;
        }
    }

    public void Reset()
    {
        _title.Text = "";
        _subtitle.Text = "";
        _suppressSlot = true;
        try
        {
            _slots.Rows.Clear();
        }
        finally
        {
            _suppressSlot = false;
        }

        _preview.ShowMessage("Select a material.", false);
        _note.Text = "";
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
        _slots.RowTemplate.Height = (int)Math.Round(20 * scale);
        _slots.ColumnHeadersHeight = (int)Math.Round(22 * scale);
        _slots.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f * scale);
        foreach (DataGridViewColumn column in _slots.Columns)
        {
            if (column.Tag is int width)
            {
                column.Width = (int)Math.Round(width * scale);
            }

            if (column.DefaultCellStyle.Font is not null)
            {
                column.DefaultCellStyle.Font = new Font("Consolas", 8.5f * scale);
            }
        }

        _preview.SetDpiScale(scale);
    }

    private void ConfigureSlots()
    {
        _slots.Dock = DockStyle.Fill;
        _slots.BackgroundColor = Theme.Background;
        _slots.GridColor = Theme.Border;
        _slots.BorderStyle = BorderStyle.None;
        _slots.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _slots.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _slots.EnableHeadersVisualStyles = false;
        _slots.ColumnHeadersHeight = 22;
        _slots.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _slots.RowHeadersVisible = false;
        _slots.AllowUserToAddRows = false;
        _slots.AllowUserToDeleteRows = false;
        _slots.AllowUserToResizeRows = false;
        _slots.AllowUserToOrderColumns = false;
        _slots.ReadOnly = true;
        _slots.MultiSelect = false;
        _slots.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _slots.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _slots.RowTemplate.Height = 20;
        _slots.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Padding = new Padding(4, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _slots.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(4, 0, 4, 0),
        };
        _slots.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        AddColumn("#", 30, true);
        AddColumn("Slot", 100, false);
        AddColumn("Binding", 118, false);
        AddColumn("Texture", 145, true);
        AddColumn("Archive", 100, false);
    }

    private void AddColumn(string header, int width, bool mono)
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

        if (width <= 30)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        _slots.Columns.Add(column);
    }

    private void OnSlotChanged()
    {
        UpdateOpenEnabled();
        if (_suppressSlot || SelectedHash is not ulong hash)
        {
            return;
        }

        SlotSelected?.Invoke(hash);
    }

    private void UpdateOpenEnabled()
    {
        _open.Enabled = SelectedHash is not null;
    }

    private void RequestTexture()
    {
        if (SelectedHash is ulong hash)
        {
            TextureRequested?.Invoke(hash);
        }
    }
}
