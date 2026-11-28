using System.Globalization;

namespace AptStudio.Desktop.Browser;

internal sealed class SkinPartView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _binding = new();
    private readonly Button _openSkeleton = new();
    private readonly Button _pose = new();
    private readonly DataGridView _parts = new();
    private readonly SkinPartPreview _preview = new();
    private readonly Label _source = new();
    private ulong _skeletonRoot;
    private bool _suppressPart;

    public SkinPartView()
    {
        BackColor = Theme.Background;
        DoubleBuffered = true;

        _title.AutoEllipsis = true;
        _title.Dock = DockStyle.Top;
        _title.Height = 23;
        _title.Font = BrowserVisual.Title;
        _title.ForeColor = Theme.Text;
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.Padding = new Padding(8, 0, 8, 0);

        _subtitle.AutoEllipsis = true;
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 20;
        _subtitle.Font = BrowserVisual.UiSmall;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.TextAlign = ContentAlignment.MiddleLeft;
        _subtitle.Padding = new Padding(8, 0, 8, 0);

        Panel header = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
        };
        header.Controls.Add(_subtitle);
        header.Controls.Add(_title);

        FlowLayoutPanel bindingStrip = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0),
        };
        Label bindingLabel = new()
        {
            Text = "BINDING",
            AutoSize = true,
            Font = BrowserVisual.Section,
            ForeColor = BrowserVisual.Faint,
            Margin = new Padding(0, 6, 8, 0),
        };
        _binding.AutoSize = true;
        _binding.Font = BrowserVisual.Row;
        _binding.ForeColor = Theme.Text;
        _binding.Margin = new Padding(0, 4, 8, 0);

        BrowserButtons.Style(
            _openSkeleton, BrowserButtonRole.Ordinary, BrowserVisual.SurfaceRaised);
        _openSkeleton.Text = "Open Skeleton";
        _openSkeleton.AutoSize = true;
        _openSkeleton.Margin = new Padding(0, 1, 0, 0);
        _openSkeleton.Enabled = false;
        _openSkeleton.Click += (_, _) =>
        {
            if (_skeletonRoot != 0)
            {
                SkeletonRequested?.Invoke(_skeletonRoot);
            }
        };

        BrowserButtons.Style(
            _pose, BrowserButtonRole.Ordinary, BrowserVisual.SurfaceRaised);
        _pose.Text = "Pose skin";
        _pose.AutoSize = true;
        _pose.Margin = new Padding(0, 1, 0, 0);
        _pose.Enabled = false;
        _pose.Click += (_, _) =>
        {
            if (_skeletonRoot != 0)
            {
                PoseRequested?.Invoke(_skeletonRoot);
            }
        };
        bindingStrip.Controls.Add(bindingLabel);
        bindingStrip.Controls.Add(_binding);
        bindingStrip.Controls.Add(_openSkeleton);
        bindingStrip.Controls.Add(_pose);

        ConfigureParts();
        _preview.MeshRequested += hash => MeshRequested?.Invoke(hash);
        _parts.CurrentCellChanged += (_, _) =>
        {
            if (_suppressPart)
            {
                return;
            }

            int index = _parts.CurrentCell?.RowIndex ?? -1;
            _preview.SelectPart(index);
            PartChanged?.Invoke(index);
        };

        _source.AutoEllipsis = true;
        _source.Dock = DockStyle.Fill;
        _source.Font = BrowserVisual.RowSmall;
        _source.ForeColor = Theme.Muted;
        _source.TextAlign = ContentAlignment.MiddleLeft;
        _source.Padding = new Padding(8, 0, 8, 0);

        Panel bodyPanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
        };
        _parts.Dock = DockStyle.Left;
        _parts.Width = 330;
        Panel spacer = new()
        {
            Dock = DockStyle.Left,
            Width = 8,
            BackColor = Theme.Background,
        };
        _preview.Dock = DockStyle.Fill;
        bodyPanel.Controls.Add(_preview);
        bodyPanel.Controls.Add(spacer);
        bodyPanel.Controls.Add(_parts);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(bindingStrip, 0, 1);
        layout.Controls.Add(bodyPanel, 0, 2);
        layout.Controls.Add(_source, 0, 3);
        Controls.Add(layout);
    }

    public event Action<ulong>? SkeletonRequested;

    public event Action<int>? PartChanged;

    public event Action<ulong>? MeshRequested;

    public event Action<ulong>? PoseRequested;

    public int SelectedPart => _parts.CurrentCell?.RowIndex ?? -1;

    public void SetSkin(
        string title,
        string subtitle,
        string binding,
        ulong skeletonRoot,
        IReadOnlyList<SkinPartDisplay> parts,
        int selected,
        string source)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _binding.Text = binding;
        _skeletonRoot = skeletonRoot;
        _openSkeleton.Enabled = skeletonRoot != 0;
        _pose.Enabled = skeletonRoot != 0;
        _source.Text = source;

        _suppressPart = true;
        try
        {
            _parts.Rows.Clear();
            foreach (SkinPartDisplay part in parts)
            {
                int row = _parts.Rows.Add(
                    part.Row.Index.ToString(CultureInfo.InvariantCulture),
                    part.Row.MeshHash.ToString("X16", CultureInfo.InvariantCulture),
                    part.Row.Vertices.ToString("N0", CultureInfo.InvariantCulture));
                _parts.Rows[row].Tag = part;
            }

            _parts.ClearSelection();
            int target = parts.Count == 0
                ? -1
                : Math.Clamp(selected < 0 ? 0 : selected, 0, parts.Count - 1);
            if (target >= 0)
            {
                _parts.CurrentCell = _parts.Rows[target].Cells[0];
                _parts.Rows[target].Selected = true;
            }
        }
        finally
        {
            _suppressPart = false;
        }

        _preview.SetParts(parts, SelectedPart);
    }

    public void SelectPart(int index)
    {
        if (index < 0 || index >= _parts.Rows.Count)
        {
            return;
        }

        _suppressPart = true;
        try
        {
            _parts.CurrentCell = _parts.Rows[index].Cells[0];
            _parts.Rows[index].Selected = true;
        }
        finally
        {
            _suppressPart = false;
        }

        _preview.SelectPart(index);
    }

    public void SetNote(string text) => _source.Text = text;

    public void Reset()
    {
        _title.Text = "";
        _subtitle.Text = "";
        _binding.Text = "";
        _skeletonRoot = 0;
        _openSkeleton.Enabled = false;
        _pose.Enabled = false;
        _source.Text = "";
        _suppressPart = true;
        try
        {
            _parts.Rows.Clear();
        }
        finally
        {
            _suppressPart = false;
        }

        _preview.Reset();
    }

    private void ConfigureParts()
    {
        _parts.BackgroundColor = Theme.Background;
        _parts.GridColor = Theme.Border;
        _parts.BorderStyle = BorderStyle.None;
        _parts.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _parts.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _parts.EnableHeadersVisualStyles = false;
        _parts.ColumnHeadersHeight = 24;
        _parts.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _parts.RowHeadersVisible = false;
        _parts.AllowUserToAddRows = false;
        _parts.AllowUserToDeleteRows = false;
        _parts.AllowUserToResizeRows = false;
        _parts.AllowUserToOrderColumns = false;
        _parts.ReadOnly = true;
        _parts.MultiSelect = false;
        _parts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _parts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _parts.RowTemplate.Height = 23;
        _parts.ScrollBars = ScrollBars.Vertical;
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

        AddColumn("#", 30, false, false);
        AddColumn("Mesh", 190, true, false);
        AddColumn("Verts", 80, false, true);
    }

    private void AddColumn(string header, int width, bool mono, bool right)
    {
        DataGridViewTextBoxColumn column = new()
        {
            HeaderText = header,
            Width = width,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
        };
        if (mono)
        {
            column.DefaultCellStyle.Font = new Font("Consolas", 8.5f);
        }

        if (right)
        {
            column.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleRight;
        }

        _parts.Columns.Add(column);
    }
}
