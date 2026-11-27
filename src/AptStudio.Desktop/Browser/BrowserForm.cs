using System.Diagnostics;
using System.Globalization;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Links;
using AptStudio.Core.Materials;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal sealed class BrowserForm : Form
{
    private readonly ScopePanel _scope = new();
    private readonly SplitContainer _outer = new();
    private readonly SplitContainer _inner = new();
    private readonly Panel _results = new();
    private readonly Panel _inspector = new();
    private readonly Panel _textureHost = new();
    private readonly AssetListHeader _textureHeader = new();
    private readonly AssetList _textureList = new();
    private readonly TextureInspector _textureInspector = new();
    private readonly MaterialChooserView _chooser = new();
    private readonly MaterialSlotsView _materialSlots = new();
    private readonly MaterialSlotInspector _slotInspector = new();
    private readonly AnimationInspector _animationInspector = new();
    private ThumbnailCache _thumbnailCache = null!;
    private IReadOnlyList<TextureOccurrence> _inspectorCopies = Array.Empty<TextureOccurrence>();
    private readonly Label _resultsTitle = new();
    private readonly Label _resultsCount = new();
    private readonly Label _resultsBody = new();
    private readonly Label _inspectorTitle = new();
    private readonly Label _inspectorBody = new();
    private readonly Label _crumbs = new();
    private readonly Button _back = new();
    private readonly Button _forward = new();
    private readonly Button _scopeButton = new();
    private readonly Button _filterButton = new();
    private readonly TableLayoutPanel _textureToolbar = new();
    private readonly ComboBox _sortCombo = new();
    private readonly Button _sortDirection = new();
    private readonly Button _exportSelected = new();
    private readonly Label _selectionCount = new();
    private bool _suppressSort;
    private int _chooserArchiveCount;
    private readonly TextBox _search = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 150 };
    private readonly GameProfile? _initialProfile;
    private readonly bool _autoLoad;
    private readonly bool _saveSettings;
    private static readonly AssetColumn[] TextureColumns =
    [
        new("Hash", 0, Flexible: true, Mono: true),
        new("Size", 92, Right: true, Muted: true),
        new("Mips", 48, Right: true, Muted: true),
        new("Format", 56, Muted: true),
        new("Storage", 64, Muted: true),
    ];
    private static readonly AssetColumn[] NarrowTextureColumns =
    [
        new("Hash", 0, Flexible: true, Mono: true),
        new("Size", 88, Right: true, Muted: true),
        new("Mips", 44, Right: true, Muted: true),
    ];
    private static readonly AssetColumn[] AnimationColumns =
    [
        new("Hash", 0, Flexible: true, Mono: true),
        new("Occurrences", 90, Right: true, Muted: true),
    ];
    private CancellationTokenSource? _loadCancel;
    private BrowserNavigation? _navigation;
    private IReadOnlyList<AssetEntry> _assets = Array.Empty<AssetEntry>();
    private IReadOnlyList<ArchiveChoice> _archives = Array.Empty<ArchiveChoice>();
    private IReadOnlyDictionary<string, int> _animationCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<ulong, int> _animationOccurrences =
        new Dictionary<ulong, int>();
    private GameProfile? _profile;
    private int _sidebarWidth = 216;
    private int _inspectorWidth = 420;
    private bool _scopeOpen = true;
    private bool _lastNarrow;
    private bool _loading;
    private MaterialCatalog? _materials;
    private MaterialTextureIndex? _materialTextures;
    private CancellationTokenSource? _materialCancel;
    private bool _materialLoading;
    private readonly Dictionary<(string Archive, long Offset, ulong Hash), MaterialData> _materialBodies =
        new();
    private IReadOnlyList<TextureOccurrence> _slotCopies = Array.Empty<TextureOccurrence>();
    private ulong _slotCopyHash;
    private string _slotCopyArchive = "";
    private int _slotCopyIndex;

    public BrowserForm(
        GameProfile? initialProfile = null,
        bool autoLoad = true,
        bool saveSettings = true)
    {
        _initialProfile = initialProfile;
        _autoLoad = autoLoad;
        _saveSettings = saveSettings;

        Text = "APTStudio";
        Icon = Theme.AppIcon;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1440, 900);
        MinimumSize = new Size(960, 600);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        KeyPreview = true;

        (int sidebar, int inspector) = Settings.LoadBrowserLayout();
        _sidebarWidth = BrowserLayout.ClampSidebar(sidebar, ClientSize.Width);
        _inspectorWidth = BrowserLayout.ClampInspector(inspector, ClientSize.Width);
        _lastNarrow = BrowserLayout.ShouldCollapseSidebar(ClientSize.Width);
        _scopeOpen = !_lastNarrow;

        BuildLayout();
        WireScope();
        WireTextureList();
        WireInspector();
        _back.Click += (_, _) => Back();
        _forward.Click += (_, _) => Forward();
        _scopeButton.Click += (_, _) => ToggleScope();
        _search.TextChanged += (_, _) => OnSearchChanged();
        _filterButton.Click += (_, _) => OnFilterRequested();
        _sortCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressSort || _navigation is null)
            {
                return;
            }

            TextureSort sort = _sortCombo.SelectedIndex switch
            {
                1 => TextureSort.Hash,
                2 => TextureSort.Size,
                3 => TextureSort.Mips,
                _ => TextureSort.Archive,
            };
            _navigation.SetSort(sort, _navigation.State.SortDescending);
            RenderState();
        };
        _sortDirection.Click += (_, _) =>
        {
            if (_navigation is null)
            {
                return;
            }

            _navigation.SetSort(_navigation.State.Sort, !_navigation.State.SortDescending);
            RenderState();
        };
        _exportSelected.Click += async (_, _) => await ExportSelectedAsync();
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ApplySearch();
        };
        Resize += (_, _) => OnResize();
        Shown += async (_, _) => await OnShownAsync();
        FormClosing += (_, _) =>
        {
            _loadCancel?.Cancel();
            _materialCancel?.Cancel();
            SaveLayout();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnailCache?.Dispose();
            _loadCancel?.Dispose();
            _materialCancel?.Dispose();
            _searchTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        root.Controls.Add(BuildTopBar(), 0, 0);
        root.Controls.Add(BuildBody(), 0, 1);
        root.Controls.Add(BuildStatus(), 0, 2);
        Controls.Add(root);
    }

    private Control BuildTopBar()
    {
        TableLayoutPanel bar = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 0, 12, 0),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Label brand = new()
        {
            Text = "APTStudio",
            AutoSize = true,
            Font = BrowserVisual.Brand,
            ForeColor = Theme.Text,
            Margin = new Padding(0, 0, 12, 0),
            Anchor = AnchorStyles.Left,
        };

        FlowLayoutPanel nav = new()
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 8, 0),
            Anchor = AnchorStyles.Left,
        };
        StyleButton(_back, "←");
        StyleButton(_forward, "→");
        _back.AutoSize = false;
        _forward.AutoSize = false;
        _back.Width = 30;
        _forward.Width = 30;
        _back.Height = 26;
        _forward.Height = 26;
        _back.Font = new Font("Segoe UI", 11f);
        _forward.Font = new Font("Segoe UI", 11f);
        _back.Padding = new Padding(0);
        _forward.Padding = new Padding(0);
        _back.TextAlign = ContentAlignment.MiddleCenter;
        _forward.TextAlign = ContentAlignment.MiddleCenter;
        _back.AccessibleName = "Back";
        _forward.AccessibleName = "Forward";
        _back.Margin = new Padding(0, 0, 4, 0);
        _back.Enabled = false;
        _forward.Enabled = false;
        nav.Controls.Add(_back);
        nav.Controls.Add(_forward);

        _scopeButton.Text = "No game";
        _scopeButton.AutoSize = false;
        _scopeButton.Width = 96;
        _scopeButton.Height = 24;
        _scopeButton.Font = BrowserVisual.RowSmall;
        _scopeButton.ForeColor = Theme.Muted;
        _scopeButton.BackColor = Theme.Background;
        _scopeButton.TextAlign = ContentAlignment.MiddleCenter;
        _scopeButton.Padding = new Padding(6, 0, 6, 0);
        _scopeButton.AutoEllipsis = true;
        _scopeButton.FlatAppearance.BorderColor = BrowserVisual.BorderStrong;
        _scopeButton.FlatAppearance.BorderSize = 1;
        _scopeButton.FlatAppearance.MouseOverBackColor = BrowserVisual.SurfaceHover;
        _scopeButton.Margin = new Padding(0, 0, 10, 0);
        _scopeButton.AccessibleName = "Toggle scope pane";

        _crumbs.Dock = DockStyle.Fill;
        _crumbs.Font = BrowserVisual.RowSmall;
        _crumbs.TextAlign = ContentAlignment.MiddleLeft;
        _crumbs.ForeColor = Theme.Muted;
        _crumbs.AutoEllipsis = true;
        _crumbs.Margin = new Padding(0, 0, 12, 0);

        _search.Width = 206;
        _search.Height = 26;
        _search.Font = BrowserVisual.Ui;
        _search.PlaceholderText = "hash, dimensions";
        _search.BackColor = Theme.Background;
        _search.ForeColor = Theme.Text;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.Anchor = AnchorStyles.Left;
        _search.Margin = new Padding(0, 0, 6, 0);
        _search.Enabled = false;

        StyleButton(_filterButton, "Filter");
        _filterButton.AutoSize = false;
        _filterButton.Width = 54;
        _filterButton.Height = 26;
        _filterButton.Enabled = false;
        _filterButton.Anchor = AnchorStyles.Left;

        bar.Controls.Add(brand, 0, 0);
        bar.Controls.Add(nav, 1, 0);
        bar.Controls.Add(_scopeButton, 2, 0);
        bar.Controls.Add(_crumbs, 3, 0);
        bar.Controls.Add(_search, 4, 0);
        bar.Controls.Add(_filterButton, 5, 0);
        return bar;
    }

    private Control BuildBody()
    {
        _outer.Dock = DockStyle.Fill;
        _outer.Orientation = Orientation.Vertical;
        _outer.SplitterWidth = 2;
        _outer.BackColor = Theme.Border;
        _outer.Panel1.BackColor = Theme.Surface;
        _outer.Panel2.BackColor = Theme.Background;

        _inner.Dock = DockStyle.Fill;
        _inner.Orientation = Orientation.Vertical;
        _inner.SplitterWidth = 2;
        _inner.BackColor = Theme.Border;
        _inner.Panel1.BackColor = Theme.Background;
        _inner.Panel2.BackColor = Theme.Surface;

        _scope.Dock = DockStyle.Fill;
        _outer.Panel1.Controls.Add(_scope);

        _results.Dock = DockStyle.Fill;
        _results.Padding = new Padding(14, 12, 14, 0);
        _results.BackColor = Theme.Background;

        FlowLayoutPanel titleLine = new()
        {
            Dock = DockStyle.Top,
            Height = 24,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _resultsTitle.AutoSize = true;
        _resultsTitle.Font = BrowserVisual.Title;
        _resultsTitle.ForeColor = Theme.Text;
        _resultsTitle.AutoEllipsis = true;
        _resultsTitle.TextAlign = ContentAlignment.MiddleLeft;
        _resultsTitle.Margin = new Padding(0, 0, 10, 0);
        _resultsCount.AutoSize = true;
        _resultsCount.Font = BrowserVisual.RowSmall;
        _resultsCount.ForeColor = Theme.Muted;
        _resultsCount.TextAlign = ContentAlignment.MiddleLeft;
        _resultsCount.AutoEllipsis = true;
        _resultsCount.Margin = new Padding(0, 6, 0, 0);
        titleLine.Controls.Add(_resultsTitle);
        titleLine.Controls.Add(_resultsCount);

        _resultsBody.Dock = DockStyle.Top;
        _resultsBody.Height = 18;
        _resultsBody.Font = BrowserVisual.RowSmall;
        _resultsBody.ForeColor = Theme.Muted;
        _resultsBody.AutoEllipsis = true;
        _resultsBody.TextAlign = ContentAlignment.MiddleLeft;
        _resultsBody.Visible = false;

        _textureHost.Dock = DockStyle.Fill;
        _textureHost.BackColor = Theme.Background;
        _textureHost.Visible = false;
        _textureHeader.Dock = DockStyle.Top;
        _textureHeader.Columns = TextureColumns;
        _textureHeader.ShowThumbnails = true;
        _textureList.Dock = DockStyle.Fill;
        _textureList.Columns = TextureColumns;
        _textureList.ShowThumbnails = true;

        _textureToolbar.Dock = DockStyle.Top;
        _textureToolbar.Height = 38;
        _textureToolbar.ColumnCount = 6;
        _textureToolbar.RowCount = 1;
        _textureToolbar.BackColor = Theme.Background;
        _textureToolbar.Padding = new Padding(0, 6, 0, 4);
        _textureToolbar.Margin = new Padding(0);
        _textureToolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _textureToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _textureToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _textureToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _textureToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _textureToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _textureToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Label sortLabel = new()
        {
            Text = "Sort",
            ForeColor = Theme.Muted,
            Font = BrowserVisual.RowSmall,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 5, 0),
        };
        _sortCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _sortCombo.Width = 124;
        _sortCombo.Font = BrowserVisual.UiSmall;
        _sortCombo.BackColor = Theme.Background;
        _sortCombo.ForeColor = Theme.Text;
        _sortCombo.FlatStyle = FlatStyle.Flat;
        _sortCombo.Items.AddRange(["Archive order", "Hash", "Size", "Mips"]);
        _sortCombo.SelectedIndex = 0;
        _sortCombo.Anchor = AnchorStyles.Left;
        _sortCombo.Margin = new Padding(0, 0, 4, 0);
        StyleButton(_sortDirection, "Asc");
        _sortDirection.AutoSize = false;
        _sortDirection.Width = 46;
        _sortDirection.Height = 24;
        _sortDirection.Font = BrowserVisual.RowSmall;
        _sortDirection.Anchor = AnchorStyles.Left;
        _sortDirection.Margin = new Padding(0, 0, 10, 0);
        StyleButton(_exportSelected, "Export selected");
        _exportSelected.AutoSize = true;
        _exportSelected.Height = 24;
        _exportSelected.Font = BrowserVisual.RowSmall;
        _exportSelected.Anchor = AnchorStyles.Left;
        _exportSelected.Margin = new Padding(0, 0, 8, 0);
        _exportSelected.Enabled = false;
        _selectionCount.Text = "0 selected";
        _selectionCount.ForeColor = Theme.Muted;
        _selectionCount.Font = BrowserVisual.RowSmall;
        _selectionCount.AutoSize = true;
        _selectionCount.Anchor = AnchorStyles.Right;
        _selectionCount.Margin = new Padding(0);
        _selectionCount.TextAlign = ContentAlignment.MiddleRight;
        _textureToolbar.Controls.Add(sortLabel, 0, 0);
        _textureToolbar.Controls.Add(_sortCombo, 1, 0);
        _textureToolbar.Controls.Add(_sortDirection, 2, 0);
        _textureToolbar.Controls.Add(_exportSelected, 3, 0);
        _textureToolbar.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background }, 4, 0);
        _textureToolbar.Controls.Add(_selectionCount, 5, 0);

        _textureHost.Controls.Add(_textureList);
        _textureHost.Controls.Add(_textureHeader);

        _chooser.Dock = DockStyle.Fill;
        _chooser.Visible = false;

        _materialSlots.Dock = DockStyle.Fill;
        _materialSlots.Visible = false;

        _results.Controls.Add(_chooser);
        _results.Controls.Add(_materialSlots);
        _results.Controls.Add(_textureHost);
        _results.Controls.Add(_textureToolbar);
        _results.Controls.Add(_resultsBody);
        _results.Controls.Add(titleLine);
        _textureToolbar.PerformLayout();

        ConfigurePanel(_inspector, _inspectorTitle, _inspectorBody);
        _inspector.BackColor = Theme.Surface;
        _textureInspector.Dock = DockStyle.Fill;
        _textureInspector.Visible = false;
        _slotInspector.Dock = DockStyle.Fill;
        _slotInspector.Visible = false;
        _animationInspector.Dock = DockStyle.Fill;
        _animationInspector.Visible = false;
        _inspector.Controls.Add(_textureInspector);
        _inspector.Controls.Add(_slotInspector);
        _inspector.Controls.Add(_animationInspector);
        _inner.Panel1.Controls.Add(_results);
        _inner.Panel2.Controls.Add(_inspector);
        _outer.Panel2.Controls.Add(_inner);
        return _outer;
    }

    private Control BuildStatus()
    {
        _status.Dock = DockStyle.Fill;
        _status.Font = BrowserVisual.RowSmall;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = BrowserVisual.Faint;
        _status.BackColor = Theme.Surface;
        _status.Padding = new Padding(14, 0, 14, 0);
        _status.Text = "No game folder selected.";
        return _status;
    }

    private static void ConfigurePanel(Panel panel, Label title, Label body)
    {
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(14, 13, 14, 10);

        title.Dock = DockStyle.Top;
        title.Height = 24;
        title.Font = BrowserVisual.Title;
        title.ForeColor = Theme.Text;
        title.AutoEllipsis = true;
        title.Text = "";

        body.Dock = DockStyle.Top;
        body.Height = 60;
        body.Font = BrowserVisual.UiSmall;
        body.ForeColor = Theme.Muted;
        body.AutoEllipsis = true;
        body.Text = "";

        panel.Controls.Add(body);
        panel.Controls.Add(title);
    }

    private static void StyleButton(Button button, string text)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Text;
        button.Font = BrowserVisual.Ui;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = BrowserVisual.BorderStrong;
        button.FlatAppearance.MouseOverBackColor = BrowserVisual.SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = BrowserVisual.AccentDim;
        button.Margin = new Padding(0, 0, 4, 0);
    }

    private void WireScope()
    {
        _scope.OpenGameRequested += (_, _) => OpenGameFolder();
        _scope.LegacyRequested += (_, _) => new MainForm().Show(this);
        _scope.ArchiveSelected += path => SwitchArchive(path);
        _scope.TypeSelected += name => SwitchType(name);
    }

    private void WireTextureList()
    {
        _thumbnailCache = new ThumbnailCache();
        _textureList.ThumbnailProvider = row =>
            row.Tag is AssetEntry entry && entry.IsPreviewable && entry.Texture is { } texture
                ? _thumbnailCache.GetCached(texture)
                : _thumbnailCache.Placeholder;
        _textureList.ThumbnailRequested += row =>
        {
            if (row.Tag is AssetEntry entry && entry.IsPreviewable && entry.Texture is { } texture)
            {
                _thumbnailCache.Request(texture);
            }
        };
        _thumbnailCache.ThumbnailReady += (_, _) => _textureList.Invalidate();
        _textureList.SelectionChanged += (_, _) => OnListSelected();
    }

    private void OnListSelected()
    {
        if (_navigation is null)
        {
            return;
        }

        if (_navigation.State.View == BrowserView.Animations)
        {
            OnAnimationSelected();
        }
        else
        {
            OnTextureSelected();
        }

        UpdateSelectionCount();
    }

    private void OnTextureSelected()
    {
        if (_navigation is null
            || _navigation.State.View != BrowserView.Textures
            || _textureList.SelectedRow?.Tag is not AssetEntry entry
            || entry.Bitmap is null)
        {
            return;
        }

        _navigation.SelectTexture(ToOccurrence(entry), _textureList.TopIndex);
        RenderInspector();
        UpdateStatus();
    }

    private void WireInspector()
    {
        _textureInspector.UsedByRequested += async (_, _) => await OpenChooserAsync();
        _textureInspector.RevealRequested += (_, _) => RevealLinkedTexture();
        _textureInspector.ExportRequested += (_, _) => ExportTexture();
        _textureInspector.MipChanged += (_, mip) => _navigation?.SetMip(mip);
        _textureInspector.ChannelChanged += (_, channel) => _navigation?.SetChannel(channel);
        _textureInspector.CopyChanged += (_, index) => OnCopyChanged(index);
        _textureInspector.ViewChanged += (_, view) =>
        {
            if (_navigation is null)
            {
                return;
            }

            if (view.Fit)
            {
                _navigation.SetFit(true);
            }
            else
            {
                _navigation.SetView(view.Zoom, view.Pan);
            }
        };
        _chooser.OpenRequested += (_, row) => OpenMaterial(row);
        _materialSlots.SelectionChanged += (_, _) => OnMaterialSlotSelected();
        _materialSlots.OpenTextureRequested += (_, slot) =>
        {
            _navigation?.SelectSlot(slot);
            OpenSlotTexture();
        };
        _slotInspector.OpenTextureRequested += (_, _) => OpenSlotTexture();
        _slotInspector.CopyChanged += (_, index) => OnSlotCopyChanged(index);
    }

    private async Task OnShownAsync()
    {
        ApplySplitterLayout();
        ApplyScopeVisibility();
        if (_initialProfile is not null)
        {
            await LoadGameAsync(_initialProfile.Root);
            return;
        }

        if (_autoLoad)
        {
            string? last = Settings.LoadLastGameRoot();
            if (!string.IsNullOrWhiteSpace(last))
            {
                await LoadGameAsync(last);
            }
        }
    }

    private void OpenGameFolder()
    {
        using FolderBrowserDialog dialog = new()
        {
            SelectedPath = _profile?.Root ?? Settings.LoadLastGameRoot() ?? "",
            Description = "Open a Resonance game folder",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadGameAsync(dialog.SelectedPath);
        }
    }

    private async Task LoadGameAsync(string root)
    {
        if (!GameProfile.TryDetect(root, out GameProfile? profile, out string? error)
            || profile is null)
        {
            _status.Text = error ?? "That folder is not a supported game.";
            return;
        }

        _materialCancel?.Cancel();
        _materialCancel?.Dispose();
        _materialCancel = null;
        _materials = null;
        _materialTextures = null;
        _materialLoading = false;
        _materialBodies.Clear();
        _slotCopies = Array.Empty<TextureOccurrence>();
        _slotCopyHash = 0;
        _slotCopyArchive = "";
        _slotCopyIndex = 0;
        _chooser.Visible = false;
        _textureInspector.ShowEmpty();
        _loadCancel?.Cancel();
        _loadCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _loadCancel = cancel;
        _loading = true;
        _profile = profile;
        Text = $"APTStudio - {profile.Kind}";
        _scope.SetGame(profile.Kind.ToString(), "read-only");
        _status.Text = "Scanning archives...";
        Stopwatch watch = Stopwatch.StartNew();
        IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
        {
            if (!IsDisposed && cancel == _loadCancel)
            {
                _status.Text = $"Scanning {Path.GetFileName(item.ArchivePath)} {item.Index}/{item.Total}";
            }
        });

        try
        {
            AssetCatalog catalog = await Task.Run(
                () => AssetCatalog.Load(profile, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _loadCancel)
            {
                return;
            }

            watch.Stop();
            _assets = catalog.Entries;
            Dictionary<string, HashSet<(ulong Name, long Offset)>> textureKeys =
                new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> textureCounts =
                new(StringComparer.OrdinalIgnoreCase);
            foreach (AssetEntry entry in _assets)
            {
                if (!entry.IsBitmap || entry.Bitmap is null)
                {
                    continue;
                }

                if (!textureKeys.TryGetValue(entry.ArchivePath, out HashSet<(ulong, long)>? keys))
                {
                    keys = new HashSet<(ulong, long)>();
                    textureKeys[entry.ArchivePath] = keys;
                }

                if (keys.Add((entry.NameHash, entry.Row.BodyOffset)))
                {
                    textureCounts[entry.ArchivePath] = keys.Count;
                }
            }

            _archives = catalog.Archives
                .Select(archive => new ArchiveChoice(
                    archive.Path,
                    textureCounts.GetValueOrDefault(archive.Path)))
                .OrderBy(archive => archive.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _animationCounts = _assets
                .Where(entry => entry.ClassHash == AssetClass.Animation)
                .GroupBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            _animationOccurrences = _assets
                .Where(entry => entry.ClassHash == AssetClass.Animation)
                .GroupBy(entry => entry.NameHash)
                .ToDictionary(group => group.Key, group => group.Count());

            string selected = ChooseInitialArchive();
            _navigation = new BrowserNavigation(new BrowserState(
                BrowserView.Textures,
                selected,
                "",
                TextureFilter.All,
                null,
                0,
                0,
                ChannelView.Rgb,
                true,
                1f,
                PointF.Empty,
                null,
                0,
                null,
                null));

            _scope.SetArchives(_archives, selected);
            _scope.SetTypes(BuildTypes(selected), "Textures");
            _search.Enabled = true;
            _filterButton.Enabled = true;
            RenderState();
            _status.Text =
                $"{Path.GetFileName(selected)} · {_textureList.Rows.Count:N0} shown of " +
                $"{ArchiveCount(selected):N0} texture rows · {catalog.Archives.Count:N0} archives · " +
                $"{watch.ElapsedMilliseconds:N0} ms";
            if (_saveSettings)
            {
                Settings.SaveLastGameRoot(profile.Root);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _loadCancel)
            {
                _status.Text = ex.Message;
            }
        }
        finally
        {
            if (cancel == _loadCancel)
            {
                _loading = false;
            }
        }
    }

    private string ChooseInitialArchive()
    {
        ArchiveChoice? greece = _archives.FirstOrDefault(
            archive => archive.Name.Equals("GREECE.DPC", StringComparison.OrdinalIgnoreCase)
                && archive.Count > 0);
        if (greece is not null)
        {
            return greece.Path;
        }

        return _archives.FirstOrDefault(archive => archive.Count > 0)?.Path
            ?? _archives.FirstOrDefault()?.Path
            ?? "";
    }

    private int ArchiveCount(string archivePath)
    {
        ArchiveChoice? archive = _archives.FirstOrDefault(
            item => item.Path.Equals(archivePath, StringComparison.OrdinalIgnoreCase));
        return archive?.Count ?? 0;
    }

    private IReadOnlyList<TypeChoice> BuildTypes(string archivePath)
    {
        int textures = ArchiveCount(archivePath);
        int animations = _animationCounts.GetValueOrDefault(archivePath);
        return
        [
            new TypeChoice("Textures", textures, true),
            new TypeChoice("Animations", animations, true),
            new TypeChoice("Materials", 0, false),
            new TypeChoice("Meshes", 0, false),
            new TypeChoice("Skins", 0, false),
            new TypeChoice("Skeletons", 0, false),
            new TypeChoice("Audio", 0, false),
        ];
    }

    private void SwitchArchive(string archivePath)
    {
        if (_navigation is null
            || _navigation.State.ArchivePath.Equals(archivePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        BrowserState state = _navigation.State;
        BrowserView view = state.View == BrowserView.Placeholder
            ? BrowserView.Placeholder
            : state.View == BrowserView.Animations ? BrowserView.Animations : BrowserView.Textures;
        _navigation.SwitchScope(archivePath, view, state.PlaceholderName);
        _scope.SetTypes(BuildTypes(archivePath), TypeName(_navigation.State));
        RenderState();
    }

    private void SwitchType(string name)
    {
        if (_navigation is null)
        {
            return;
        }

        BrowserView view = name switch
        {
            "Textures" => BrowserView.Textures,
            "Animations" => BrowserView.Animations,
            _ => BrowserView.Placeholder,
        };
        _navigation.SwitchScope(
            _navigation.State.ArchivePath,
            view,
            view == BrowserView.Placeholder ? name : "");
        RenderState();
    }

    private void Back()
    {
        _navigation?.Back();
        RenderState();
    }

    private void Forward()
    {
        _navigation?.Forward();
        RenderState();
    }

    private void OnSearchChanged()
    {
        if (_navigation is null)
        {
            return;
        }

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void ApplySearch()
    {
        if (_navigation is null)
        {
            return;
        }

        _navigation.SetQuery(_search.Text);
        _navigation.SetListIndex(0);
        RenderState();
    }

    private void OnFilterRequested()
    {
        if (_navigation is null || _navigation.State.View != BrowserView.Textures)
        {
            return;
        }

        BrowserState state = _navigation.State;
        List<AssetEntry> textures = _assets
            .Where(entry => entry.IsBitmap
                && entry.Bitmap is not null
                && entry.ArchivePath.Equals(state.ArchivePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (textures.Count == 0)
        {
            return;
        }

        ContextMenuStrip menu = new()
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            ShowImageMargin = false,
            MinimumSize = new Size(190, 0),
        };

        void Add(ToolStripItemCollection items, string text, bool check, TextureFilter filter)
        {
            ToolStripMenuItem item = new(text)
            {
                Checked = check,
                ForeColor = Theme.Text,
            };
            item.Click += (_, _) =>
            {
                menu.Close();
                ApplyTextureFilter(filter);
            };
            items.Add(item);
        }

        ToolStripMenuItem format = new("Format") { ForeColor = Theme.Text };
        Add(format.DropDownItems, "All formats", state.Filter.Codec is null,
            state.Filter with { Codec = null });
        foreach (TextureCodec codec in new[]
        {
            TextureCodec.Bc1,
            TextureCodec.Bc3,
            TextureCodec.Unknown,
        })
        {
            if (!textures.Any(entry => entry.Bitmap!.Format.Codec == codec))
            {
                continue;
            }

            string label = codec switch
            {
                TextureCodec.Bc1 => "BC1",
                TextureCodec.Bc3 => "BC3",
                _ => "Other formats",
            };
            Add(format.DropDownItems, label, state.Filter.Codec == codec,
                state.Filter with { Codec = codec });
        }

        ToolStripMenuItem size = new("Size") { ForeColor = Theme.Text };
        Add(size.DropDownItems, "All sizes", state.Filter.Width is null && state.Filter.Height is null,
            state.Filter with { Width = null, Height = null });
        foreach (IGrouping<(int Width, int Height), AssetEntry> group in textures
            .GroupBy(entry => (entry.Bitmap!.Width, entry.Bitmap.Height))
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.Width)
            .ThenBy(group => group.Key.Height)
            .Take(12))
        {
            Add(size.DropDownItems, $"{group.Key.Width} × {group.Key.Height}",
                state.Filter.Width == group.Key.Width && state.Filter.Height == group.Key.Height,
                state.Filter with { Width = group.Key.Width, Height = group.Key.Height });
        }

        ToolStripMenuItem mips = new("Mips") { ForeColor = Theme.Text };
        Add(mips.DropDownItems, "All mips", state.Filter.MipCount is null,
            state.Filter with { MipCount = null });
        foreach (IGrouping<int, AssetEntry> group in textures
            .GroupBy(entry => entry.Bitmap!.MipCount)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(12))
        {
            Add(mips.DropDownItems, $"{group.Key} mips", state.Filter.MipCount == group.Key,
                state.Filter with { MipCount = group.Key });
        }

        menu.Items.Add(format);
        menu.Items.Add(size);
        menu.Items.Add(mips);
        Add(menu.Items, "Previewable only", state.Filter.PreviewableOnly,
            state.Filter with { PreviewableOnly = !state.Filter.PreviewableOnly });
        menu.Items.Add(new ToolStripSeparator());
        Add(menu.Items, "Clear filters", state.Filter.IsAll, TextureFilter.All);
        menu.Show(_filterButton, new Point(_filterButton.Width - 190, _filterButton.Height));
    }

    private void ApplyTextureFilter(TextureFilter filter)
    {
        if (_navigation is null)
        {
            return;
        }

        _navigation.SetFilter(filter);
        _navigation.SetListIndex(0);
        RenderState();
    }

    private void UpdateFilterButton(TextureFilter filter)
    {
        int active = (filter.Codec is null ? 0 : 1)
            + (filter.Width is null && filter.Height is null ? 0 : 1)
            + (filter.MipCount is null ? 0 : 1)
            + (filter.PreviewableOnly ? 1 : 0);
        _filterButton.Text = active == 0 ? "Filter" : $"Filter ({active})";
    }

    private void RenderState()
    {
        if (_navigation is null)
        {
            _resultsTitle.Text = "No game folder";
            _resultsCount.Text = "";
            _resultsBody.Text = "Open a supported game folder to start.";
            _resultsBody.Visible = true;
            _resultsBody.Height = 18;
            _inspectorTitle.Text = "";
            _inspectorBody.Text = "";
            _back.Enabled = false;
            _forward.Enabled = false;
            _scopeButton.Text = "No game";
            _crumbs.Text = "";
            _textureToolbar.Visible = false;
            return;
        }

        BrowserState state = _navigation.State;
        _scope.SelectArchive(state.ArchivePath);
        _scope.SetActiveType(TypeName(state));
        _scopeButton.Text = _navigation.IsChooserOpen ? "All archives" : ScopeChip(state);
        _back.Enabled = _navigation.CanBack;
        _forward.Enabled = _navigation.CanForward;
        _crumbs.Text = _navigation.IsChooserOpen ? ChooserCrumbs(state) : Crumbs(state);
        if (_search.Text != state.Query)
        {
            _search.Text = state.Query;
        }

        bool textureView = state.View == BrowserView.Textures || state.View == BrowserView.LinkedTexture;
        _textureInspector.Visible = textureView;
        _slotInspector.Visible = state.View == BrowserView.Material;
        _animationInspector.Visible = state.View == BrowserView.Animations;
        _materialSlots.Visible = state.View == BrowserView.Material
            || state.View == BrowserView.LinkedTexture;
        bool labelView = state.View == BrowserView.Placeholder;
        _inspectorTitle.Visible = labelView;
        _inspectorBody.Visible = labelView;

        if (_navigation.IsChooserOpen)
        {
            _textureHost.Visible = false;
            _textureToolbar.Visible = false;
            _chooser.Visible = true;
            _materialSlots.Visible = false;
            _slotInspector.Visible = false;
            _animationInspector.Visible = false;
            _textureInspector.Visible = true;
            _inspectorTitle.Visible = false;
            _inspectorBody.Visible = false;
            _resultsTitle.Text = "Materials";
            _resultsCount.Text = $"{_chooser.RowCount:N0}";
            _resultsBody.Text =
                $"All archives · {_chooser.RowCount:N0} uses in {_chooserArchiveCount:N0} archives · " +
                "select an occurrence to open";
            _resultsBody.Visible = true;
            _resultsBody.Height = 18;
            _filterButton.Enabled = false;
            _search.Enabled = false;
            RenderInspector();
            _status.Text =
                $"{Path.GetFileName(state.ArchivePath)} · {_chooser.RowCount:N0} material uses · " +
                "all archives";
            return;
        }

        _chooser.Visible = false;
        _search.Enabled = true;
        _filterButton.Enabled = state.View == BrowserView.Textures;
        UpdateFilterButton(state.Filter);
        _textureHost.Visible = state.View == BrowserView.Textures
            || state.View == BrowserView.Animations;
        _textureToolbar.Visible = state.View is BrowserView.Textures or BrowserView.Animations;
        _resultsBody.Visible = true;
        _resultsBody.Height = labelView ? 44 : 18;

        switch (state.View)
        {
            case BrowserView.Textures:
                _resultsTitle.Text = "Textures";
                SetListMode(false);
                RenderTextureList(state);
                _resultsCount.Text = $"{_textureList.Rows.Count:N0}";
                _resultsBody.Text =
                    $"{Path.GetFileName(state.ArchivePath)} · {_textureList.Rows.Count:N0} of " +
                    $"{ArchiveCount(state.ArchivePath):N0} textures · {SortLabel(state)}";
                break;
            case BrowserView.Animations:
                _resultsTitle.Text = "Animations";
                SetListMode(true);
                RenderAnimationList(state);
                _resultsCount.Text = $"{_textureList.Rows.Count:N0}";
                string clipNoun = _textureList.Rows.Count == 1 ? "clip" : "clips";
                _resultsBody.Text =
                    $"{Path.GetFileName(state.ArchivePath)} · {_textureList.Rows.Count:N0} " +
                    $"{clipNoun} · hash order";
                break;
            case BrowserView.Material:
                _resultsTitle.Text = "Material";
                ShowMaterialSlots(state);
                RenderSlotInspector();
                _resultsCount.Text =
                    $"{MaterialFor(state)?.Slots.Count ?? 0:N0} of {MaterialSlotRoles.Count} slots";
                _resultsBody.Text = state.Material is { } material
                    ? $"0x{material.Hash:X16} · {material.Code} · {material.ArchiveName} · " +
                        $"{MaterialFor(state)?.Slots.Count ?? 0:N0} filled of " +
                        $"{MaterialSlotRoles.Count} slots"
                    : "";
                break;
            case BrowserView.LinkedTexture:
                _resultsTitle.Text = "Material";
                ShowMaterialSlots(state);
                RenderInspector();
                _resultsCount.Text = "";
                _resultsBody.Text = state.Material is { } linkedMaterial
                    ? $"0x{linkedMaterial.Hash:X16} · {linkedMaterial.Code} · " +
                        $"{linkedMaterial.ArchiveName} · slot {state.MaterialSlot}"
                    : "";
                break;
            default:
                _resultsTitle.Text = state.PlaceholderName;
                _resultsCount.Text = "";
                _resultsBody.Text =
                    "This view moves in a later slice.\n" +
                    "Use Open legacy browser in the scope pane for the current view.";
                _inspectorTitle.Text = "";
                _inspectorBody.Text = "";
                break;
        }

        if (!_loading)
        {
            UpdateStatus();
        }
    }

    private void RenderTextureList(BrowserState state)
    {
        IReadOnlyList<AssetRow> rows = BuildTextureRows(state);
        string? keep = state.Texture is null ? null : OccurrenceKey(state.Texture);
        IReadOnlyCollection<string> selectedKeys = _textureList.SelectedKeys.ToArray();
        _textureList.SetRows(rows, keep, selectedKeys);
        bool keepFound = keep is not null
            && _textureList.SelectedRow?.Key.Equals(keep, StringComparison.Ordinal) == true;
        if (!keepFound)
        {
            int first = -1;
            for (int index = 0; index < rows.Count; index++)
            {
                if (rows[index].Tag is AssetEntry entry && entry.IsPreviewable)
                {
                    first = index;
                    break;
                }
            }

            if (first < 0 && rows.Count > 0)
            {
                first = 0;
            }

            _textureList.SelectedIndex = first;
            if (first >= 0)
            {
                OnTextureSelected();
            }
        }

        _textureList.TopIndex = state.ListIndex;
        _textureList.EnsureVisible(_textureList.SelectedIndex);
        if (rows.Count == 0)
        {
            _textureInspector.ShowEmpty();
        }
        else
        {
            RenderInspector();
        }

        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        int count = _textureList.SelectedKeys.Count;
        _selectionCount.Text = $"{count:N0} selected";
        _exportSelected.Enabled = count > 0
            && _textureList.SelectedRows.Any(row =>
                row.Tag is AssetEntry entry && entry.IsPreviewable);
    }

    private void SetListMode(bool animations)
    {
        bool narrow = ClientSize.Width < BrowserLayout.CollapseWidth;
        IReadOnlyList<AssetColumn> columns = animations
            ? AnimationColumns
            : narrow ? NarrowTextureColumns : TextureColumns;
        _textureHeader.Columns = columns;
        _textureList.Columns = columns;
        _textureHeader.ShowThumbnails = !animations;
        _textureList.ShowThumbnails = !animations;
        _textureToolbar.Visible = !animations;
        _textureList.AllowMultiSelect = !animations;
        if (!animations && _navigation is not null)
        {
            SyncSortControls(_navigation.State);
        }
    }

    private void SyncSortControls(BrowserState state)
    {
        _suppressSort = true;
        try
        {
            _sortCombo.SelectedIndex = state.Sort switch
            {
                TextureSort.Hash => 1,
                TextureSort.Size => 2,
                TextureSort.Mips => 3,
                _ => 0,
            };
            _sortDirection.Text = state.SortDescending ? "Desc" : "Asc";
        }
        finally
        {
            _suppressSort = false;
        }
    }

    private void RenderAnimationList(BrowserState state)
    {
        IReadOnlyList<AssetRow> rows = BuildAnimationRows(state);
        string? keep = state.Animation is null ? null : AnimationKey(state.Animation);
        _textureList.SetRows(rows, keep);
        bool keepFound = keep is not null
            && _textureList.SelectedRow?.Key.Equals(keep, StringComparison.Ordinal) == true;
        if (!keepFound)
        {
            _textureList.SelectedIndex = rows.Count > 0 ? 0 : -1;
            if (rows.Count > 0)
            {
                OnAnimationSelected();
            }
        }

        _textureList.TopIndex = state.ListIndex;
        _textureList.EnsureVisible(_textureList.SelectedIndex);
        if (rows.Count == 0)
        {
            _animationInspector.ShowEmpty();
        }
        else
        {
            RenderAnimation();
        }
    }

    private IReadOnlyList<AssetRow> BuildAnimationRows(BrowserState state)
    {
        List<AssetRow> rows = new();
        HashSet<(string ArchivePath, long Offset)> seen = new();
        string query = state.Query.Trim();
        string needle = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? query[2..]
            : query;
        foreach (AssetEntry entry in _assets)
        {
            if (entry.ClassHash != AssetClass.Animation
                || !entry.ArchivePath.Equals(state.ArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add((entry.BodyArchivePath, entry.Row.BodyOffset)))
            {
                continue;
            }

            if (needle.Length > 0
                && !entry.NameHash.ToString("X16", CultureInfo.InvariantCulture)
                    .Contains(needle, StringComparison.OrdinalIgnoreCase)
                && !entry.ArchivePath.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add(new AssetRow(
                AnimationKey(entry),
                [
                    $"0x{entry.NameHash:X16}",
                    _animationOccurrences.GetValueOrDefault(entry.NameHash)
                        .ToString("N0", CultureInfo.InvariantCulture),
                ],
                entry));
        }

        rows.Sort((left, right) => ((AssetEntry)left.Tag!).NameHash
            .CompareTo(((AssetEntry)right.Tag!).NameHash));
        return rows;
    }

    private void OnAnimationSelected()
    {
        if (_navigation is null
            || _navigation.State.View != BrowserView.Animations
            || _textureList.SelectedRow?.Tag is not AssetEntry entry)
        {
            return;
        }

        _navigation.SetAnimation(ToAnimation(entry), _textureList.TopIndex);
        RenderAnimation();
        UpdateStatus();
    }

    private void RenderAnimation()
    {
        if (_navigation?.State.Animation is not { } animation || _profile is null)
        {
            _animationInspector.ShowEmpty();
            return;
        }

        AssetEntry? entry = FindAnimationEntry(animation);
        if (entry is null)
        {
            _animationInspector.ShowEmpty();
            return;
        }

        _animationInspector.SetClip(entry, _assets, _profile);
    }

    private AssetEntry? FindAnimationEntry(AnimationOccurrence animation) =>
        _assets.FirstOrDefault(entry =>
            entry.ClassHash == AssetClass.Animation
            && entry.NameHash == animation.Hash
            && entry.BodyArchivePath.Equals(
                animation.BodyArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.Row.BodyOffset == animation.BodyOffset);

    private static AnimationOccurrence ToAnimation(AssetEntry entry) => new(
        entry.NameHash,
        entry.ArchivePath,
        entry.BodyArchivePath,
        entry.Row.BodyOffset);

    private static string AnimationKey(AssetEntry entry) =>
        $"{entry.BodyArchivePath}|{entry.Row.BodyOffset}|{entry.NameHash:X16}";

    private static string AnimationKey(AnimationOccurrence animation) =>
        $"{animation.BodyArchivePath}|{animation.BodyOffset}|{animation.Hash:X16}";

    private IReadOnlyList<AssetRow> BuildTextureRows(BrowserState state)
    {
        List<AssetRow> rows = new();
        if (_assets.Count == 0)
        {
            return rows;
        }

        string query = state.Query.Trim();
        string needle = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? query[2..]
            : query;
        HashSet<(ulong Name, long Offset)> seen = new();
        foreach (AssetEntry entry in _assets)
        {
            if (!entry.IsBitmap || entry.Bitmap is null)
            {
                continue;
            }

            if (!entry.ArchivePath.Equals(state.ArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add((entry.NameHash, entry.Row.BodyOffset)))
            {
                continue;
            }

            BitmapHead head = entry.Bitmap;
            if (!state.Filter.Matches(head))
            {
                continue;
            }

            if (needle.Length > 0 && !Matches(entry, needle))
            {
                continue;
            }

            rows.Add(new AssetRow(
                OccurrenceKey(entry),
                [
                    $"0x{entry.NameHash:X16}",
                    $"{head.Width} × {head.Height}",
                    head.MipCount.ToString(CultureInfo.InvariantCulture),
                    head.Format.DisplayName,
                    head.Storage.ToString(),
                ],
                entry));
        }

        rows.Sort((left, right) =>
        {
            AssetEntry a = (AssetEntry)left.Tag!;
            AssetEntry b = (AssetEntry)right.Tag!;
            int order = state.Sort switch
            {
                TextureSort.Hash => a.NameHash.CompareTo(b.NameHash),
                TextureSort.Size => ((long)a.Bitmap!.Width * a.Bitmap.Height)
                    .CompareTo((long)b.Bitmap!.Width * b.Bitmap.Height),
                TextureSort.Mips => a.Bitmap!.MipCount.CompareTo(b.Bitmap!.MipCount),
                _ => a.Row.BodyOffset.CompareTo(b.Row.BodyOffset),
            };
            if (order == 0)
            {
                order = a.NameHash.CompareTo(b.NameHash);
            }

            if (order == 0)
            {
                order = a.Row.BodyOffset.CompareTo(b.Row.BodyOffset);
            }

            return state.SortDescending ? -order : order;
        });
        return rows;
    }

    private static bool Matches(AssetEntry entry, string needle)
    {
        BitmapHead head = entry.Bitmap!;
        return entry.NameHash.ToString("X16", CultureInfo.InvariantCulture)
                .Contains(needle, StringComparison.OrdinalIgnoreCase)
            || entry.ArchivePath.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || $"{head.Width}x{head.Height}".Contains(needle, StringComparison.OrdinalIgnoreCase)
            || $"{head.Width} × {head.Height}".Contains(needle, StringComparison.OrdinalIgnoreCase)
            || head.Format.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || head.Storage.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private void RenderInspector()
    {
        if (_navigation is null)
        {
            return;
        }

        BrowserState state = _navigation.State;
        if (state.View is not (BrowserView.Textures or BrowserView.LinkedTexture))
        {
            return;
        }

        TextureOccurrence? occurrence = state.View == BrowserView.Textures
            ? state.Texture
            : state.LinkedTexture;
        if (occurrence is null)
        {
            _textureInspector.ShowEmpty();
            return;
        }

        AssetEntry? entry = FindEntry(occurrence);
        if (entry?.Texture is not { } texture)
        {
            _textureInspector.ShowEmpty();
            return;
        }

        _inspectorCopies = BuildCopies(occurrence);
        int copyIndex = 0;
        for (int index = 0; index < _inspectorCopies.Count; index++)
        {
            if (OccurrenceKey(_inspectorCopies[index]) == OccurrenceKey(occurrence))
            {
                copyIndex = index;
                break;
            }
        }

        bool linked = state.View == BrowserView.LinkedTexture;
        string? context = occurrence.IsExternal ? $"Data in {occurrence.BodyArchiveName}" : null;
        if (linked && state.Material is { } material)
        {
            bool local = _inspectorCopies.Any(copy => copy.ArchiveName.Equals(
                material.ArchiveName, StringComparison.OrdinalIgnoreCase));
            string link = local
                ? $"Linked from {material.ArchiveName} slot {state.MaterialSlot}"
                : $"No {material.ArchiveName} copy; showing {occurrence.ArchiveName}";
            context = context is null ? link : $"{link} · {context}";
        }

        _textureInspector.SetTexture(
            occurrence,
            texture,
            _inspectorCopies,
            copyIndex,
            state.Mip,
            state.Channel,
            state.Fit,
            state.Zoom,
            state.Pan,
            linked);
        _textureInspector.SetContext(context);
    }

    private AssetEntry? FindEntry(TextureOccurrence occurrence) =>
        _assets.FirstOrDefault(entry =>
            entry.IsBitmap
            && entry.Bitmap is not null
            && entry.NameHash == occurrence.Hash
            && entry.ArchivePath.Equals(occurrence.ArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.BodyArchivePath.Equals(
                occurrence.BodyArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.Row.BodyOffset == occurrence.BodyOffset);

    private IReadOnlyList<TextureOccurrence> BuildCopies(TextureOccurrence selected)
    {
        List<TextureOccurrence> copies = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        void Add(TextureOccurrence occurrence)
        {
            if (keys.Add(OccurrenceKey(occurrence)))
            {
                copies.Add(occurrence);
            }
        }

        Add(selected);
        foreach (AssetEntry entry in _assets)
        {
            if (entry.IsBitmap && entry.Bitmap is not null && entry.NameHash == selected.Hash)
            {
                Add(ToOccurrence(entry));
            }
        }

        copies.Sort((left, right) => string.Compare(
            left.ArchiveName, right.ArchiveName, StringComparison.OrdinalIgnoreCase));
        return copies;
    }

    private void OnCopyChanged(int index)
    {
        if (_navigation is null || index < 0 || index >= _inspectorCopies.Count)
        {
            return;
        }

        if (_navigation.State.View == BrowserView.LinkedTexture)
        {
            _navigation.SetLinkedTextureCopy(_inspectorCopies[index]);
        }
        else
        {
            _navigation.SetTextureCopy(_inspectorCopies[index]);
        }

        RenderInspector();
    }

    private async Task ExportSelectedAsync()
    {
        if (_profile is null)
        {
            return;
        }

        List<AssetEntry> entries = _textureList.SelectedRows
            .Select(row => row.Tag)
            .OfType<AssetEntry>()
            .Where(entry => entry.IsBitmap && entry.IsPreviewable && entry.Texture is not null)
            .ToList();
        if (entries.Count == 0)
        {
            _status.Text = "No previewable textures selected.";
            return;
        }

        using FolderBrowserDialog dialog = new()
        {
            Description = "Export selected textures to a folder",
            UseDescriptionForTitle = true,
            SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (GameFolderGuard.IsInside(_profile.Root, dialog.SelectedPath))
        {
            _status.Text = "Refusing to write inside the game folder.";
            return;
        }

        string folder = dialog.SelectedPath;
        _exportSelected.Enabled = false;
        _status.Text = $"Exporting {entries.Count:N0} textures...";
        (int Exported, int Skipped) result = await Task.Run(() =>
        {
            HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
            int exported = 0;
            int skipped = 0;
            foreach (AssetEntry entry in entries)
            {
                try
                {
                    string name = ExportNames.Unique(
                        folder, entry.ArchivePath, entry.NameHash, used);
                    TextureExport.ExportPng(entry.Texture!, Path.Combine(folder, name));
                    exported++;
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
                    or IOException or UnauthorizedAccessException)
                {
                    skipped++;
                }
            }

            return (exported, skipped);
        });

        _status.Text =
            $"Exported {result.Exported:N0}, skipped {result.Skipped:N0}.";
        UpdateSelectionCount();
    }

    private void ExportTexture()
    {
        if (_textureInspector.Texture is not { } texture
            || _textureInspector.Occurrence is not { } occurrence
            || _profile is null)
        {
            return;
        }

        using SaveFileDialog dialog = new()
        {
            Filter = "PNG (*.png)|*.png",
            FileName = $"{Path.GetFileNameWithoutExtension(occurrence.ArchivePath)}_" +
                $"{occurrence.Hash:X16}.png",
            Title = "Export PNG",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string? folder = Path.GetDirectoryName(dialog.FileName);
        if (folder is null || GameFolderGuard.IsInside(_profile.Root, folder))
        {
            _status.Text = "Refusing to write inside the game folder.";
            return;
        }

        try
        {
            TextureExport.ExportPng(texture, dialog.FileName);
            _status.Text = $"Exported {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
            or IOException or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
        }
    }

    private async Task OpenChooserAsync()
    {
        if (_navigation is null
            || _navigation.IsChooserOpen
            || _materialLoading)
        {
            return;
        }

        BrowserState initial = _navigation.State;
        if (initial.View is not (BrowserView.Textures or BrowserView.LinkedTexture))
        {
            return;
        }

        TextureOccurrence? source = initial.View == BrowserView.Textures
            ? initial.Texture
            : initial.LinkedTexture;
        if (source is null)
        {
            return;
        }

        _navigation.SetListIndex(_textureList.TopIndex);
        if (_materialTextures is null && !await LoadMaterialIndexAsync())
        {
            return;
        }

        BrowserState current = _navigation.State;
        TextureOccurrence? currentSource = current.View == BrowserView.Textures
            ? current.Texture
            : current.LinkedTexture;
        if (currentSource is null
            || OccurrenceKey(currentSource) != OccurrenceKey(source)
            || _materialTextures is null
            || !_materialTextures.ByTexture.TryGetValue(source.Hash, out IReadOnlyList<MaterialTextureUse>? uses)
            || uses.Count == 0)
        {
            _status.Text = "No material uses.";
            return;
        }

        List<MaterialUseRow> rows = new();
        foreach (MaterialTextureUse use in uses)
        {
            foreach (int slot in use.Slots)
            {
                rows.Add(new MaterialUseRow(use, slot));
            }
        }

        if (rows.Count == 0)
        {
            _status.Text = "No material uses.";
            return;
        }

        int archiveCount = rows
            .Select(row => row.Archive)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        _navigation.OpenChooser();
        _chooserArchiveCount = archiveCount;
        _chooser.SetRows(rows);
        RenderState();
        _chooser.FocusList();
    }

    private async Task<bool> LoadMaterialIndexAsync()
    {
        if (_profile is null)
        {
            return false;
        }

        _materialLoading = true;
        _textureInspector.SetBusy(true);
        _status.Text = "Loading materials...";
        _materialCancel?.Cancel();
        _materialCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _materialCancel = cancel;
        IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
        {
            if (!IsDisposed && cancel == _materialCancel)
            {
                _status.Text =
                    $"Materials {Path.GetFileName(item.ArchivePath)} {item.Index}/{item.Total}";
            }
        });

        try
        {
            MaterialCatalog catalog = await Task.Run(
                () => MaterialCatalog.Load(_profile, false, progress, cancel.Token),
                cancel.Token);
            MaterialTextureIndex index = await Task.Run(
                () => MaterialTextureIndex.Load(catalog, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _materialCancel)
            {
                return false;
            }

            _materials = catalog;
            _materialTextures = index;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _materialCancel)
            {
                _status.Text = ex.Message;
            }

            return false;
        }
        finally
        {
            if (cancel == _materialCancel)
            {
                _materialLoading = false;
                _textureInspector.SetBusy(false);
            }
        }
    }

    private void OpenMaterial(MaterialUseRow row)
    {
        if (_navigation is null)
        {
            return;
        }

        MaterialOccurrence occurrence = new(
            row.Hash,
            row.Use.Material.ArchivePath,
            row.Use.Material.Offset,
            row.Code);
        try
        {
            using FileStream stream = new(
                row.Use.Material.ArchivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            _materialBodies[(row.Use.Material.ArchivePath, row.Use.Material.Offset, row.Hash)] =
                MaterialDecoder.Read(
                    stream, row.Use.Material.ArchivePath, row.Use.Material.Hit);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
            return;
        }

        _slotCopies = Array.Empty<TextureOccurrence>();
        _slotCopyHash = 0;
        _slotCopyArchive = "";
        _slotCopyIndex = 0;
        _navigation.OpenMaterial(occurrence, row.Slot);
        RenderState();
    }

    private MaterialData? MaterialFor(BrowserState state)
    {
        if (state.Material is not { } occurrence)
        {
            return null;
        }

        (string Archive, long Offset, ulong Hash) key = (
            occurrence.ArchivePath, occurrence.BodyOffset, occurrence.Hash);
        if (_materialBodies.TryGetValue(key, out MaterialData? cached))
        {
            return cached;
        }

        MaterialEntry? entry = _materials?.Entries.FirstOrDefault(candidate =>
            candidate.NameHash == occurrence.Hash
            && candidate.Offset == occurrence.BodyOffset
            && candidate.ArchivePath.Equals(
                occurrence.ArchivePath, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        try
        {
            using FileStream stream = new(
                entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            MaterialData data = MaterialDecoder.Read(stream, entry.ArchivePath, entry.Hit);
            _materialBodies[key] = data;
            return data;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
            return null;
        }
    }

    private void RevealLinkedTexture()
    {
        if (_navigation is null || _navigation.State.View != BrowserView.LinkedTexture)
        {
            return;
        }

        _navigation.RevealLinkedTexture();
        RenderState();
    }

    private void ShowMaterialSlots(BrowserState state)
    {
        MaterialData? data = MaterialFor(state);
        if (data is null)
        {
            return;
        }

        Dictionary<ulong, int> counts = new();
        foreach (MaterialSlot slot in data.Slots)
        {
            counts[slot.TextureHash] = BuildCopiesForHash(slot.TextureHash).Count;
        }

        _materialSlots.SetMaterial(
            data,
            state.MaterialSlot,
            hash => counts.GetValueOrDefault(hash));
    }

    private void RenderSlotInspector()
    {
        if (_navigation is null || _navigation.State.Material is not { } material)
        {
            _slotInspector.ShowEmpty();
            return;
        }

        MaterialData? data = MaterialFor(_navigation.State);
        if (data is null)
        {
            _slotInspector.ShowEmpty();
            return;
        }

        MaterialSlot? slot = data.Slots
            .FirstOrDefault(item => item.Index == _navigation.State.MaterialSlot);
        if (slot is null)
        {
            _slotInspector.ShowEmpty();
            return;
        }

        IReadOnlyList<TextureOccurrence> copies = BuildCopiesForHash(slot.TextureHash);
        if (copies.Count == 0)
        {
            _slotInspector.ShowEmpty();
            return;
        }

        if (_slotCopyHash != slot.TextureHash
            || !_slotCopyArchive.Equals(material.ArchiveName, StringComparison.OrdinalIgnoreCase)
            || _slotCopies.Count != copies.Count)
        {
            _slotCopies = copies;
            _slotCopyHash = slot.TextureHash;
            _slotCopyArchive = material.ArchiveName;
            _slotCopyIndex = ChooseCopyIndex(copies, material.ArchiveName);
        }

        int index = Math.Clamp(_slotCopyIndex, 0, _slotCopies.Count - 1);
        TextureOccurrence occurrence = _slotCopies[index];
        AssetEntry? entry = FindEntry(occurrence);
        if (entry?.Texture is not { } texture)
        {
            _slotInspector.ShowEmpty();
            return;
        }

        string context = _slotCopies.Any(copy => copy.ArchiveName.Equals(
            material.ArchiveName, StringComparison.OrdinalIgnoreCase))
                ? $"Linked from {material.ArchiveName} slot {slot.Index}"
                : $"No {material.ArchiveName} copy; showing {occurrence.ArchiveName}";
        if (occurrence.IsExternal)
        {
            context += $" · Data in {occurrence.BodyArchiveName}";
        }
        _slotInspector.SetSlot(
            data,
            slot,
            occurrence,
            texture,
            _slotCopies,
            index,
            context);
    }

    private void OnMaterialSlotSelected()
    {
        if (_navigation is null)
        {
            return;
        }

        int slot = _materialSlots.SelectedSlot;
        if (slot < 0)
        {
            return;
        }

        _navigation.SelectSlot(slot);
        _slotCopyHash = 0;
        _slotCopies = Array.Empty<TextureOccurrence>();
        RenderSlotInspector();
    }

    private void OpenSlotTexture()
    {
        if (_navigation is null || _slotCopies.Count == 0)
        {
            return;
        }

        int index = Math.Clamp(_slotCopyIndex, 0, _slotCopies.Count - 1);
        _navigation.OpenLinkedTexture(_slotCopies[index]);
        RenderState();
    }

    private void OnSlotCopyChanged(int index)
    {
        if (_slotCopies.Count == 0)
        {
            return;
        }

        _slotCopyIndex = Math.Clamp(index, 0, _slotCopies.Count - 1);
        RenderSlotInspector();
    }

    private IReadOnlyList<TextureOccurrence> BuildCopiesForHash(ulong hash)
    {
        List<TextureOccurrence> copies = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (AssetEntry entry in _assets)
        {
            if (!entry.IsBitmap || entry.Bitmap is null || entry.NameHash != hash)
            {
                continue;
            }

            TextureOccurrence occurrence = ToOccurrence(entry);
            if (keys.Add(OccurrenceKey(occurrence)))
            {
                copies.Add(occurrence);
            }
        }

        copies.Sort((left, right) => string.Compare(
            left.ArchiveName, right.ArchiveName, StringComparison.OrdinalIgnoreCase));
        return copies;
    }

    private static int ChooseCopyIndex(
        IReadOnlyList<TextureOccurrence> copies,
        string materialArchive)
    {
        for (int index = 0; index < copies.Count; index++)
        {
            if (copies[index].ArchiveName.Equals(
                materialArchive, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private void UpdateStatus()
    {
        if (_navigation is null || _loading)
        {
            return;
        }

        BrowserState state = _navigation.State;
        if (state.View == BrowserView.Textures)
        {
            _status.Text =
                $"{Path.GetFileName(state.ArchivePath)} · {_textureList.Rows.Count:N0} of " +
                $"{ArchiveCount(state.ArchivePath):N0} textures · {_archives.Count:N0} archives";
            return;
        }

        if (state.View == BrowserView.Animations)
        {
            string noun = _textureList.Rows.Count == 1 ? "clip" : "clips";
            _status.Text =
                $"{Path.GetFileName(state.ArchivePath)} · {_textureList.Rows.Count:N0} {noun} · " +
                $"{_archives.Count:N0} archives";
            return;
        }

        if (state.View == BrowserView.Material && state.Material is { } material)
        {
            _status.Text =
                $"{material.ArchiveName} · {material.Code} · " +
                $"{MaterialFor(state)?.Slots.Count ?? 0} slots";
            return;
        }

        if (state.View == BrowserView.LinkedTexture && state.LinkedTexture is { } linked)
        {
            string link = linked.IsExternal
                ? $"{linked.ArchiveName} · data {linked.BodyArchiveName}"
                : linked.ArchiveName;
            _status.Text = $"{state.Material?.ArchiveName ?? "Material"} · linked {link}";
            return;
        }

        _status.Text =
            $"{Path.GetFileName(state.ArchivePath)} · {TypeName(state)} · " +
            $"{ArchiveCount(state.ArchivePath):N0} rows · {_archives.Count:N0} archives";
    }

    private static string OccurrenceKey(AssetEntry entry) =>
        $"{entry.ArchivePath}|{entry.Row.BodyOffset}|{entry.NameHash:X16}";

    private static string OccurrenceKey(TextureOccurrence occurrence) =>
        $"{occurrence.ArchivePath}|{occurrence.BodyOffset}|{occurrence.Hash:X16}";

    private static TextureOccurrence ToOccurrence(AssetEntry entry) => new(
        entry.NameHash,
        entry.ArchivePath,
        entry.BodyArchivePath,
        entry.Row.BodyOffset,
        entry.Bitmap!);

    private static string TypeName(BrowserState state) => state.View switch
    {
        BrowserView.Textures => "Textures",
        BrowserView.Animations => "Animations",
        BrowserView.Placeholder => state.PlaceholderName,
        _ => "Textures",
    };

    private static string ScopeChip(BrowserState state) =>
        Path.GetFileName(state.ArchivePath);

    private static string SortLabel(BrowserState state)
    {
        string sort = state.Sort switch
        {
            TextureSort.Hash => "hash order",
            TextureSort.Size => "size order",
            TextureSort.Mips => "mip order",
            _ => "archive order",
        };
        return state.SortDescending ? $"{sort}, descending" : sort;
    }

    private string Crumbs(BrowserState state)
    {
        string archive = Path.GetFileName(state.ArchivePath);
        bool compact = ClientSize.Width < BrowserLayout.CollapseWidth;
        return state.View switch
        {
            BrowserView.Textures => $"{archive} / Textures",
            BrowserView.Animations => $"{archive} / Animations",
            BrowserView.Material => state.Material is { } material
                ? compact
                    ? $"{archive} / 0x{material.Hash:X16}"
                    : $"{archive} / Textures / 0x{material.Hash:X16} / Materials"
                : $"{archive} / Materials",
            BrowserView.LinkedTexture => state.LinkedTexture is { } linked
                ? compact
                    ? $"{archive} / {linked.ArchiveName}"
                        + (linked.IsExternal ? $" / {linked.BodyArchiveName}" : "")
                    : $"{archive} / Textures / Materials / {linked.ArchiveName} occurrence"
                        + (linked.IsExternal ? $" / data {linked.BodyArchiveName}" : "")
                : $"{archive} / Textures / Materials",
            _ => $"{archive} / {state.PlaceholderName}",
        };
    }

    private string ChooserCrumbs(BrowserState state)
    {
        string archive = Path.GetFileName(state.ArchivePath);
        if (ClientSize.Width < BrowserLayout.CollapseWidth)
        {
            return $"{archive} / Materials (all archives)";
        }

        string source = state.Texture is { } texture
            ? $"0x{texture.Hash:X16}"
            : "Source";
        return $"{archive} / Textures / {source} / Materials (all archives)";
    }

    private void OnResize()
    {
        bool wasNarrow = _lastNarrow;
        bool narrow = BrowserLayout.ShouldCollapseSidebar(ClientSize.Width);
        if (narrow && !_lastNarrow)
        {
            _scopeOpen = false;
        }
        else if (!narrow && _lastNarrow)
        {
            _scopeOpen = true;
        }

        _lastNarrow = narrow;
        ApplyScopeVisibility();
        if (wasNarrow != narrow && _navigation is not null)
        {
            RenderState();
        }
    }

    private void ToggleScope()
    {
        _scopeOpen = !_scopeOpen;
        ApplyScopeVisibility();
    }

    private void ApplyScopeVisibility()
    {
        _outer.Panel1Collapsed = !_scopeOpen;
        _scopeButton.Text = _navigation is null
            ? "No game"
            : _navigation.IsChooserOpen ? "All archives" : ScopeChip(_navigation.State);
        _scopeButton.Enabled = true;
    }

    private void ApplySplitterLayout()
    {
        _outer.Panel1MinSize = BrowserLayout.SidebarMinimum;
        _outer.Panel2MinSize = BrowserLayout.ResultsMinimum;
        _inner.Panel1MinSize = BrowserLayout.ResultsMinimum;
        _inner.Panel2MinSize = BrowserLayout.InspectorMinimum;

        if (!_outer.Panel1Collapsed)
        {
            int width = BrowserLayout.ClampSidebar(_sidebarWidth, ClientSize.Width);
            int max = Math.Max(_outer.Panel1MinSize,
                _outer.Width - _outer.Panel2MinSize - _outer.SplitterWidth);
            _outer.SplitterDistance = Math.Clamp(width, _outer.Panel1MinSize, max);
        }

        int inspector = BrowserLayout.ClampInspector(_inspectorWidth, ClientSize.Width);
        int distance = _inner.Width - inspector - _inner.SplitterWidth;
        int min = _inner.Panel1MinSize;
        int maxDistance = Math.Max(min,
            _inner.Width - _inner.Panel2MinSize - _inner.SplitterWidth);
        _inner.SplitterDistance = Math.Clamp(distance, min, maxDistance);
    }

    private void SaveLayout()
    {
        if (!_saveSettings || _outer.IsDisposed)
        {
            return;
        }

        int sidebar = _outer.Panel1Collapsed ? _sidebarWidth : _outer.SplitterDistance;
        int inspector = _inner.Panel2.Width;
        Settings.SaveBrowserLayout(
            BrowserLayout.ClampSidebar(sidebar, ClientSize.Width),
            BrowserLayout.ClampInspector(inspector, ClientSize.Width));
    }
}
