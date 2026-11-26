using System.Globalization;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Links;
using AptStudio.Core.Skeletons;

namespace AptStudio.Desktop;

internal sealed class SkeletonForm : Form
{
    private readonly GameProfile _profile;
    private readonly IReadOnlyList<AssetEntry> _assets;
    private readonly ulong? _initialName;
    private readonly TextBox _filter = new();
    private readonly Label _count = new();
    private readonly Label _status = new();
    private readonly Button _used = new();
    private readonly DataGridView _grid = new();
    private readonly SkeletonView _view = new();
    private readonly LinkView _links = new();
    private readonly SplitContainer _split = new();
    private readonly System.Windows.Forms.Timer _filterTimer = new() { Interval = 150 };

    private IReadOnlyList<SkeletonEntry> _entries = Array.Empty<SkeletonEntry>();
    private Dictionary<ulong, int> _copies = new();
    private SkinSkeletonIndex? _skinIndex;
    private Task? _loadTask;
    private Task? _indexTask;
    private Task? _decodeTask;
    private CancellationTokenSource? _decodeCancel;
    private bool _screenshotMode;
    private bool _loading;

    public SkeletonForm(GameProfile profile, IReadOnlyList<AssetEntry> assets, ulong? initialName = null)
    {
        _profile = profile;
        _assets = assets;
        _initialName = initialName;
        Text = "Skeletons - APTStudio";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1180, 740);
        MinimumSize = new Size(900, 560);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
        DpiChanged += (_, _) => _view.SetDpiScale(DeviceDpi / 96f);

        _filter.BorderStyle = BorderStyle.FixedSingle;
        _filter.Width = 250;
        _filter.BackColor = Theme.Surface;
        _filter.ForeColor = Theme.Text;
        _filter.PlaceholderText = "hash or archive";
        _filter.Margin = new Padding(0, 6, 8, 0);
        _filter.TextChanged += (_, _) =>
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            ApplyFilter();
        };

        _count.AutoSize = true;
        _count.ForeColor = Theme.Muted;
        _count.Margin = new Padding(0, 10, 8, 0);
        _count.Text = "0 skeletons";

        _used.Text = "Used by 0";
        _used.AutoSize = true;
        _used.FlatStyle = FlatStyle.Flat;
        _used.BackColor = Theme.Surface;
        _used.ForeColor = Theme.Text;
        _used.Padding = new Padding(8, 0, 8, 0);
        _used.Margin = new Padding(0, 5, 0, 0);
        _used.MinimumSize = new Size(0, 24);
        _used.UseVisualStyleBackColor = false;
        _used.FlatAppearance.BorderSize = 0;
        _used.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _used.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _used.Enabled = false;
        _used.Click += (_, _) => ShowSkins();

        FlowLayoutPanel left = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            BackColor = Theme.Surface,
        };
        left.Controls.AddRange([_filter, _count, _used]);

        _status.AutoSize = true;
        _status.ForeColor = Theme.Muted;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Margin = new Padding(8, 10, 0, 0);
        _status.Text = "not scanned";

        TableLayoutPanel toolbar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(8, 0, 8, 0),
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.Controls.Add(left, 0, 0);
        toolbar.Controls.Add(_status, 1, 0);

        ConfigureGrid();
        _grid.SelectionChanged += (_, _) => _ = DecodeSelectedAsync();
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                _ = DecodeSelectedAsync();
            }
        };

        _view.Dock = DockStyle.Fill;
        _links.Dock = DockStyle.Fill;
        _links.Visible = false;
        _links.BackRequested += () =>
        {
            _links.Visible = false;
            _view.Visible = true;
        };
        _links.LinkActivated += row =>
        {
            SkinRequested?.Invoke(row.Hash);
            Close();
        };

        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterWidth = 4;
        _split.BackColor = Theme.Border;
        _split.Panel1.BackColor = Theme.Background;
        _split.Panel2.BackColor = Theme.Background;
        _split.Panel1.Controls.Add(_grid);
        _split.Panel2.Controls.Add(_links);
        _split.Panel2.Controls.Add(_view);

        Controls.Add(_split);
        Controls.Add(toolbar);
        Shown += (_, _) =>
        {
            if (_split.Width > 700)
            {
                _split.SplitterDistance = _split.Width * 2 / 5;
            }

            _ = StartAsync();
        };
    }

    public event Action<ulong>? SkinRequested;

    public string StatusText => _status.Text;

    protected override bool ShowWithoutActivation => _screenshotMode || base.ShowWithoutActivation;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _decodeCancel?.Dispose();
            _filterTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    internal void EnableScreenshotMode()
    {
        _screenshotMode = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-4000, -4000);
    }

    internal async Task LoadForShotAsync(ulong? nameHash)
    {
        await StartAsync();
        if (nameHash is ulong hash)
        {
            SelectName(hash);
        }

        if (_decodeTask is not null)
        {
            await _decodeTask;
        }

        if (_indexTask is not null)
        {
            await _indexTask;
            UpdateUsedButton();
        }
    }

    internal void CaptureShot(string path)
    {
        using Bitmap bitmap = new(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    internal void ShowSkinsForShot()
    {
        ShowSkins();
    }

    internal void ScaleForShot(float scale)
    {
        if (scale > 0f)
        {
            Scale(new SizeF(scale, scale));
            SetDpiScale(scale);
            _view.SetDpiScale(scale);
        }
    }

    internal void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

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

    private async Task StartAsync()
    {
        if (_loadTask is not null)
        {
            await _loadTask;
            return;
        }

        _loadTask = LoadCatalogAsync();
        await _loadTask;
    }

    private async Task LoadCatalogAsync()
    {
        _loading = true;
        _status.Text = "scanning skeletons...";
        try
        {
            SkeletonCatalog catalog = await Task.Run(() => SkeletonCatalog.Load(_profile));
            _entries = catalog.Entries
                .OrderBy(entry => entry.NameHash)
                .ThenBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _copies = _entries
                .GroupBy(entry => entry.NameHash)
                .ToDictionary(group => group.Key, group => group.Count());
            _status.Text =
                $"{_entries.Count:N0} objects | {catalog.UniqueNameCount:N0} names | {catalog.Archives.Count:N0} archives | scanned {catalog.Scanned:N0}, cached {catalog.Cached:N0}";
            _loading = false;
            ApplyFilter();
            if (_initialName is ulong initial)
            {
                SelectName(initial);
            }

            _indexTask = LoadIndexAsync();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadIndexAsync()
    {
        try
        {
            _skinIndex = await Task.Run(() => SkinSkeletonIndex.Load(_assets));
            UpdateUsedButton();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
        }
    }

    private void ApplyFilter()
    {
        if (_loading)
        {
            return;
        }

        string query = _filter.Text.Trim();
        IEnumerable<SkeletonEntry> rows = _entries;
        if (query.Length > 0)
        {
            rows = rows.Where(entry =>
                entry.NameHash.ToString("X16").Contains(query, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(entry.ArchivePath)
                    .Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        List<SkeletonEntry> view = rows.ToList();
        _grid.Rows.Clear();
        foreach (SkeletonEntry entry in view)
        {
            int index = _grid.Rows.Add(
                entry.NameHash.ToString("X16", CultureInfo.InvariantCulture),
                Path.GetFileName(entry.ArchivePath),
                entry.Offset.ToString("N0", CultureInfo.InvariantCulture),
                entry.Hit.BodySize.ToString("N0", CultureInfo.InvariantCulture),
                entry.Hit.StoredSize.ToString("N0", CultureInfo.InvariantCulture),
                _copies.GetValueOrDefault(entry.NameHash).ToString(
                    CultureInfo.InvariantCulture));
            _grid.Rows[index].Tag = entry;
        }

        _count.Text = $"{view.Count:N0} of {_entries.Count:N0}";
        if (_grid.Rows.Count > 0)
        {
            _grid.ClearSelection();
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
            _grid.Rows[0].Selected = true;
        }
        else
        {
            _view.Reset();
            UpdateUsedButton();
        }
    }

    private void SelectName(ulong nameHash)
    {
        for (int index = 0; index < _grid.Rows.Count; index++)
        {
            if (_grid.Rows[index].Tag is SkeletonEntry entry && entry.NameHash == nameHash)
            {
                _grid.ClearSelection();
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
                _grid.Rows[index].Selected = true;
                _grid.FirstDisplayedScrollingRowIndex = index;
                return;
            }
        }
    }

    private SkeletonEntry? Selected()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            return null;
        }

        int index = _grid.SelectedRows[0].Index;
        return index >= 0 && index < _grid.Rows.Count
            ? _grid.Rows[index].Tag as SkeletonEntry
            : null;
    }

    private async Task DecodeSelectedAsync()
    {
        SkeletonEntry? entry = Selected();
        if (entry is null || _loading)
        {
            UpdateUsedButton();
            return;
        }

        _decodeCancel?.Cancel();
        _decodeCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _decodeCancel = cancel;
        _decodeTask = DecodeAsync(entry, cancel);
        await _decodeTask;
    }

    private async Task DecodeAsync(SkeletonEntry entry, CancellationTokenSource cancel)
    {
        _view.SetMessage(
            $"Skel_Z 0x{entry.NameHash:X16}",
            "Loading...");
        try
        {
            SkeletonData data = await Task.Run(() =>
            {
                using FileStream stream = new(
                    entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return SkeletonDecoder.Read(stream, entry.ArchivePath, entry.Hit);
            }, cancel.Token);

            if (IsDisposed || cancel.IsCancellationRequested)
            {
                return;
            }

            int copies = _copies.GetValueOrDefault(entry.NameHash);
            _view.SetSkeleton(
                $"Skel_Z 0x{entry.NameHash:X16}",
                $"{Path.GetFileName(entry.ArchivePath)} | offset {entry.Offset:N0} | v{data.Head.Version} | {data.Bones.Count:N0} bones | tail {data.TailLength:N0} bytes | {copies:N0} copies",
                data);
            _status.Text =
                $"{data.Bones.Count:N0} bones | {data.Head.BodySize:N0} bytes | {Path.GetFileName(entry.ArchivePath)}";
            UpdateUsedButton();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && !cancel.IsCancellationRequested)
            {
                _status.Text = ex.Message;
            }
        }
    }

    private void UpdateUsedButton()
    {
        SkeletonEntry? entry = Selected();
        if (entry is null || _skinIndex is null)
        {
            _used.Text = "Used by 0";
            _used.Enabled = false;
            return;
        }

        int count = _skinIndex.BySkeleton.TryGetValue(entry.NameHash, out var list)
            ? list.Count
            : 0;
        _used.Text = $"Used by {count:N0}";
        _used.Enabled = count > 0;
    }

    private void ShowSkins()
    {
        SkeletonEntry? entry = Selected();
        if (entry is null || _skinIndex is null
            || !_skinIndex.BySkeleton.TryGetValue(entry.NameHash, out var uses))
        {
            return;
        }

        LinkRow[] rows = uses
            .Select(use => new LinkRow(
                LinkKind.Skin,
                use.NameHash,
                use.ArchiveName,
                "bound"))
            .ToArray();
        _links.SetLinks(
            $"Skins bound to 0x{entry.NameHash:X16}",
            $"{rows.Length:N0} skins | double-click to open in the browser",
            rows);
        _view.Visible = false;
        _links.Visible = true;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
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

        AddColumn("Name", 146, true);
        AddColumn("Archive", 94, false);
        AddColumn("Offset", 60, false, true);
        AddColumn("Body", 54, false, true);
        AddColumn("Stored", 54, false, true);
        AddColumn("Copies", 58, false, true);
    }

    private void AddColumn(string header, int width, bool mono, bool right = false)
    {
        DataGridViewTextBoxColumn column = new()
        {
            HeaderText = header,
            Width = width,
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

        _grid.Columns.Add(column);
    }
}
