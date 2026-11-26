using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using AptStudio.Core.Assets;
using AptStudio.Core.Audio;
using AptStudio.Core.Games;
using AptStudio.Core.Materials;
using AptStudio.Core.Skins;
using AptStudio.Core.Meshes;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop;

internal sealed class MainForm : Form
{
    private enum SortColumn
    {
        Type,
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
    private readonly MeshView _meshView = new();
    private readonly MaterialView _materialPanel = new();
    private readonly SkinView _skinView = new();
    private readonly AudioView _audioPanel = new();
    private readonly DarkComboBox _typeCombo = new();
    private readonly DarkComboBox _formatCombo = new();
    private readonly DarkComboBox _mipCombo = new();
    private readonly DarkComboBox _channelCombo = new();
    private readonly CheckBox _wireframeCheck = new();
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

    private IReadOnlyList<AssetEntry> _entries = Array.Empty<AssetEntry>();
    private IReadOnlyList<MaterialRow> _materials = Array.Empty<MaterialRow>();
    private IReadOnlyList<AudioEntry> _audio = Array.Empty<AudioEntry>();
    private Dictionary<ulong, AssetEntry> _bitmapByHash = new();
    private Dictionary<ulong, AssetEntry> _meshByHash = new();
    private int[] _view = Array.Empty<int>();
    private GameProfile? _profile;
    private CancellationTokenSource? _scanCancel;
    private CancellationTokenSource? _previewCancel;
    private CancellationTokenSource? _exportCancel;
    private CancellationTokenSource? _materialCancel;
    private CancellationTokenSource? _audioCancel;
    private bool _suppressFilters;
    private bool _suppressMip;
    private bool _updatingView;
    private bool _scanning;
    private bool _exporting;
    private bool _materialsLoaded;
    private bool _materialsLoading;
    private bool _viewIsMaterials;
    private bool _viewIsAudio;
    private bool _audioLoaded;
    private bool _audioLoading;
    private bool _audioBusy;
    private bool _columnsAreAudio;
    private string _assetStatus = "No game folder selected.";
    private string _materialStatus = "";
    private string _audioStatus = "";
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
        _fitButton.Click += (_, _) =>
        {
            if (_meshView.Visible)
            {
                _meshView.Fit();
            }
            else
            {
                _preview.Fit();
            }
        };
        _actualButton.Click += (_, _) => _preview.ActualSize();
        _wireframeCheck.CheckedChanged += (_, _) => _meshView.SetWireframe(_wireframeCheck.Checked);
        _materialPanel.SlotSelected += hash => _ = ShowSlotTextureAsync(hash);
        _materialPanel.TextureRequested += OpenTexture;
        _skinView.MeshRequested += OpenMesh;
        _audioPanel.PlayRequested += () => _ = PlaySelectedAudioAsync();
        _audioPanel.ExportRequested += () => _ = ExportAudioAsync();
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
        _filterText.PlaceholderText = "hash, archive, type";
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
        _typeCombo.Width = 200;
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
        _wireframeCheck.Text = "Wireframe";
        _wireframeCheck.AutoSize = true;
        _wireframeCheck.ForeColor = Theme.Text;
        _wireframeCheck.Margin = new Padding(0, 6, 6, 0);
        _wireframeCheck.Visible = false;
        tools.Controls.AddRange(
            [_fitButton, _actualButton, _zoomLabel, _mipCombo, _channelCombo, _wireframeCheck]);
        bar.Controls.Add(tools);

        _infoLabel.Dock = DockStyle.Bottom;
        _infoLabel.Height = 22;
        _infoLabel.TextAlign = ContentAlignment.MiddleLeft;
        _infoLabel.ForeColor = Theme.Muted;
        _infoLabel.Padding = new Padding(8, 0, 0, 0);
        _infoLabel.Text = "";

        _preview.Dock = DockStyle.Fill;
        _meshView.Dock = DockStyle.Fill;
        _meshView.Visible = false;
        _materialPanel.Dock = DockStyle.Fill;
        _materialPanel.Visible = false;
        _skinView.Dock = DockStyle.Fill;
        _skinView.Visible = false;
        _audioPanel.Dock = DockStyle.Fill;
        _audioPanel.Visible = false;

        Panel panel = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };
        panel.Controls.Add(_preview);
        panel.Controls.Add(_meshView);
        panel.Controls.Add(_materialPanel);
        panel.Controls.Add(_skinView);
        panel.Controls.Add(_audioPanel);
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

        AddColumn("Type", 110, SortColumn.Type);
        AddColumn("Hash", 140, SortColumn.Hash, mono: true);
        AddColumn("Format", 130, SortColumn.Format);
        AddColumn("Size", 90, SortColumn.Size, right: true);
        AddColumn("Mips", 50, SortColumn.Mips, right: true);
        AddColumn("Surf", 48, SortColumn.Surfaces, right: true);
        AddColumn("Storage", 64, SortColumn.Storage);
        AddColumn("Archive", 155, SortColumn.Archive);

        _grid.CellValueNeeded += OnCellValueNeeded;
        _grid.SelectionChanged += OnGridSelectionChanged;
        _grid.ColumnHeaderMouseClick += OnColumnHeaderClick;
        _grid.CellMouseDown += OnCellMouseDown;
        _grid.CellDoubleClick += OnCellDoubleClick;
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
            _ = LoadGameAsync(last);
        }
        else
        {
            _statusLabel.Text = "Open the game folder to begin.";
        }
    }

    private void ScaleGrid()
    {
        float scale = DeviceDpi / 96f * _shotScale;
        int[] widths = _columnsAreAudio
            ? [120, 86, 164, 60, 42, 68, 70, 84]
            : [110, 140, 130, 90, 50, 48, 64, 155];
        for (int i = 0; i < _grid.Columns.Count && i < widths.Length; i++)
        {
            _grid.Columns[i].Width = (int)Math.Round(widths[i] * scale);
        }

        _grid.RowTemplate.Height = (int)Math.Round(22 * scale);
        _grid.ColumnHeadersHeight = (int)Math.Round(26 * scale);
    }

    private void SetColumnMode(bool audio)
    {
        if (_columnsAreAudio == audio)
        {
            return;
        }

        _columnsAreAudio = audio;
        string[] headers = audio
            ? ["Pack", "Id", "Name", "Length", "Ch", "Rate", "Lang", "Bank"]
            : ["Type", "Hash", "Format", "Size", "Mips", "Surf", "Storage", "Archive"];
        for (int i = 0; i < _grid.Columns.Count && i < headers.Length; i++)
        {
            _grid.Columns[i].HeaderText = headers[i];
            _grid.Columns[i].HeaderCell.Tag = headers[i];
        }

        ScaleGrid();
        UpdateSortGlyphs();
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
        _materialPanel.SetDpiScale(DeviceDpi / 96f);
        _skinView.SetDpiScale(DeviceDpi / 96f);
        _audioPanel.SetDpiScale(DeviceDpi / 96f);
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

    private void OnCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (_viewIsAudio && e.RowIndex >= 0)
        {
            _ = PlaySelectedAudioAsync();
        }
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

        if (_viewIsAudio)
        {
            AudioEntry row = _audio[_view[e.RowIndex]];
            e.Value = e.ColumnIndex switch
            {
                0 => row.PackName,
                1 => row.Id.ToString("X8", CultureInfo.InvariantCulture),
                2 => row.Name ?? "",
                3 => AudioTime(row.DurationSeconds),
                4 => row.Info.Channels.ToString(CultureInfo.InvariantCulture),
                5 => (row.Info.SampleRate / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " kHz",
                6 => ShortLanguage(row.Language),
                7 => row.BankId == 0 ? "-" : $"0x{row.BankId:X8}",
                _ => null,
            };
            return;
        }

        if (_viewIsMaterials)
        {
            MaterialRow row = _materials[_view[e.RowIndex]];
            e.Value = e.ColumnIndex switch
            {
                0 => row.ClassName,
                1 => row.NameHash.ToString("X16"),
                2 => row.Code,
                3 => row.SlotCount.ToString(CultureInfo.InvariantCulture),
                4 => "-",
                5 => "-",
                6 => row.Storage == MaterialStorage.Raw ? "raw" : "lz4",
                7 => Path.GetFileName(row.Entry.ArchivePath),
                _ => null,
            };
            return;
        }

        AssetEntry entry = _entries[_view[e.RowIndex]];
        BitmapHead? head = entry.Bitmap;
        e.Value = e.ColumnIndex switch
        {
            0 => entry.ClassName,
            1 => entry.NameHash.ToString("X16"),
            2 => head?.Format.DisplayName ?? "-",
            3 => head is null
                ? "-"
                : head.Kind == BitmapKind.Volume ? $"w={head.Width}" : $"{head.Width}x{head.Height}",
            4 => head?.MipCount.ToString(CultureInfo.InvariantCulture) ?? "-",
            5 => head?.Surfaces.ToString(CultureInfo.InvariantCulture) ?? "-",
            6 => head is null ? "-" : head.Storage == BitmapStorage.Raw ? "raw" : "lz4",
            7 => Path.GetFileName(entry.ArchivePath),
            _ => null,
        };
    }

    private void ApplyFilter(bool selectFirst)
    {
        FilterItem? type = _typeCombo.SelectedItem as FilterItem;
        bool materials = type?.Materials == true;
        bool audio = type?.Audio == true;
        bool modeChanged = materials != _viewIsMaterials || audio != _viewIsAudio;
        if (modeChanged)
        {
            SetColumnMode(audio);
            _formatCombo.Enabled = !materials && !audio;
            _previewableCheck.Enabled = !materials && !audio;
            _statusLabel.Text = audio
                ? _audioLoaded
                    ? _audioStatus
                    : _audioLoading ? "Scanning SOUNDBANKS..." : "SOUNDBANKS not scanned."
                : materials
                    ? _materialsLoaded
                        ? _materialStatus
                        : _materialsLoading ? "Scanning materials..." : "Material_Z not scanned."
                    : _assetStatus;
        }

        if (!audio)
        {
            _audioPanel.StopPlayback();
        }

        int keep = -1;
        if (!modeChanged && _grid.SelectedRows.Count > 0)
        {
            int selected = _grid.SelectedRows[0].Index;
            if (selected >= 0 && selected < _view.Length)
            {
                keep = _view[selected];
            }
        }

        string query = NormalizeQuery(_filterText.Text);
        _viewIsMaterials = materials;
        _viewIsAudio = audio;
        if (audio)
        {
            if (!_audioLoaded)
            {
                _ = LoadAudioAsync();
                SetView(Array.Empty<int>(), -1, false);
                ShowMessageMode();
                _preview.ShowMessage(
                    _audioLoading
                        ? "Scanning SOUNDBANKS..."
                        : "Select Audio (SOUNDBANKS) to scan.",
                    false);
                return;
            }

            IEnumerable<int> audioIndices = Enumerable.Range(0, _audio.Count);
            if (query.Length > 0)
            {
                audioIndices = audioIndices.Where(index => MatchesAudio(_audio[index], query));
            }

            int[] audioView = audioIndices.ToArray();
            Array.Sort(audioView, CompareAudio);
            SetView(audioView, keep, selectFirst);
            return;
        }

        if (materials)
        {
            if (!_materialsLoaded)
            {
                _ = LoadMaterialsAsync();
                SetView(Array.Empty<int>(), -1, false);
                ShowMessageMode();
                _preview.ShowMessage(
                    _materialsLoading ? "Scanning materials..." : "Select Material_Z to scan.",
                    false);
                return;
            }

            IEnumerable<int> materialIndices = Enumerable.Range(0, _materials.Count);
            if (query.Length > 0)
            {
                materialIndices = materialIndices.Where(
                    index => MatchesMaterial(_materials[index], query));
            }

            int[] materialView = materialIndices.ToArray();
            Array.Sort(materialView, CompareMaterials);
            SetView(materialView, keep, selectFirst);
            return;
        }

        IEnumerable<int> indices = Enumerable.Range(0, _entries.Count);
        FilterItem? format = _formatCombo.SelectedItem as FilterItem;
        bool previewable = _previewableCheck.Checked;

        if (type?.Class is not null)
        {
            indices = indices.Where(index => _entries[index].ClassHash == type.Class.Value);
        }

        if (format?.Format is not null)
        {
            indices = indices.Where(index =>
                string.Equals(
                    _entries[index].Bitmap?.Format.DisplayName, format.Format,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (previewable)
        {
            indices = indices.Where(index => _entries[index].IsPreviewable);
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
        AssetEntry left = _entries[a];
        AssetEntry right = _entries[b];
        int result = _sortColumn switch
        {
            SortColumn.Type => StringComparer.OrdinalIgnoreCase.Compare(
                left.ClassName, right.ClassName),
            SortColumn.Hash => left.NameHash.CompareTo(right.NameHash),
            SortColumn.Format => StringComparer.OrdinalIgnoreCase.Compare(
                left.Bitmap?.Format.DisplayName, right.Bitmap?.Format.DisplayName),
            SortColumn.Size => PixelCount(left).CompareTo(PixelCount(right)),
            SortColumn.Mips => Nullable.Compare(left.Bitmap?.MipCount, right.Bitmap?.MipCount),
            SortColumn.Surfaces => Nullable.Compare(left.Bitmap?.Surfaces, right.Bitmap?.Surfaces),
            SortColumn.Storage => Nullable.Compare(left.Bitmap?.Storage, right.Bitmap?.Storage),
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

        result = left.NameHash.CompareTo(right.NameHash);
        if (result != 0)
        {
            return result;
        }

        result = string.CompareOrdinal(left.ArchivePath, right.ArchivePath);
        return result != 0 ? result : a.CompareTo(b);
    }

    private int CompareAudio(int a, int b)
    {
        AudioEntry left = _audio[a];
        AudioEntry right = _audio[b];
        int result = _sortColumn switch
        {
            SortColumn.Type => StringComparer.OrdinalIgnoreCase.Compare(
                left.PackName, right.PackName),
            SortColumn.Hash => left.Id.CompareTo(right.Id),
            SortColumn.Format => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name),
            SortColumn.Size => left.DurationSeconds.CompareTo(right.DurationSeconds),
            SortColumn.Mips => left.Info.Channels.CompareTo(right.Info.Channels),
            SortColumn.Surfaces => left.Info.SampleRate.CompareTo(right.Info.SampleRate),
            SortColumn.Storage => StringComparer.OrdinalIgnoreCase.Compare(
                left.Language, right.Language),
            SortColumn.Archive => left.BankId.CompareTo(right.BankId),
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

        result = left.Id.CompareTo(right.Id);
        if (result != 0)
        {
            return result;
        }

        result = string.CompareOrdinal(left.PackName, right.PackName);
        return result != 0 ? result : a.CompareTo(b);
    }

    private int CompareMaterials(int a, int b)
    {
        MaterialRow left = _materials[a];
        MaterialRow right = _materials[b];
        int result = _sortColumn switch
        {
            SortColumn.Type => StringComparer.OrdinalIgnoreCase.Compare(
                left.ClassName, right.ClassName),
            SortColumn.Hash => left.NameHash.CompareTo(right.NameHash),
            SortColumn.Format => StringComparer.OrdinalIgnoreCase.Compare(
                left.Code, right.Code),
            SortColumn.Size => left.SlotCount.CompareTo(right.SlotCount),
            SortColumn.Mips => 0,
            SortColumn.Surfaces => 0,
            SortColumn.Storage => left.Storage.CompareTo(right.Storage),
            SortColumn.Archive => StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileName(left.Entry.ArchivePath),
                Path.GetFileName(right.Entry.ArchivePath)),
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

        result = left.NameHash.CompareTo(right.NameHash);
        if (result != 0)
        {
            return result;
        }

        result = string.CompareOrdinal(left.Entry.ArchivePath, right.Entry.ArchivePath);
        return result != 0 ? result : a.CompareTo(b);
    }

    private static byte[] ConvertAudio(AudioEntry media)
    {
        byte[] wem = new byte[media.Size];
        using (FileStream stream = new(
            media.PackPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            stream.Seek(media.Offset, SeekOrigin.Begin);
            stream.ReadExactly(wem);
        }

        return WemConverter.ToOgg(wem);
    }

    private static string ShortLanguage(string language)
    {
        int parenthesis = language.IndexOf('(');
        return parenthesis > 0 ? language[..parenthesis] : language;
    }

    private static string AudioTime(double seconds)
    {
        if (seconds <= 0)
        {
            return "0:00";
        }

        TimeSpan value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private static string AudioExportName(AudioEntry media)
    {
        string name = media.Name
            ?? $"{Path.GetFileNameWithoutExtension(media.PackName)}_{media.Id:X8}";
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name + ".ogg";
    }

    private static long PixelCount(AssetEntry entry) =>
        entry.Bitmap is null ? 0 : (long)entry.Bitmap.Width * entry.Bitmap.Height;

    private static string NormalizeQuery(string text)
    {
        string query = text.Trim();
        return query.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? query[2..] : query;
    }

    private static bool Matches(AssetEntry entry, string query) =>
        entry.NameHash.ToString("X16").Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(entry.ArchivePath).Contains(query, StringComparison.OrdinalIgnoreCase)
        || (entry.Bitmap is not null
            && entry.Bitmap.Format.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesAudio(AudioEntry row, string query) =>
        row.Id.ToString("X8", CultureInfo.InvariantCulture)
            .Contains(query, StringComparison.OrdinalIgnoreCase)
        || (row.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || row.PackName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || row.Language.Contains(query, StringComparison.OrdinalIgnoreCase)
        || row.Info.Codec.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesMaterial(MaterialRow row, string query) =>
        row.NameHash.ToString("X16").Contains(query, StringComparison.OrdinalIgnoreCase)
        || row.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || row.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(row.Entry.ArchivePath).Contains(query, StringComparison.OrdinalIgnoreCase);

    private void SetView(int[] view, int keep, bool selectFirst)
    {
        _updatingView = true;
        try
        {
            _view = view;
            _grid.RowCount = 0;
            _grid.RowCount = view.Length;
            _countLabel.Text = $"{view.Length:N0} of {ActiveCount:N0}";
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
            ShowMessageMode();
            _preview.ShowMessage(
                ActiveCount == 0
                    ? _viewIsAudio
                        ? "No audio to show."
                        : _viewIsMaterials ? "No materials to show." : "No assets in this game folder."
                    : "Select an asset.",
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

    private int ActiveCount =>
        _viewIsAudio ? _audio.Count : _viewIsMaterials ? _materials.Count : _entries.Count;

    private MaterialRow? SelectedMaterial()
    {
        DataGridViewRow? row = _grid.CurrentRow;
        if (row is null || !row.Selected)
        {
            row = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0] : null;
        }

        return row is not null && row.Index >= 0 && row.Index < _view.Length
            && _materials.Count > _view[row.Index]
            ? _materials[_view[row.Index]]
            : null;
    }

    private AudioEntry? SelectedAudio()
    {
        DataGridViewRow? row = _grid.CurrentRow;
        if (row is null || !row.Selected)
        {
            row = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0] : null;
        }

        return row is not null && row.Index >= 0 && row.Index < _view.Length
            && _audio.Count > _view[row.Index]
            ? _audio[_view[row.Index]]
            : null;
    }

    private AssetEntry? SelectedEntry()
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

    private List<AssetEntry> SelectedEntries() =>
        _viewIsMaterials || _viewIsAudio
            ? new List<AssetEntry>()
            : _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .OrderBy(row => row.Index)
                .Where(row => row.Index >= 0 && row.Index < _view.Length)
                .Select(row => _entries[_view[row.Index]])
                .ToList();

    private void ShowNoSelection()
    {
        CancelPreview();
        ShowMessageMode();
        _selectionTimer.Stop();
        _selectionLabel.Text = "0 selected";
        UpdateExportEnabled();
        _preview.ShowMessage("Select an asset.", false);
        _infoLabel.Text = "";
    }

    private async Task PreviewSelectionAsync(bool keepMip)
    {
        if (_viewIsAudio)
        {
            AudioEntry? media = SelectedAudio();
            if (media is null)
            {
                ShowNoSelection();
                return;
            }

            ShowAudioMode();
            _audioPanel.SetAudio(media);
            _infoLabel.Text =
                $"Audio | {media.IdText} | {media.PackName} | {media.Language} | {media.DurationSeconds:0.00} s";
            _previewReady.TrySetResult(true);
            return;
        }

        if (_viewIsMaterials)
        {
            MaterialRow? material = SelectedMaterial();
            if (material is null)
            {
                ShowNoSelection();
                return;
            }

            await PreviewMaterialAsync(material);
            return;
        }

        AssetEntry? entry = SelectedEntry();
        if (entry is null)
        {
            ShowNoSelection();
            return;
        }

        if (entry.ClassHash == AssetClass.Mesh)
        {
            ShowMeshMode();
            await DecodeMeshAsync(entry);
            return;
        }

        if (entry.ClassHash == AssetClass.Skin)
        {
            ShowSkinMode();
            await DecodeSkinAsync(entry);
            return;
        }

        ShowTextureMode();
        if (entry.Bitmap is null)
        {
            CancelPreview();
            _previewReady.TrySetResult(true);
            ShowMessageMode();
            _preview.ShowMessage(
                entry.BodyError ?? $"{entry.ClassName} preview is not available yet.",
                entry.BodyError is not null);
            _infoLabel.Text =
                $"{entry.ClassName} | 0x{entry.NameHash:X16} | {Path.GetFileName(entry.ArchivePath)}";
            _suppressMip = true;
            _mipCombo.Items.Clear();
            _suppressMip = false;
            return;
        }

        if (!keepMip)
        {
            FillMipCombo(entry.Bitmap);
        }

        int mip = _mipCombo.SelectedIndex >= 0 ? _mipCombo.SelectedIndex : 0;
        await DecodePreviewAsync(entry, mip);
    }

    private void ShowTextureMode()
    {
        _meshView.Visible = false;
        _materialPanel.Visible = false;
        _skinView.Visible = false;
        _audioPanel.Visible = false;
        _preview.Visible = true;
        _mipCombo.Visible = true;
        _channelCombo.Visible = true;
        _zoomLabel.Visible = true;
        _actualButton.Visible = true;
        _fitButton.Visible = true;
        _fitButton.Text = "Fit";
        _wireframeCheck.Visible = false;
    }

    private void ShowMeshMode()
    {
        _preview.Visible = false;
        _materialPanel.Visible = false;
        _skinView.Visible = false;
        _audioPanel.Visible = false;
        _meshView.Visible = true;
        _mipCombo.Visible = false;
        _channelCombo.Visible = false;
        _zoomLabel.Visible = false;
        _actualButton.Visible = false;
        _fitButton.Visible = true;
        _fitButton.Text = "Reset";
        _wireframeCheck.Visible = true;
        _zoomLabel.Text = "";
    }

    private void ShowMaterialMode()
    {
        _preview.Visible = false;
        _meshView.Visible = false;
        _skinView.Visible = false;
        _audioPanel.Visible = false;
        _materialPanel.Visible = true;
        _mipCombo.Visible = false;
        _channelCombo.Visible = false;
        _zoomLabel.Visible = false;
        _actualButton.Visible = false;
        _fitButton.Visible = false;
        _wireframeCheck.Visible = false;
        _zoomLabel.Text = "";
    }

    private void ShowSkinMode()
    {
        _preview.Visible = false;
        _meshView.Visible = false;
        _materialPanel.Visible = false;
        _audioPanel.Visible = false;
        _skinView.Visible = true;
        _mipCombo.Visible = false;
        _channelCombo.Visible = false;
        _zoomLabel.Visible = false;
        _actualButton.Visible = false;
        _fitButton.Visible = false;
        _wireframeCheck.Visible = false;
        _zoomLabel.Text = "";
    }

    private void ShowAudioMode()
    {
        _preview.Visible = false;
        _meshView.Visible = false;
        _materialPanel.Visible = false;
        _skinView.Visible = false;
        _audioPanel.Visible = true;
        _mipCombo.Visible = false;
        _channelCombo.Visible = false;
        _zoomLabel.Visible = false;
        _actualButton.Visible = false;
        _fitButton.Visible = false;
        _wireframeCheck.Visible = false;
        _zoomLabel.Text = "";
    }

    private void ShowMessageMode()
    {
        _meshView.Visible = false;
        _materialPanel.Visible = false;
        _skinView.Visible = false;
        _preview.Visible = true;
        _mipCombo.Visible = false;
        _channelCombo.Visible = false;
        _zoomLabel.Visible = false;
        _actualButton.Visible = false;
        _fitButton.Visible = false;
        _wireframeCheck.Visible = false;
    }

    private async Task LoadAudioAsync()
    {
        if (_audioLoaded || _audioLoading || _profile is null)
        {
            return;
        }

        _audioLoading = true;
        _audioCancel?.Cancel();
        _audioCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _audioCancel = cancel;
        GameProfile profile = _profile;
        Stopwatch watch = Stopwatch.StartNew();
        _statusLabel.Text = "Scanning SOUNDBANKS...";
        IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
        {
            if (!IsDisposed && cancel == _audioCancel)
            {
                _statusLabel.Text =
                    $"Scanning SOUNDBANKS {item.ArchivePath} {item.Index}/{item.Total}";
            }
        });

        try
        {
            AudioCatalog catalog = await Task.Run(
                () => AudioCatalog.Load(profile, progress, cancel.Token), cancel.Token);
            if (IsDisposed || cancel != _audioCancel)
            {
                return;
            }

            watch.Stop();
            _audio = catalog.Entries;
            _audioLoaded = true;
            int named = catalog.Entries.Count(entry => entry.Name is not null);
            _audioStatus =
                $"{N(catalog.Entries.Count)} audio media | {N(named)} named | {N(catalog.Archives.Count)} packs | {N(catalog.Previews)} previews skipped | {catalog.Failures.Count} failed | {watch.ElapsedMilliseconds} ms";
            _statusLabel.Text = _audioStatus;
            ApplyFilter(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _audioCancel)
            {
                _statusLabel.Text = ex.Message;
                _preview.ShowMessage(ex.Message, true);
            }
        }
        finally
        {
            if (cancel == _audioCancel)
            {
                _audioLoading = false;
            }
        }
    }

    private async Task PlaySelectedAudioAsync()
    {
        AudioEntry? media = SelectedAudio();
        if (media is null || _audioBusy)
        {
            return;
        }

        _audioBusy = true;
        _audioPanel.SetBusy("Converting...");
        try
        {
            byte[] ogg = await Task.Run(() => ConvertAudio(media));

            if (IsDisposed || !ReferenceEquals(SelectedAudio(), media))
            {
                _audioPanel.StopPlayback();
                return;
            }

            _audioPanel.StartPlayback(ogg);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed)
            {
                _audioPanel.ShowError(ex.Message);
            }
        }
        finally
        {
            _audioBusy = false;
        }
    }

    private async Task ExportAudioAsync()
    {
        AudioEntry? media = SelectedAudio();
        if (media is null || _audioBusy)
        {
            return;
        }

        using SaveFileDialog dialog = new()
        {
            Filter = "Ogg Vorbis (*.ogg)|*.ogg",
            FileName = AudioExportName(media),
            Title = "Export Audio",
            AddExtension = true,
            DefaultExt = "ogg",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string full = Path.GetFullPath(dialog.FileName);
        if (_profile is not null
            && GameFolderGuard.IsInside(_profile.Root, Path.GetDirectoryName(full)!))
        {
            _statusLabel.Text = "Refusing to write inside the game folder.";
            return;
        }

        _audioBusy = true;
        _audioPanel.SetBusy("Exporting...");
        try
        {
            byte[] ogg = await Task.Run(() => ConvertAudio(media));

            string? directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = full + ".tmp";
            File.WriteAllBytes(temp, ogg);
            File.Move(temp, full, overwrite: true);
            _audioPanel.SetStatus($"Exported {Path.GetFileName(full)}");
            _statusLabel.Text = $"Exported {Path.GetFileName(full)} ({N(ogg.Length)} bytes).";
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed)
            {
                _audioPanel.ShowError(ex.Message);
            }
        }
        finally
        {
            _audioBusy = false;
        }
    }

    private async Task DecodeMeshAsync(AssetEntry entry)
    {
        CancelPreview();
        CancellationTokenSource cancel = new();
        _previewCancel = cancel;
        _meshView.ShowMessage("Loading mesh...", false);
        _infoLabel.Text =
            $"{entry.ClassName} | 0x{entry.NameHash:X16} | {Path.GetFileName(entry.ArchivePath)}";

        try
        {
            MeshGeometry? geometry = await Task.Run(() =>
            {
                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                MeshData mesh = MeshDecoder.Read(stream, entry.BodyArchivePath, entry.Row);
                return mesh.Streams.Count == 0 ? null : mesh.ToGeometry();
            }, cancel.Token);

            if (IsDisposed || cancel != _previewCancel)
            {
                return;
            }

            if (geometry is null)
            {
                _meshView.ShowMessage("Mesh has no geometry.", false);
                _previewReady.TrySetResult(true);
                return;
            }

            _meshView.SetMesh(geometry);
            _infoLabel.Text =
                $"{geometry.Positions.Length:N0} verts | {geometry.Indices.Length / 3:N0} tris | {entry.ClassName} | {Path.GetFileName(entry.ArchivePath)}";
            _previewReady.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _previewCancel)
            {
                _meshView.ShowMessage(ex.Message, true);
                _previewReady.TrySetResult(true);
            }
        }
    }

    private async Task DecodeSkinAsync(AssetEntry entry)
    {
        CancelPreview();
        CancellationTokenSource cancel = new();
        _previewCancel = cancel;
        _skinView.SetSkin($"Skin_Z 0x{entry.NameHash:X16}", "Loading...", Array.Empty<SkinPartRow>());
        _infoLabel.Text =
            $"{entry.ClassName} | 0x{entry.NameHash:X16} | {Path.GetFileName(entry.ArchivePath)}";

        try
        {
            (SkinData skin, SkinPartRow[] rows) = await Task.Run(() =>
            {
                Dictionary<ulong, int?> counts = new();
                int? Verts(ulong mesh)
                {
                    if (counts.TryGetValue(mesh, out int? known))
                    {
                        return known;
                    }

                    int? result = null;
                    if (_meshByHash.TryGetValue(mesh, out AssetEntry? row))
                    {
                        try
                        {
                            using FileStream stream = new(
                                row.BodyArchivePath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.ReadWrite);
                            MeshData data = MeshDecoder.Read(stream, row.BodyArchivePath, row.Row);
                            result = data.Streams.Count > 0 ? data.Streams[0].VertexCount : 0;
                        }
                        catch (InvalidDataException)
                        {
                            result = null;
                        }
                    }

                    counts[mesh] = result;
                    return result;
                }

                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                SkinData data = SkinDecoder.Read(
                    stream, entry.BodyArchivePath, entry.Row, Verts);
                SkinPartRow[] parts = data.Parts
                    .Select(part => new SkinPartRow(
                        part.Index,
                        part.MeshHash,
                        part.VertexCount,
                        part.Stride,
                        part.PacketCount,
                        part.PacketsParsed,
                        part.MaterialHash))
                    .ToArray();
                return (data, parts);
            }, cancel.Token);

            if (IsDisposed || cancel != _previewCancel)
            {
                return;
            }

            string subtitle =
                $"v{skin.Head.Version} | {N(skin.MeshLinks.Length)} meshes | {N(skin.Parts.Length)} parts | {N(skin.Bones.Length)} bones | {Path.GetFileName(entry.ArchivePath)}";
            _skinView.SetSkin($"Skin_Z 0x{entry.NameHash:X16}", subtitle, rows);
            _infoLabel.Text =
                $"Skin_Z | 0x{entry.NameHash:X16} | {N(skin.SkinnedParts)} skinned parts | {N(rows.Sum(row => row.Vertices))} verts | {Path.GetFileName(entry.ArchivePath)}";
            _previewReady.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _previewCancel)
            {
                _skinView.SetSkin(
                    $"Skin_Z 0x{entry.NameHash:X16}", ex.Message, Array.Empty<SkinPartRow>());
                _previewReady.TrySetResult(true);
            }
        }
    }

    private void FillMipCombo(BitmapHead head)
    {
        _suppressMip = true;
        try
        {
            _mipCombo.Items.Clear();
            for (int mip = 0; mip < head.MipCount; mip++)
            {
                int width = Math.Max(1, head.Width >> mip);
                int height = Math.Max(1, head.Height >> mip);
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

    private async Task DecodePreviewAsync(AssetEntry entry, int mip)
    {
        TextureEntry? texture = entry.Texture;
        if (texture is null)
        {
            return;
        }

        CancelPreview();
        CancellationTokenSource cancel = new();
        _previewCancel = cancel;
        _infoLabel.Text =
            $"{entry.ClassName} | {entry.Bitmap!.Format.DisplayName} | {Path.GetFileName(entry.ArchivePath)}";

        try
        {
            (Bitmap Bitmap, int Width, int Height) result = await Task.Run(() =>
            {
                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                DecodedTexture image = TextureDecoder.DecodeMip(stream, texture, mip);
                return (PreviewBitmap.Create(image), image.Width, image.Height);
            }, cancel.Token);

            if (IsDisposed || cancel != _previewCancel)
            {
                result.Bitmap.Dispose();
                return;
            }

            _preview.SetImage(result.Bitmap);
            _infoLabel.Text =
                $"Mip {mip} | {result.Width:N0}x{result.Height:N0} | {entry.Bitmap!.MipCount} mips | {entry.Bitmap.Format.DisplayName} | {Path.GetFileName(entry.ArchivePath)}";
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
                _infoLabel.Text =
                    $"{entry.ClassName} | {entry.Bitmap!.Format.DisplayName} | {Path.GetFileName(entry.ArchivePath)}";
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

    private async Task LoadMaterialsAsync()
    {
        if (_materialsLoaded || _materialsLoading || _profile is null)
        {
            return;
        }

        _materialsLoading = true;
        _materialCancel?.Cancel();
        _materialCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _materialCancel = cancel;
        GameProfile profile = _profile;
        Stopwatch watch = Stopwatch.StartNew();
        _statusLabel.Text = "Scanning materials...";
        _statusLabel.ToolTipText = "";
        IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
        {
            if (!IsDisposed && cancel == _materialCancel)
            {
                _statusLabel.Text =
                    $"Scanning materials {Path.GetFileName(item.ArchivePath)} {item.Index}/{item.Total}";
            }
        });

        try
        {
            (MaterialCatalog Catalog, List<MaterialRow> Rows) result = await Task.Run(() =>
            {
                MaterialCatalog catalog = MaterialCatalog.Load(
                    profile, false, progress, cancel.Token);
                List<MaterialRow> rows = new(catalog.Entries.Count);
                foreach (IGrouping<string, MaterialEntry> group in catalog.Entries
                    .GroupBy(entry => entry.ArchivePath))
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    using FileStream stream = new(
                        group.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    foreach (MaterialEntry entry in group)
                    {
                        cancel.Token.ThrowIfCancellationRequested();
                        try
                        {
                            MaterialData data = MaterialDecoder.Read(
                                stream, entry.ArchivePath, entry.Hit);
                            rows.Add(new MaterialRow(
                                entry, data.Code, data.Slots.Count, data.Head.Storage));
                        }
                        catch (InvalidDataException)
                        {
                            rows.Add(new MaterialRow(
                                entry, "(failed)", 0, MaterialStorage.Lz4, true));
                        }
                    }
                }

                return (catalog, rows);
            }, cancel.Token);

            if (IsDisposed || cancel != _materialCancel)
            {
                return;
            }

            watch.Stop();
            _materials = result.Rows;
            _materialsLoaded = true;
            UpdateMaterialFilterLabel();
            int failed = result.Rows.Count(row => row.Failed);
            int slots = result.Rows.Sum(row => row.SlotCount);
            _materialStatus =
                $"{N(result.Rows.Count)} materials | {N(slots)} slots | {N(result.Catalog.Archives.Count)} archives | {N(failed)} failed | scanned {N(result.Catalog.Scanned)}, cached {N(result.Catalog.Cached)} | {watch.ElapsedMilliseconds} ms";
            _statusLabel.Text = _materialStatus;
            if (result.Catalog.Failures.Count > 0)
            {
                _statusLabel.ToolTipText = string.Join(
                    Environment.NewLine,
                    result.Catalog.Failures.Select(failure => failure.Message).Take(20));
            }

            ApplyFilter(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _materialCancel)
            {
                _statusLabel.Text = ex.Message;
            }
        }
        finally
        {
            _materialsLoading = false;
        }
    }

    private void UpdateMaterialFilterLabel()
    {
        for (int index = 0; index < _typeCombo.Items.Count; index++)
        {
            if (_typeCombo.Items[index] is not FilterItem item || !item.Materials)
            {
                continue;
            }

            bool selected = _typeCombo.SelectedIndex == index;
            _suppressFilters = true;
            try
            {
                _typeCombo.Items[index] = new FilterItem(
                    $"Material_Z ({_materials.Count:N0})", AssetClass.Material, null, true);
                if (selected)
                {
                    _typeCombo.SelectedIndex = index;
                }
            }
            finally
            {
                _suppressFilters = false;
            }

            return;
        }
    }

    private async Task PreviewMaterialAsync(MaterialRow row)
    {
        CancelPreview();
        CancellationTokenSource cancel = new();
        _previewCancel = cancel;
        ShowMaterialMode();
        _materialPanel.ShowPreviewMessage("Loading material...", false);
        _infoLabel.Text =
            $"{row.ClassName} | 0x{row.NameHash:X16} | {Path.GetFileName(row.Entry.ArchivePath)}";

        try
        {
            (MaterialData Data, List<MaterialSlotRow> Slots) result = await Task.Run(() =>
            {
                using FileStream stream = new(
                    row.Entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                MaterialData data = MaterialDecoder.Read(
                    stream, row.Entry.ArchivePath, row.Entry.Hit);
                List<MaterialSlotRow> slots = new(data.Slots.Count);
                foreach (MaterialSlot slot in data.Slots)
                {
                    string texture = "missing";
                    string archive = "-";
                    if (_bitmapByHash.TryGetValue(slot.TextureHash, out AssetEntry? bitmap))
                    {
                        texture = $"0x{slot.TextureHash:X16}";
                        archive = Path.GetFileName(bitmap.ArchivePath);
                    }

                    slots.Add(new MaterialSlotRow(
                        slot.Index,
                        slot.Role.Authoring,
                        slot.Role.Binding,
                        slot.TextureHash,
                        texture,
                        archive));
                }

                return (data, slots);
            }, cancel.Token);

            if (IsDisposed || cancel != _previewCancel)
            {
                return;
            }

            _materialPanel.SetMaterial(
                $"{result.Data.Code} | {row.ClassName}",
                $"0x{row.NameHash:X16} | {Path.GetFileName(row.Entry.ArchivePath)} | {result.Data.Slots.Count} slots",
                result.Slots);
            _infoLabel.Text =
                $"{result.Data.Code} | {row.ClassName} | {Path.GetFileName(row.Entry.ArchivePath)}";
            if (result.Data.Slots.Count > 0)
            {
                await ShowSlotTextureAsync(result.Data.Slots[0].TextureHash);
            }
            else
            {
                _materialPanel.ShowPreviewMessage("Material has no texture slots.", false);
                _previewReady.TrySetResult(true);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _previewCancel)
            {
                _materialPanel.ShowPreviewMessage(ex.Message, true);
                _previewReady.TrySetResult(true);
            }
        }
    }

    private async Task ShowSlotTextureAsync(ulong hash)
    {
        if (!_bitmapByHash.TryGetValue(hash, out AssetEntry? entry) || entry.Texture is null)
        {
            _materialPanel.ShowPreviewMessage(
                $"Texture 0x{hash:X16} is not in the asset catalog.", true);
            return;
        }

        CancelPreview();
        CancellationTokenSource cancel = new();
        _previewCancel = cancel;
        _materialPanel.ShowPreviewMessage("Loading texture...", false);

        try
        {
            (Bitmap Bitmap, int Width, int Height, string Format, int Mips) result =
                await Task.Run(() =>
                {
                    using FileStream stream = new(
                        entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    DecodedTexture image = TextureDecoder.DecodeMip(stream, entry.Texture, 0);
                    return (
                        PreviewBitmap.Create(image),
                        image.Width,
                        image.Height,
                        entry.Bitmap!.Format.DisplayName,
                        entry.Bitmap.MipCount);
                }, cancel.Token);

            if (IsDisposed || cancel != _previewCancel)
            {
                result.Bitmap.Dispose();
                return;
            }

            _materialPanel.SetPreview(
                result.Bitmap,
                $"{result.Width:N0}x{result.Height:N0} | {result.Format} | {result.Mips} mips | {Path.GetFileName(entry.ArchivePath)}");
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
                _materialPanel.ShowPreviewMessage(ex.Message, true);
                _previewReady.TrySetResult(true);
            }
        }
    }

    private void OpenTexture(ulong hash)
    {
        if (!_bitmapByHash.ContainsKey(hash))
        {
            _statusLabel.Text = $"Texture 0x{hash:X16} is not in the asset catalog.";
            return;
        }

        FocusRow(hash);
    }

    private void OpenMesh(ulong hash)
    {
        if (!_meshByHash.ContainsKey(hash))
        {
            _statusLabel.Text = $"Mesh 0x{hash:X16} is not in the asset catalog.";
            return;
        }

        FocusRow(hash);
    }

    private void FocusRow(ulong hash)
    {
        _suppressFilters = true;
        try
        {
            if (_typeCombo.Items.Count > 0)
            {
                _typeCombo.SelectedIndex = 0;
            }

            _filterText.Text = hash.ToString("X16");
        }
        finally
        {
            _suppressFilters = false;
        }

        _filterTimer.Stop();
        ApplyFilter(true);
    }

    private async Task ExportSelectedAsync()
    {
        if (_exporting)
        {
            return;
        }

        List<AssetEntry> selected = SelectedEntries();
        List<AssetEntry> entries = selected.Where(entry => entry.Texture is not null).ToList();
        if (entries.Count == 0)
        {
            _statusLabel.Text = "Select at least one texture to export.";
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

        await ExportEntriesAsync(entries, dialog.SelectedPath, selected.Count - entries.Count);
    }

    private async Task ExportEntriesAsync(List<AssetEntry> entries, string folder, int skipped = 0)
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
                foreach (AssetEntry entry in entries)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    string name = ExportNames.Unique(
                        folder, entry.ArchivePath, entry.NameHash, used);
                    string path = Path.Combine(folder, name);
                    try
                    {
                        TextureExport.ExportPng(entry.Texture!, path);
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
                ? $"Exported {completed:N0} textures{(skipped > 0 ? $" ({skipped:N0} non-texture rows skipped)" : "")} -> {folder}"
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
        ulong? hash = _viewIsAudio
            ? SelectedAudio()?.Id
            : _viewIsMaterials
                ? SelectedMaterial()?.NameHash
                : SelectedEntry()?.NameHash;
        if (hash is not ulong value)
        {
            return;
        }

        try
        {
            string text = _viewIsAudio ? $"0x{value:X8}" : $"0x{value:X16}";
            Clipboard.SetText(text);
            _statusLabel.Text = $"Copied {text}.";
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
            _ = LoadGameAsync(dialog.SelectedPath);
        }
    }

    private void Rescan()
    {
        if (_profile is not null && !_scanning && !_exporting)
        {
            _ = LoadGameAsync(_profile.Root);
        }
    }

    private async Task LoadGameAsync(string root)
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
        _materialCancel?.Cancel();
        _materialCancel?.Dispose();
        _materialCancel = null;
        _materials = Array.Empty<MaterialRow>();
        _materialsLoaded = false;
        _materialsLoading = false;
        _viewIsMaterials = false;
        _materialPanel.Reset();
        SetScanning(true);
        _statusLabel.Text = "Scanning...";
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
            AssetCatalog catalog = await Task.Run(
                () => AssetCatalog.Load(profile, progress, cancel.Token),
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

            _entries = catalog.Entries;
            _bitmapByHash = catalog.Entries
                .Where(entry => entry.IsBitmap && entry.Texture is not null)
                .GroupBy(entry => entry.NameHash)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(entry => entry.IsPreviewable).First());
            _meshByHash = catalog.Entries
                .Where(entry => entry.ClassHash == AssetClass.Mesh)
                .GroupBy(entry => entry.NameHash)
                .ToDictionary(group => group.Key, group => group.First());
            RebuildOptions(catalog);
            SetScanning(false);
            ApplyFilter(true);
            _assetStatus =
                $"{N(_entries.Count)} assets | {N(catalog.BitmapCount)} textures | {N(catalog.PreviewableCount)} previewable | {N(catalog.Archives.Count)} archives | {N(catalog.BodyErrorCount)} body errors | {catalog.Failures.Count} failed | {watch.ElapsedMilliseconds} ms";
            _statusLabel.Text = _assetStatus;
            List<string> problems = catalog.Failures.Select(failure => failure.Message)
                .Concat(catalog.Entries
                    .Where(entry => entry.BodyError is not null)
                    .Select(entry => entry.BodyError!))
                .Take(20)
                .ToList();
            if (problems.Count > 0)
            {
                _statusLabel.ToolTipText = string.Join(Environment.NewLine, problems);
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

    private void RebuildOptions(AssetCatalog catalog)
    {
        _suppressFilters = true;
        try
        {
            _typeCombo.Items.Clear();
            _typeCombo.Items.Add(new FilterItem("All classes", null, null));
            foreach (AssetClassCount group in catalog.Classes)
            {
                _typeCombo.Items.Add(new FilterItem(
                    $"{group.Name} ({group.Count:N0})", group.Hash, null));
            }

            _typeCombo.Items.Add(new FilterItem(
                "Material_Z (not scanned)", AssetClass.Material, null, true));
            _typeCombo.Items.Add(new FilterItem("Audio (SOUNDBANKS)", null, null, Audio: true));

            _typeCombo.SelectedIndex = 0;

            _formatCombo.Items.Clear();
            _formatCombo.Items.Add(new FilterItem("All formats", null, null));
            foreach (IGrouping<string, AssetEntry> group in catalog.Entries
                .Where(entry => entry.Bitmap is not null)
                .GroupBy(entry => entry.Bitmap!.Format.DisplayName)
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
            !_exporting && !_scanning && !_viewIsMaterials && !_viewIsAudio
            && _grid.SelectedRows.Count > 0;
    }

    private void UpdateZoomLabel()
    {
        if (_meshView.Visible)
        {
            _zoomLabel.Text = "";
            _fitButton.ForeColor = Theme.Text;
            return;
        }

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
        _materialCancel?.Cancel();
        _materialCancel?.Dispose();
        _materialCancel = null;
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
        if (scale <= 0f)
        {
            return;
        }

        _shotScale = scale;
        if (Math.Abs(scale - 1f) > 0.001f)
        {
            Scale(new SizeF(scale, scale));
        }

        Font = new Font(Font.FontFamily, 9f * scale);
        _monoFont.Dispose();
        _monoFont = new Font("Consolas", 9f * scale);
        DataGridViewColumn? hashColumn = _grid.Columns
            .Cast<DataGridViewColumn>()
            .FirstOrDefault(column => column.Tag is SortColumn.Hash);
        if (hashColumn is not null)
        {
            hashColumn.DefaultCellStyle.Font = _monoFont;
        }

        Font? header = _grid.ColumnHeadersDefaultCellStyle.Font;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f * scale);
        header?.Dispose();
        ScaleGrid();
        _filterText.Width = (int)Math.Round(240 * scale);
        _filterText.Font = _grid.Font;
        _preview.Font = _grid.Font;
        _preview.SetDpiScale(scale);
        _meshView.Font = _grid.Font;
        _materialPanel.Font = _grid.Font;
        _materialPanel.SetDpiScale(scale);
        _infoLabel.Font = _grid.Font;
        _zoomLabel.Font = _grid.Font;
        _statusLabel.Font = _grid.Font;
        _selectionLabel.Font = _grid.Font;
    }

    internal Task LoadForScreenshotAsync(string root)
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        return LoadGameAsync(root);
    }

    internal async Task MaterialsForShotAsync()
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        for (int index = 0; index < _typeCombo.Items.Count; index++)
        {
            if (_typeCombo.Items[index] is FilterItem item && item.Materials)
            {
                _suppressFilters = true;
                _typeCombo.SelectedIndex = index;
                _suppressFilters = false;
                ApplyFilter(true);
                _previewReady = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                await WaitForPreviewAsync();
                return;
            }
        }

        _previewReady.TrySetResult(true);
    }

    internal void SlotForShot(int index)
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _materialPanel.SelectSlot(index);
    }

    internal void OpenTextureForShot()
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (_materialPanel.SelectedHash is ulong hash)
        {
            OpenTexture(hash);
        }
        else
        {
            _previewReady.TrySetResult(true);
        }
    }

    internal void OpenMeshForShot()
    {
        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (_skinView.SelectedMeshHash is ulong hash)
        {
            OpenMesh(hash);
        }
        else
        {
            _previewReady.TrySetResult(true);
        }
    }

    internal async Task AudioForShotAsync()
    {
        FilterItem? item = _typeCombo.Items
            .Cast<FilterItem>()
            .FirstOrDefault(candidate => candidate.Audio);
        if (item is null)
        {
            return;
        }

        _typeCombo.SelectedItem = item;
        for (int i = 0; i < 400 && !_audioLoaded && !IsDisposed; i++)
        {
            await Task.Delay(50);
        }

        if (IsDisposed)
        {
            return;
        }

        _previewReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ApplyFilter(true);
    }

    internal async Task PlayAudioForShotAsync()
    {
        await PlaySelectedAudioAsync();
        await Task.Delay(600);
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

    internal void WireframeForShot()
    {
        _wireframeCheck.Checked = true;
        _meshView.SetWireframe(true);
    }

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
        List<AssetEntry> entries = SelectedEntries()
            .Where(entry => entry.Texture is not null)
            .ToList();
        return ExportEntriesAsync(entries, folder);
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

    private sealed record MaterialRow(
        MaterialEntry Entry,
        string Code,
        int SlotCount,
        MaterialStorage Storage,
        bool Failed = false)
    {
        public ulong NameHash => Entry.NameHash;

        public string ClassName =>
            Entry.ClassHash == MaterialHead.AnimClass ? "MaterialAnim_Z" : "Material_Z";
    }

    private sealed record FilterItem(
        string Text,
        ulong? Class,
        string? Format,
        bool Materials = false,
        bool Audio = false)
    {
        public override string ToString() => Text;
    }
}
