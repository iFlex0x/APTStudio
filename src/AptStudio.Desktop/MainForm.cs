using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using AptStudio.Core.Games;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop;

internal sealed class MainForm : Form
{
    private enum SortColumn
    {
        Hash,
        Format,
        Size,
        Mips,
        Surfaces,
        Storage,
        Archive,
    }

    private readonly Label _pathLabel = new();
    private readonly Label _countLabel = new();
    private readonly Label _infoLabel = new();
    private readonly Label _zoomLabel = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _selectionLabel = new();
    private readonly TextureGrid _grid = new();
    private readonly PreviewControl _preview = new();
    private readonly DarkComboBox _typeCombo = new();
    private readonly DarkComboBox _formatCombo = new();
    private readonly DarkComboBox _mipCombo = new();
    private readonly DarkComboBox _channelCombo = new();
    private readonly TextBox _filterText = new();
    private readonly CheckBox _previewableCheck = new();
    private readonly Button _openButton = MakeButton("Open Game Folder", Theme.Accent);
    private readonly Button _rescanButton = MakeButton("Rescan", Theme.Text);
    private readonly Button _exportButton = MakeButton("Export Selected", Theme.Text);
    private readonly Button _cancelButton = MakeButton("Cancel", Theme.Error);
    private readonly Button _fitButton = MakeButton("Fit", Theme.Text);
    private readonly Button _actualButton = MakeButton("1:1", Theme.Text);
    private readonly Button _clearButton = MakeButton("Clear", Theme.Muted);
    private readonly SplitContainer _split = new();
    private Font _monoFont = new("Consolas", 9f);
    private readonly System.Windows.Forms.Timer _selectionTimer = new() { Interval = 120 };
    private readonly System.Windows.Forms.Timer _filterTimer = new() { Interval = 150 };

    private IReadOnlyList<TextureEntry> _entries = Array.Empty<TextureEntry>();
    private int[] _view = Array.Empty<int>();
    private GameProfile? _profile;
    private CancellationTokenSource? _scanCancel;
    private CancellationTokenSource? _previewCancel;
    private CancellationTokenSource? _exportCancel;
    private bool _suppressFilters;
    private bool _suppressMip;
    private bool _updatingView;
    private bool _scanning;
    private bool _exporting;
    private bool _screenshotMode;
    private float _shotScale = 1f;
    private TaskCompletionSource<bool> _previewReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private SortColumn _sortColumn = SortColumn.Hash;
    private bool _sortAscending = true;

    public MainForm()
    {
        Text = "APTStudio";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(960, 600);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        KeyPreview = true;
        KeyDown += OnKeyDown;
        FormClosing += OnFormClosing;
        DpiChanged += (_, _) => OnDpiChanged();

        ConfigureGrid();
        ConfigurePreview();
        _openButton.Click += (_, _) => OpenGameFolder();
        _rescanButton.Click += (_, _) => Rescan();
        _exportButton.Click += (_, _) => _ = ExportSelectedAsync();
        _cancelButton.Click += (_, _) => _exportCancel?.Cancel();
        _fitButton.Click += (_, _) => _preview.Fit();
        _actualButton.Click += (_, _) => _preview.ActualSize();
        _clearButton.Click += (_, _) => ClearFilters();
        _typeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressFilters)
            {
                ApplyFilter(false);
            }
        };
        _formatCombo.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressFilters)
            {
                ApplyFilter(false);
            }
        };
        _previewableCheck.CheckedChanged += (_, _) =>
        {
            if (!_suppressFilters)
            {
                ApplyFilter(false);
            }
        };
        _filterText.TextChanged += (_, _) =>
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        };
        _mipCombo.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressMip)
            {
                _ = PreviewSelectionAsync(keepMip: true);
            }
        };
        _channelCombo.SelectedIndexChanged += (_, _) =>
        {
            _preview.SetChannel((ChannelView)_channelCombo.SelectedIndex);
        };
        _selectionTimer.Tick += (_, _) =>
        {
            _selectionTimer.Stop();
            _ = PreviewSelectionAsync(keepMip: false);
        };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            ApplyFilter(false);
        };

        Controls.Add(BuildContent());
        Controls.Add(BuildFilters());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatus());
        _exportButton.Enabled = false;
        _cancelButton.Visible = false;
        _statusLabel.Text = "No game folder selected.";
        _selectionLabel.Text = "0 selected";
        UpdateSortGlyphs();
        UpdateZoomLabel();
        Shown += OnShown;
    }

    protected override bool ShowWithoutActivation => _screenshotMode || base.ShowWithoutActivation;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scanCancel?.Dispose();
            _previewCancel?.Dispose();
            _exportCancel?.Dispose();
            _selectionTimer.Dispose();
            _filterTimer.Dispose();
            _monoFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Button MakeButton(string text, Color color)
    {
        Button button = new()
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = color,
            Padding = new Padding(8, 0, 8, 0),
            Margin = new Padding(0, 0, 6, 0),
            UseVisualStyleBackColor = false,
            MinimumSize = new Size(0, 26),
            TabStop = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        button.FlatAppearance.MouseDownBackColor = Theme.Selection;
        return button;
    }

    private Control BuildToolbar()
    {
        TableLayoutPanel table = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(8, 0, 8, 0),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        FlowLayoutPanel buttons = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 7, 0, 7),
            BackColor = Theme.Surface,
        };
        buttons.Controls.AddRange([_openButton, _rescanButton, _exportButton, _cancelButton]);

        _pathLabel.Dock = DockStyle.Fill;
        _pathLabel.AutoEllipsis = true;
        _pathLabel.TextAlign = ContentAlignment.MiddleRight;
        _pathLabel.ForeColor = Theme.Muted;
        _pathLabel.Padding = new Padding(8, 0, 0, 0);
        _pathLabel.Text = "No game folder selected";

        table.Controls.Add(buttons, 0, 0);
        table.Controls.Add(_pathLabel, 1, 0);
        return table;
    }

    private Control BuildFilters()
    {
        _filterText.BorderStyle = BorderStyle.FixedSingle;
        _filterText.Width = 240;
        _filterText.BackColor = Theme.Surface;
        _filterText.ForeColor = Theme.Text;
        _filterText.PlaceholderText = "hash, archive, format";
        _filterText.Margin = new Padding(0, 0, 6, 0);

        TableLayoutPanel table = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
            Padding = new Padding(8, 0, 8, 0),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        FlowLayoutPanel filters = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 5, 0, 5),
            BackColor = Theme.Background,
        };
        _typeCombo.Width = 120;
        _typeCombo.Margin = new Padding(0, 0, 6, 0);
        _formatCombo.Width = 165;
        _formatCombo.Margin = new Padding(0, 0, 6, 0);
        _previewableCheck.AutoSize = true;
        _previewableCheck.ForeColor = Theme.Text;
        _previewableCheck.Margin = new Padding(4, 6, 6, 0);
        _clearButton.Margin = new Padding(0, 0, 0, 0);

        filters.Controls.AddRange(
        [
            _filterText,
            _typeCombo,
            _formatCombo,
            _previewableCheck,
            _clearButton,
        ]);

        _countLabel.AutoSize = true;
        _countLabel.TextAlign = ContentAlignment.MiddleRight;
        _countLabel.ForeColor = Theme.Muted;
        _countLabel.Margin = new Padding(8, 8, 0, 0);
        _countLabel.Text = "0 of 0";

        table.Controls.Add(filters, 0, 0);
        table.Controls.Add(_countLabel, 1, 0);
        return table;
    }

    private Control BuildContent()
    {
        _split.Dock = DockStyle.Fill;
        _split.SplitterWidth = 4;
        _split.BackColor = Theme.Border;
        _split.Panel1.BackColor = Theme.Background;
        _split.Panel2.BackColor = Theme.Background;
        _split.Panel1.Controls.Add(_grid);
        _split.Panel2.Controls.Add(BuildPreviewPanel());
        return _split;
    }

    private Control BuildPreviewPanel()
    {
        Panel bar = new()
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Theme.Surface,
            Padding = new Padding(6, 4, 6, 4),
        };
        FlowLayoutPanel tools = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            BackColor = Theme.Surface,
        };
        _fitButton.Margin = new Padding(0, 0, 4, 0);
        _actualButton.Margin = new Padding(0, 0, 6, 0);
        _zoomLabel.Width = 44;
        _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
        _zoomLabel.ForeColor = Theme.Muted;
        _zoomLabel.Margin = new Padding(0, 0, 6, 0);
        _mipCombo.Width = 140;
        _mipCombo.Margin = new Padding(0, 0, 6, 0);
        _channelCombo.Width = 100;
        _channelCombo.Items.AddRange(["RGB", "Alpha", "Red", "Green", "Blue"]);
        _channelCombo.SelectedIndex = 0;
        tools.Controls.AddRange([_fitButton, _actualButton, _zoomLabel, _mipCombo, _channelCombo]);
        bar.Controls.Add(tools);

        _infoLabel.Dock = DockStyle.Bottom;
        _infoLabel.Height = 22;
        _infoLabel.TextAlign = ContentAlignment.MiddleLeft;
        _infoLabel.ForeColor = Theme.Muted;
        _infoLabel.Padding = new Padding(8, 0, 0, 0);
        _infoLabel.Text = "";

        _preview.Dock = DockStyle.Fill;

        Panel panel = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };
        panel.Controls.Add(_preview);
        panel.Controls.Add(_infoLabel);
        panel.Controls.Add(bar);
        return panel;
    }

    private Control BuildStatus()
    {
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Theme.Muted;
        _statusLabel.Text = "Open the game folder to begin.";

        _selectionLabel.ForeColor = Theme.Muted;
        _selectionLabel.Text = "0 selected";

        StatusStrip status = new()
        {
            BackColor = Theme.Surface,
            SizingGrip = false,
            Renderer = new DarkToolStripRenderer(),
            ShowItemToolTips = true,
        };
        status.Items.Add(_statusLabel);
        status.Items.Add(_selectionLabel);
        return status;
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
        _grid.ColumnHeadersHeight = 26;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.RowTemplate.Height = 22;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 9f),
            Padding = new Padding(6, 0, 6, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(6, 0, 6, 0),
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        AddColumn("Hash", 150, SortColumn.Hash, mono: true);
        AddColumn("Format", 110, SortColumn.Format);
        AddColumn("Size", 100, SortColumn.Size, right: true);
        AddColumn("Mips", 50, SortColumn.Mips, right: true);
        AddColumn("Surf", 50, SortColumn.Surfaces, right: true);
        AddColumn("Storage", 84, SortColumn.Storage);
        AddColumn("Archive", 150, SortColumn.Archive);

        _grid.CellValueNeeded += OnCellValueNeeded;
        _grid.SelectionChanged += OnGridSelectionChanged;
        _grid.ColumnHeaderMouseClick += OnColumnHeaderClick;
        _grid.CellMouseDown += OnCellMouseDown;
        _grid.ContextMenuStrip = BuildGridMenu();
    }

    private void AddColumn(string header, int width, SortColumn sort, bool mono = false, bool right = false)
    {
        DataGridViewTextBoxColumn column = new()
        {
            HeaderText = header,
            Width = width,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.Programmatic,
            ReadOnly = true,
            Tag = sort,
        };
        column.HeaderCell.Tag = header;
        if (mono)
        {
            column.DefaultCellStyle.Font = _monoFont;
        }

        if (right)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        _grid.Columns.Add(column);
    }

    private ContextMenuStrip BuildGridMenu()
    {
        ToolStripMenuItem export = new("Export Selected") { ForeColor = Theme.Text };
        ToolStripMenuItem copy = new("Copy Hash") { ForeColor = Theme.Text };
        export.Click += (_, _) => _ = ExportSelectedAsync();
        copy.Click += (_, _) => CopyHash();

        ContextMenuStrip menu = new()
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            ShowImageMargin = false,
        };
        menu.Items.Add(export);
        menu.Items.Add(copy);
        menu.Opening += (_, _) =>
        {
            bool any = _grid.SelectedRows.Count > 0;
            export.Enabled = any;
            copy.Enabled = any;
        };
        return menu;
    }

    private void ConfigurePreview()
    {
        _preview.ZoomChanged += (_, _) => UpdateZoomLabel();
    }

    private void OnShown(object? sender, EventArgs e)
    {
        ConfigureSplit();
        ScaleGrid();
        _grid.Focus();
        if (_screenshotMode)
        {
            return;
        }

        string? last = Settings.LoadLastGameRoot();
        if (last is not null)
        {
            _ = LoadGameAsync(last, rebuild: false);
        }
        else
        {
            _statusLabel.Text = "Open the game folder to begin.";
        }
    }

    private void ScaleGrid()
    {
        float scale = DeviceDpi / 96f * _shotScale;
        int[] widths = [150, 110, 100, 50, 50, 84, 150];
        for (int i = 0; i < _grid.Columns.Count && i < widths.Length; i++)
        {
            _grid.Columns[i].Width = (int)Math.Round(widths[i] * scale);
        }

        _grid.RowTemplate.Height = (int)Math.Round(22 * scale);
        _grid.ColumnHeadersHeight = (int)Math.Round(26 * scale);
    }

    private void ConfigureSplit()
    {
        float scale = DeviceDpi / 96f;
        _split.Panel1MinSize = (int)Math.Round(420 * scale);
        _split.Panel2MinSize = (int)Math.Round(400 * scale);
        int width = _split.ClientSize.Width;
        int maximum = width - _split.SplitterWidth - _split.Panel2MinSize;
        if (maximum >= _split.Panel1MinSize)
        {
            _split.SplitterDistance = Math.Clamp((int)(width * 0.58), _split.Panel1MinSize, maximum);
        }
    }

    private void OnDpiChanged()
    {
        ConfigureSplit();
        ScaleGrid();
        if (_entries.Count > 0 && !_scanning)
        {
            ApplyFilter(false);
        }
    }

    private void OnCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = _grid.Rows[e.RowIndex];
        if (!row.Selected)
        {
            _grid.ClearSelection();
            row.Selected = true;
        }

        int column = Math.Clamp(e.ColumnIndex, 0, _grid.Columns.Count - 1);
        _grid.CurrentCell = row.Cells[column];
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_updatingView)
        {
            return;
        }

        _selectionLabel.Text = $"{_grid.SelectedRows.Count:N0} selected";
        UpdateExportEnabled();
        _selectionTimer.Stop();
        _selectionTimer.Start();
    }

    private void OnColumnHeaderClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Tag is not SortColumn column)
        {
            return;
        }

        if (column == _sortColumn)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        UpdateSortGlyphs();
        ApplyFilter(false);
    }

    private void UpdateSortGlyphs()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            if (column.Tag is not SortColumn sort)
            {
                continue;
            }

            string header = column.HeaderCell.Tag as string ?? column.HeaderText;
            column.HeaderText = sort == _sortColumn
                ? $"{header} {(_sortAscending ? "▲" : "▼")}"
                : header;
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }
    }

    private void OnCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _view.Length || e.ColumnIndex < 0)
        {
            return;
        }

        TextureEntry entry = _entries[_view[e.RowIndex]];
        BitmapHead head = entry.Head;
        e.Value = e.ColumnIndex switch
        {
            0 => head.NameHash.ToString("X16"),
            1 => head.Format.DisplayName,
            2 => head.Kind == BitmapKind.Volume ? $"w={head.Width}" : $"{head.Width}x{head.Height}",
            3 => head.MipCount,
            4 => head.Surfaces,
            5 => head.Storage == BitmapStorage.Raw ? "raw" : "lz4",
            6 => Path.GetFileName(entry.ArchivePath),
            _ => null,
        };
    }

    private void ApplyFilter(bool selectFirst)
    {
        int keep = -1;
        if (_grid.SelectedRows.Count > 0)
        {
            int selected = _grid.SelectedRows[0].Index;
            if (selected >= 0 && selected < _view.Length)
            {
                keep = _view[selected];
            }
        }

        IEnumerable<int> indices = Enumerable.Range(0, _entries.Count);
        FilterItem? kind = _typeCombo.SelectedItem as FilterItem;
        FilterItem? format = _formatCombo.SelectedItem as FilterItem;
        bool previewable = _previewableCheck.Checked;
        string query = NormalizeQuery(_filterText.Text);

        if (kind?.Kind is not null)
        {
            indices = indices.Where(index => _entries[index].Head.Kind == kind.Kind);
        }

        if (format?.Format is not null)
        {
            indices = indices.Where(index =>
                string.Equals(
                    _entries[index].Head.Format.DisplayName, format.Format,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (previewable)
        {
            indices = indices.Where(index => TextureDecoder.CanDecodeMip0(_entries[index]));
        }

        if (query.Length > 0)
        {
            indices = indices.Where(index => Matches(_entries[index], query));
        }

        int[] view = indices.ToArray();
        Array.Sort(view, CompareRows);
        SetView(view, keep, selectFirst);
    }

    private int CompareRows(int a, int b)
    {
        TextureEntry left = _entries[a];
        TextureEntry right = _entries[b];
        int result = _sortColumn switch
        {
            SortColumn.Hash => left.Head.NameHash.CompareTo(right.Head.NameHash),
            SortColumn.Format => StringComparer.OrdinalIgnoreCase.Compare(
                left.Head.Format.DisplayName, right.Head.Format.DisplayName),
            SortColumn.Size => PixelCount(left).CompareTo(PixelCount(right)),
            SortColumn.Mips => left.Head.MipCount.CompareTo(right.Head.MipCount),
            SortColumn.Surfaces => left.Head.Surfaces.CompareTo(right.Head.Surfaces),
            SortColumn.Storage => left.Head.Storage.CompareTo(right.Head.Storage),
            SortColumn.Archive => StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileName(left.ArchivePath), Path.GetFileName(right.ArchivePath)),
            _ => 0,
        };
        if (!_sortAscending)
        {
            result = -result;
        }

        if (result != 0)
        {
            return result;
        }

        result = left.Head.NameHash.CompareTo(right.Head.NameHash);
        if (result != 0)
        {
            return result;
        }

        result = string.CompareOrdinal(left.ArchivePath, right.ArchivePath);
        return result != 0 ? result : a.CompareTo(b);
    }

    private static long PixelCount(TextureEntry entry) =>
        (long)entry.Head.Width * entry.Head.Height;

    private static string NormalizeQuery(string text)
    {
        string query = text.Trim();
        return query.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? query[2..] : query;
    }

    private static bool Matches(TextureEntry entry, string query) =>
        entry.Head.NameHash.ToString("X16").Contains(query, StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(entry.ArchivePath).Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Head.Format.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void SetView(int[] view, int keep, bool selectFirst)
    {
        _updatingView = true;
        try
        {
            _view = view;
            _grid.RowCount = 0;
            _grid.RowCount = view.Length;
            _countLabel.Text = $"{view.Length:N0} of {_entries.Count:N0}";
        }
        finally
        {
            _updatingView = false;
        }

        int row = -1;
        if (keep >= 0)
        {
            row = Array.IndexOf(view, keep);
        }

        if (row < 0 && (selectFirst || keep >= 0) && view.Length > 0)
        {
            row = 0;
        }

        if (row >= 0)
        {
            _grid.ClearSelection();
            _grid.CurrentCell = _grid.Rows[row].Cells[0];
            _grid.Rows[row].Selected = true;
            _selectionLabel.Text = $"{_grid.SelectedRows.Count:N0} selected";
            UpdateExportEnabled();
            _selectionTimer.Stop();
            _ = PreviewSelectionAsync(keepMip: false);
        }
        else
        {
            _grid.ClearSelection();
            CancelPreview();
            _selectionTimer.Stop();
            _selectionLabel.Text = "0 selected";
            UpdateExportEnabled();
            _preview.ShowMessage(
                _entries.Count == 0 ? "No textures in this game folder." : "Select a texture.",
                false);
            _infoLabel.Text = "";
            _suppressMip = true;
            _mipCombo.Items.Clear();
            _suppressMip = false;
            _previewReady.TrySetResult(true);
        }

        _grid.Invalidate();
    }

    private void ClearFilters()
    {
        _suppressFilters = true;
        try
        {
            _filterText.Clear();
            if (_typeCombo.Items.Count > 0)
            {
                _typeCombo.SelectedIndex = 0;
            }

            if (_formatCombo.Items.Count > 0)
            {
                _formatCombo.SelectedIndex = 0;
            }

            _previewableCheck.Checked = false;
        }
        finally
        {
            _suppressFilters = false;
        }

        ApplyFilter(true);
    }

    private TextureEntry? SelectedEntry()
    {
        DataGridViewRow? row = _grid.CurrentRow;
        if (row is null || !row.Selected)
        {
            row = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0] : null;
        }

        return row is not null && row.Index >= 0 && row.Index < _view.Length
            ? _entries[_view[row.Index]]
            : null;
    }

    private List<TextureEntry> SelectedEntries() =>
        _grid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .Where(row => row.Index >= 0 && row.Index < _view.Length)
            .Select(row => _entries[_view[row.Index]])
            .ToList();

    private async Task PreviewSelectionAsync(bool keepMip)
    {
        TextureEntry? entry = SelectedEntry();
        if (entry is null)
        {
            CancelPreview();
            _selectionTimer.Stop();
            _selectionLabel.Text = "0 selected";
            UpdateExportEnabled();
            _preview.ShowMessage("Select a texture.", false);
            _infoLabel.Text = "";
            return;
        }

        if (!keepMip)
        {
            FillMipCombo(entry);
        }

        int mip = _mipCombo.SelectedIndex >= 0 ? _mipCombo.SelectedIndex : 0;
        await DecodePreviewAsync(entry, mip);
    }

    private void FillMipCombo(TextureEntry entry)
    {
        _suppressMip = true;
        try
        {
            _mipCombo.Items.Clear();
            for (int mip = 0; mip < entry.Head.MipCount; mip++)
            {
                int width = Math.Max(1, entry.Head.Width >> mip);
                int height = Math.Max(1, entry.Head.Height >> mip);
                _mipCombo.Items.Add($"Mip {mip} - {width}x{height}");
            }

            if (_mipCombo.Items.Count > 0)
            {
                _mipCombo.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressMip = false;
        }
    }

    private async Task DecodePreviewAsync(TextureEntry entry, int mip)
    {
        CancelPreview();
        CancellationTokenSource cancel = new();
        _previewCancel = cancel;
        _infoLabel.Text = $"{entry.Head.Format.DisplayName} | {Path.GetFileName(entry.ArchivePath)}";

        try
        {
            (Bitmap Bitmap, int Width, int Height) result = await Task.Run(() =>
            {
                using FileStream stream = new(
                    entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                DecodedTexture image = TextureDecoder.DecodeMip(stream, entry, mip);
                return (PreviewBitmap.Create(image), image.Width, image.Height);
            }, cancel.Token);

            if (IsDisposed || cancel != _previewCancel)
            {
                result.Bitmap.Dispose();
                return;
            }

            _preview.SetImage(result.Bitmap);
            _infoLabel.Text =
                $"Mip {mip} | {result.Width:N0}x{result.Height:N0} | {entry.Head.MipCount} mips | {entry.Head.Format.DisplayName} | {Path.GetFileName(entry.ArchivePath)}";
            _previewReady.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
            or IOException or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _previewCancel)
            {
                _preview.ShowMessage(ex.Message, true);
                _infoLabel.Text = $"{entry.Head.Format.DisplayName} | {Path.GetFileName(entry.ArchivePath)}";
                _previewReady.TrySetResult(true);
            }
        }
    }

    private void CancelPreview()
    {
        _previewCancel?.Cancel();
        _previewCancel?.Dispose();
        _previewCancel = null;
    }

    private async Task ExportSelectedAsync()
    {
        if (_exporting)
        {
            return;
        }

        List<TextureEntry> entries = SelectedEntries();
        if (entries.Count == 0)
        {
            return;
        }

        string? lastFolder = _profile is null ? null : Path.Combine(_profile.Root, "..");
        using FolderBrowserDialog dialog = new()
        {
            Description = "Export selected textures",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };
        if (lastFolder is not null && Directory.Exists(lastFolder))
        {
            dialog.SelectedPath = Path.GetFullPath(lastFolder);
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExportEntriesAsync(entries, dialog.SelectedPath);
    }

    private async Task ExportEntriesAsync(List<TextureEntry> entries, string folder)
    {
        if (entries.Count == 0)
        {
            return;
        }

        if (IsInsideGameFolder(folder))
        {
            _statusLabel.Text = "Pick an export folder outside the game folder.";
            return;
        }

        _exporting = true;
        _exportButton.Enabled = false;
        _cancelButton.Visible = true;
        _cancelButton.Enabled = true;
        CancellationTokenSource cancel = new();
        _exportCancel = cancel;
        List<string> failures = new();
        int completed = 0;
        _statusLabel.ToolTipText = "";
        IProgress<int> progress = new Progress<int>(value =>
        {
            if (!IsDisposed && _exporting)
            {
                _statusLabel.Text = $"Exporting {value:N0}/{entries.Count:N0}";
            }
        });
        _statusLabel.Text = $"Exporting 0/{entries.Count:N0}";

        try
        {
            await Task.Run(() =>
            {
                HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
                foreach (TextureEntry entry in entries)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    string name = ExportNames.Unique(
                        folder, entry.ArchivePath, entry.Head.NameHash, used);
                    string path = Path.Combine(folder, name);
                    try
                    {
                        TextureExport.ExportPng(entry, path);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
                        or IOException or UnauthorizedAccessException)
                    {
                        failures.Add($"{name}: {ex.Message}");
                    }

                    completed++;
                    progress.Report(completed);
                }
            }, cancel.Token);

            if (IsDisposed)
            {
                return;
            }

            _statusLabel.Text = failures.Count == 0
                ? $"Exported {completed:N0} textures -> {folder}"
                : $"Exported {completed - failures.Count:N0}, failed {failures.Count:N0} -> {folder}";
            if (failures.Count > 0)
            {
                _statusLabel.ToolTipText = string.Join(
                    Environment.NewLine, failures.Take(20));
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                _statusLabel.Text = $"Export cancelled after {completed:N0} of {entries.Count:N0}";
            }
        }
        finally
        {
            if (_exportCancel == cancel)
            {
                _exportCancel = null;
            }

            cancel.Dispose();
            _exporting = false;
            if (!IsDisposed)
            {
                _cancelButton.Visible = false;
                UpdateExportEnabled();
            }
        }
    }

    private bool IsInsideGameFolder(string folder) =>
        _profile is not null && GameFolderGuard.IsInside(_profile.Root, folder);

    private void CopyHash()
    {
        TextureEntry? entry = SelectedEntry();
        if (entry is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(entry.Head.NameHash.ToString("X16"));
            _statusLabel.Text = $"Copied 0x{entry.Head.NameHash:X16}.";
        }
        catch (ExternalException)
        {
            _statusLabel.Text = "Clipboard is busy. Try again.";
        }
    }

    private void OpenGameFolder()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Select the Resonance game folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        string? start = _profile?.Root ?? Settings.LoadLastGameRoot();
        if (start is not null && Directory.Exists(start))
        {
            dialog.SelectedPath = start;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadGameAsync(dialog.SelectedPath, rebuild: false);
        }
    }

    private void Rescan()
    {
        if (_profile is not null && !_scanning && !_exporting)
        {
            _ = LoadGameAsync(_profile.Root, rebuild: true);
        }
    }

    private async Task LoadGameAsync(string root, bool rebuild)
    {
        if (!GameProfile.TryDetect(root, out GameProfile? profile, out string? error) || profile is null)
        {
            _statusLabel.Text = error ?? "That folder is not a supported game.";
            _previewReady.TrySetResult(true);
            return;
        }

        _scanCancel?.Cancel();
        _scanCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _scanCancel = cancel;
        SetScanning(true);
        _statusLabel.Text = rebuild ? "Rebuilding index..." : "Scanning...";
        _statusLabel.ToolTipText = "";
        Stopwatch watch = Stopwatch.StartNew();
        IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
        {
            if (!IsDisposed && cancel == _scanCancel)
            {
                _statusLabel.Text =
                    $"Scanning {Path.GetFileName(item.ArchivePath)} {item.Index}/{item.Total}";
            }
        });

        try
        {
            TextureCatalog catalog = await Task.Run(
                () => TextureCatalog.Load(profile, rebuild, null, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _scanCancel)
            {
                return;
            }

            watch.Stop();
            _profile = profile;
            _pathLabel.Text = profile.Root;
            Text = $"APTStudio - {profile.Kind}";
            if (!_screenshotMode)
            {
                Settings.SaveLastGameRoot(profile.Root);
            }

            _entries = catalog.Textures;
            RebuildOptions(catalog);
            SetScanning(false);
            ApplyFilter(true);
            int archives = catalog.Archives.Count(
                item => !item.IsExternal && item.TextureCount > 0);
            _statusLabel.Text =
                $"{N(_entries.Count)} textures | {N(catalog.PreviewableCount)} previewable | {archives} archives | {catalog.Failures.Count} failed | {watch.ElapsedMilliseconds} ms";
            if (catalog.Failures.Count > 0)
            {
                _statusLabel.ToolTipText = string.Join(
                    Environment.NewLine, catalog.Failures.Take(20).Select(item => item.Message));
            }
        }
        catch (OperationCanceledException)
        {
            _previewReady.TrySetResult(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _scanCancel)
            {
                _statusLabel.Text = ex.Message;
            }

            _previewReady.TrySetResult(true);
        }
        finally
        {
            if (!IsDisposed && cancel == _scanCancel)
            {
                SetScanning(false);
            }
        }
    }

    private void RebuildOptions(TextureCatalog catalog)
    {
        _suppressFilters = true;
        try
        {
            _typeCombo.Items.Clear();
            _typeCombo.Items.Add(new FilterItem("All types", null, null));
            foreach (IGrouping<BitmapKind, TextureEntry> group in catalog.Textures
                .GroupBy(entry => entry.Head.Kind)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key))
            {
                _typeCombo.Items.Add(new FilterItem(
                    $"{group.Key} ({group.Count():N0})", group.Key, null));
            }

            _typeCombo.SelectedIndex = 0;

            _formatCombo.Items.Clear();
            _formatCombo.Items.Add(new FilterItem("All formats", null, null));
            foreach (IGrouping<string, TextureEntry> group in catalog.Textures
                .GroupBy(entry => entry.Head.Format.DisplayName)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal))
            {
                _formatCombo.Items.Add(new FilterItem(
                    $"{group.Key} ({group.Count():N0})", null, group.Key));
            }

            _formatCombo.SelectedIndex = 0;
            _previewableCheck.Text = $"Previewable ({catalog.PreviewableCount:N0})";
        }
        finally
        {
            _suppressFilters = false;
        }
    }

    private void SetScanning(bool scanning)
    {
        _scanning = scanning;
        _openButton.Enabled = !scanning;
        _rescanButton.Enabled = !scanning && _profile is not null;
        _grid.Enabled = !scanning;
        _filterText.Enabled = !scanning;
        _typeCombo.Enabled = !scanning;
        _formatCombo.Enabled = !scanning;
        _previewableCheck.Enabled = !scanning;
        _clearButton.Enabled = !scanning;
        UpdateExportEnabled();
    }

    private void UpdateExportEnabled()
    {
        _exportButton.Enabled =
            !_exporting && !_scanning && _grid.SelectedRows.Count > 0;
    }

    private void UpdateZoomLabel()
    {
        _zoomLabel.Text = _preview.HasImage ? $"{_preview.Zoom * 100f:0}%" : "";
        _fitButton.ForeColor = _preview.IsFit ? Theme.Accent : Theme.Text;
        _actualButton.ForeColor = _preview.IsFit ? Theme.Text : Theme.Accent;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.O)
        {
            if (!_scanning && !_exporting)
            {
                OpenGameFolder();
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.E)
        {
            if (!_scanning && !_exporting)
            {
                _ = ExportSelectedAsync();
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            Rescan();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape && _exporting)
        {
            _exportCancel?.Cancel();
            e.Handled = true;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _scanCancel?.Cancel();
        _scanCancel?.Dispose();
        _scanCancel = null;
        CancelPreview();
        _exportCancel?.Cancel();
        _exportCancel?.Dispose();
        _exportCancel = null;
    }

    private static string N(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    // --shot drives these from ScreenshotMode so UI checks run without focus.
    internal void EnableScreenshotMode()
    {
        _screenshotMode = true;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-4000, -4000);
    }

    internal void ScaleForShot(float scale)
    {
        if (scale <= 0f || Math.Abs(scale - 1f) < 0.001f)
        {
            return;
        }

        _shotScale = scale;
        Scale(new SizeF(scale, scale));
        Font = new Font(Font.FontFamily, 9f * scale);
        _monoFont.Dispose();
        _monoFont = new Font("Consolas", 9f * scale);
        _grid.Columns[0].DefaultCellStyle.Font = _monoFont;
        Font? header = _grid.ColumnHeadersDefaultCellStyle.Font;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f * scale);
        header?.Dispose();
        _filterText.Width = (int)Math.Round(240 * scale);
        _filterText.Font = _grid.Font;
        _preview.Font = _grid.Font;
        _preview.SetDpiScale(scale);
        _infoLabel.Font = _grid.Font;
        _zoomLabel.Font = _grid.Font;
        _statusLabel.Font = _grid.Font;
        _selectionLabel.Font = _grid.Font;
    }

    internal Task LoadForScreenshotAsync(string root)
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        return LoadGameAsync(root, rebuild: false);
    }

    internal void FilterForShot(string text)
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _filterText.Text = text;
        _filterTimer.Stop();
        ApplyFilter(true);
    }

    internal void MipForShot(int mip)
    {
        if (_mipCombo.Items.Count == 0)
        {
            return;
        }

        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int index = Math.Clamp(mip, 0, _mipCombo.Items.Count - 1);
        if (_mipCombo.SelectedIndex == index)
        {
            _ = PreviewSelectionAsync(keepMip: true);
        }
        else
        {
            _mipCombo.SelectedIndex = index;
        }
    }

    internal void ChannelForShot(int index)
    {
        if (index >= 0 && index < _channelCombo.Items.Count)
        {
            _channelCombo.SelectedIndex = index;
        }
    }

    internal void ActualForShot() => _preview.ActualSize();

    internal void PreviewableForShot()
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _suppressFilters = true;
        _previewableCheck.Checked = true;
        _suppressFilters = false;
        _filterTimer.Stop();
        ApplyFilter(true);
    }

    internal void SortForShot(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
        {
            return;
        }

        if (_grid.Columns[columnIndex].Tag is not SortColumn sort)
        {
            return;
        }

        _sortColumn = sort;
        _sortAscending = false;
        UpdateSortGlyphs();
        ApplyFilter(false);
    }

    internal Task ExportForShotAsync(string folder, int count)
    {
        if (count <= 0 || _view.Length == 0)
        {
            return Task.CompletedTask;
        }

        int take = Math.Min(count, _view.Length);
        _grid.ClearSelection();
        for (int i = 0; i < take; i++)
        {
            _grid.Rows[i].Selected = true;
        }

        _grid.CurrentCell = _grid.Rows[0].Cells[0];
        _selectionTimer.Stop();
        _ = PreviewSelectionAsync(keepMip: false);
        return ExportEntriesAsync(SelectedEntries(), folder);
    }

    internal string StatusText => _statusLabel.Text ?? string.Empty;

    internal async Task WaitForPreviewAsync()
    {
        await Task.WhenAny(_previewReady.Task, Task.Delay(20000));
    }

    internal void CaptureShot(string path)
    {
        using Bitmap bitmap = new(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(path, ImageFormat.Png);
    }

    private sealed record FilterItem(string Text, BitmapKind? Kind, string? Format)
    {
        public override string ToString() => Text;
    }
}
