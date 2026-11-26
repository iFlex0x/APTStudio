using System.Globalization;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Links;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop;

internal sealed class SkeletonForm : Form
{
    private readonly GameProfile _profile;
    private readonly IReadOnlyList<AssetEntry> _assets;
    private readonly ulong? _initialName;
    private readonly TextBox _filter = new();
    private readonly Label _count = new();
    private readonly Label _status = new();
    private readonly Button _back = new();
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
    private CancellationTokenSource? _loadCancel;
    private ulong? _pendingName;
    private bool _screenshotMode;
    private bool _loading;
    private bool _loaded;

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
        DpiChanged += (_, _) => SetDpiScale(DeviceDpi / 96f);
        FormClosing += (_, _) =>
        {
            _loadCancel?.Cancel();
            _decodeCancel?.Cancel();
        };

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

        _back.Text = "Back";
        _back.AutoSize = true;
        _back.FlatStyle = FlatStyle.Flat;
        _back.BackColor = Theme.Surface;
        _back.ForeColor = Theme.Text;
        _back.Padding = new Padding(8, 0, 8, 0);
        _back.Margin = new Padding(0, 5, 8, 0);
        _back.MinimumSize = new Size(0, 24);
        _back.UseVisualStyleBackColor = false;
        _back.FlatAppearance.BorderSize = 0;
        _back.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _back.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _back.Visible = false;
        _back.Click += (_, _) => CloseLinks();

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
        left.Controls.AddRange([_back, _filter, _count, _used]);

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
        _links.BackRequested += CloseLinks;
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

    protected override bool ShowWithoutActivation => _screenshotMode || base.ShowWithoutActivation;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _decodeCancel?.Dispose();
            _loadCancel?.Dispose();
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
        if (nameHash is ulong hash && !SelectName(hash))
        {
            ShowMissing(hash);
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

    internal void SelectSkeleton(ulong? nameHash)
    {
        if (nameHash is not ulong hash)
        {
            return;
        }

        CloseLinks();
        _filterTimer.Stop();
        _filter.Text = "";
        _filterTimer.Stop();
        if (!_loaded)
        {
            _pendingName = hash;
            return;
        }

        ApplyFilter();
        if (!SelectName(hash))
        {
            ShowMissing(hash);
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

    internal void FilterForShot(string text)
    {
        _filterTimer.Stop();
        _filter.Text = text;
        _filterTimer.Stop();
        ApplyFilter();
    }

    internal void ScaleForShot(float scale)
    {
        if (scale > 0f)
        {
            Scale(new SizeF(scale, scale));
            SetDpiScale(scale);
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
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8f * scale);
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            if (column.Tag is int width)
            {
                column.Width = (int)Math.Round(width * scale);
            }
        }

        _view.SetDpiScale(scale);
        _links.SetDpiScale(scale);
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
        _loaded = false;
        _status.Text = "scanning skeletons...";
        _loadCancel = new CancellationTokenSource();
        try
        {
            Progress<CatalogProgress> progress = new(value =>
            {
                if (!IsDisposed)
                {
                    _status.Text =
                        $"scanning {value.Index:N0}/{value.Total:N0} {Path.GetFileName(value.ArchivePath)}";
                }
            });
            SkeletonCatalog catalog = await Task.Run(
                () => SkeletonCatalog.Load(_profile, false, progress, _loadCancel.Token),
                _loadCancel.Token);
            if (IsDisposed)
            {
                return;
            }

            _entries = catalog.Entries
                .OrderBy(entry => entry.NameHash)
                .ThenBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _copies = _entries
                .GroupBy(entry => entry.NameHash)
                .ToDictionary(group => group.Key, group => group.Count());
            _loaded = true;
            _status.Text =
                $"{_entries.Count:N0} objects | {catalog.UniqueNameCount:N0} names | {catalog.Archives.Count:N0} archives | scanned {catalog.Scanned:N0}, cached {catalog.Cached:N0}";
            _loading = false;
            ApplyFilter();
            ulong? initial = _pendingName ?? _initialName;
            _pendingName = null;
            if (initial is ulong hash && !SelectName(hash))
            {
                ShowMissing(hash);
            }

            _indexTask = LoadIndexAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed)
            {
                _status.Text = ex.Message;
            }
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
            Progress<CatalogProgress> progress = new(value =>
            {
                if (!IsDisposed)
                {
                    _status.Text = $"indexing skins {value.Index:N0}/{value.Total:N0}";
                }
            });
            CancellationToken token = _loadCancel?.Token ?? CancellationToken.None;
            _skinIndex = await Task.Run(
                () => SkinSkeletonIndex.Load(_assets, progress, token), token);
            if (IsDisposed)
            {
                return;
            }

            UpdateUsedButton();
            if (_status.Text.StartsWith("indexing", StringComparison.Ordinal))
            {
                _status.Text =
                    $"{_skinIndex.BoundCount:N0} bound | {_skinIndex.NoSkeleton:N0} none | {_skinIndex.Failures.Count:N0} failed";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed)
            {
                _status.Text = ex.Message;
            }
        }
    }

    private void ApplyFilter()
    {
        if (_loading)
        {
            return;
        }

        string query = _filter.Text.Trim();
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            query = query[2..];
        }

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

    private bool SelectName(ulong nameHash)
    {
        for (int index = 0; index < _grid.Rows.Count; index++)
        {
            if (_grid.Rows[index].Tag is SkeletonEntry entry && entry.NameHash == nameHash)
            {
                _grid.ClearSelection();
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
                _grid.Rows[index].Selected = true;
                _grid.FirstDisplayedScrollingRowIndex = index;
                return true;
            }
        }

        return false;
    }

    private void ShowMissing(ulong nameHash)
    {
        CloseLinks();
        _decodeCancel?.Cancel();
        _decodeCancel?.Dispose();
        _decodeCancel = null;
        _decodeTask = null;
        _grid.ClearSelection();
        _grid.CurrentCell = null;
        _view.SetMessage("Skeleton not found", $"0x{nameHash:X16} is not in the catalog.");
        _status.Text = $"skeleton 0x{nameHash:X16} not found";
        _used.Text = "Used by 0";
        _used.Enabled = false;
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
        if (_links.Visible)
        {
            CloseLinks();
        }

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
        string title = $"Skel_Z 0x{entry.NameHash:X16}";
        _view.SetMessage(title, "Loading...");
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
                title,
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
                _view.SetMessage(title, "Could not read this skeleton.");
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
        _back.Visible = true;
    }

    private void CloseLinks()
    {
        _links.Visible = false;
        _view.Visible = true;
        _back.Visible = false;
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
            Font = new Font("Segoe UI Semibold", 8f),
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

        AddColumn("Name", 144, true);
        AddColumn("Archive", 66, false);
        AddColumn("Offset", 78, false, true);
        AddColumn("Body", 56, false, true);
        AddColumn("Stored", 54, false, true);
        AddColumn("Copies", 54, false, true);
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
