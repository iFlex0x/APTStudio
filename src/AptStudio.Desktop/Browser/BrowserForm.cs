using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using AptStudio.Core.Assets;
using AptStudio.Core.Audio;
using AptStudio.Core.Games;
using AptStudio.Core.Links;
using AptStudio.Core.Materials;
using AptStudio.Core.Meshes;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Skins;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal sealed class BrowserForm : Form, IMessageFilter
{
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;

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
    private readonly MeshInspector _meshInspector = new();
    private readonly SkinChooserView _meshSkins = new(showParts: true);
    private readonly SkinChooserView _skeletonSkins = new(showParts: false);
    private readonly Panel _poseHost = new();
    private readonly Panel _animationLargeHost = new();
    private AnimationLargeView? _animationLargeView;
    private string? _animationLargeKey;
    private readonly SkinPartView _skinView = new();
    private readonly SkeletonView _skeletonView = new();
    private readonly AudioView _audioView = new();
    private ThumbnailCache _thumbnailCache = null!;
    private MeshThumbnailCache _meshThumbnails = null!;
    private IReadOnlyList<TextureOccurrence> _inspectorCopies = Array.Empty<TextureOccurrence>();
    private readonly Label _resultsTitle = new();
    private readonly Label _resultsCount = new();
    private readonly Label _resultsBody = new();
    private readonly Label _crumbs = new();
    private readonly Button _back = new();
    private readonly Button _forward = new();
    private readonly Button _scopeButton = new();
    private readonly Button _filterButton = new();
    private readonly TableLayoutPanel _textureToolbar = new();
    private readonly Label _sortLabel = new();
    private readonly ComboBox _sortCombo = new();
    private readonly Button _sortDirection = new();
    private readonly Button _exportSelected = new();
    private readonly Button _cancelExport = new();
    private readonly Button _copyHash = new();
    private CancellationTokenSource? _exportCancel;
    private bool _exporting;
    private readonly Button _openSkin = new();
    private readonly Button _openSkeleton = new();
    private readonly Button _openMaterial = new();
    private readonly Label _selectionCount = new();
    private Control _titleLine = new();
    private bool _suppressSort;
    private int _chooserArchiveCount;
    private bool _meshSkinsOpen;
    private bool _skeletonSkinsOpen;
    private PoseView? _poseView;
    private string? _poseKey;
    private SkinLinkIndex? _skinLinks;
    private Task? _skinLinksTask;
    private CancellationTokenSource? _skinLinksCancel;
    private SkinSkeletonIndex? _skinSkeletons;
    private Task? _skinSkeletonsTask;
    private CancellationTokenSource? _skinSkeletonsCancel;
    private SkeletonCatalog? _skeletonCatalog;
    private Task? _skeletonTask;
    private CancellationTokenSource? _skeletonCancel;
    private CancellationTokenSource? _meshCancel;
    private CancellationTokenSource? _skinCancel;
    private CancellationTokenSource? _skeletonDecodeCancel;
    private string? _skinLoadedKey;
    private string? _skeletonLoadedKey;
    private IReadOnlyDictionary<ulong, AssetEntry> _meshRows =
        new Dictionary<ulong, AssetEntry>();
    private IReadOnlyDictionary<string, int> _meshCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, int> _skinCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<AudioEntry> _audio = Array.Empty<AudioEntry>();
    private bool _audioLoaded;
    private bool _audioLoading;
    private CancellationTokenSource? _audioCancel;
    private CancellationTokenSource? _audioListCancel;
    private int _audioListVersion;
    private bool _audioListBuilding;
    private bool _audioBusy;
    private int _audioGeneration;
    private byte[]? _audioOgg;
    private string? _audioOggKey;
    private readonly TextBox _search = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 150 };
    private readonly GameProfile? _initialProfile;
    private readonly bool _autoLoad;
    private readonly bool _saveSettings;
    private static readonly AssetColumn[] TextureColumns =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
        new("Size", 92, Right: true, Muted: true),
        new("Mips", 48, Right: true, Muted: true),
        new("Format", 56, Muted: true),
        new("Storage", 64, Muted: true),
    ];
    private static readonly AssetColumn[] NarrowTextureColumns =
    [
        new("Hash", 150, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];
    private static readonly AssetColumn[] AnimationColumns =
    [
        new("Hash", 140, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
        new("Occurrences", 78, Right: true, Muted: true),
    ];
    private static readonly AssetColumn[] MeshColumns =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];
    private static readonly AssetColumn[] SkinColumns =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];
    private static readonly AssetColumn[] SkeletonColumns =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];
    private static readonly AssetColumn[] MaterialColumns =
    [
        new("Hash", 160, Mono: true),
        new("Code", 190),
        new("Slots", 52, Right: true, Muted: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];
    private static readonly AssetColumn[] AudioColumns =
    [
        new("Pack", 140, Muted: true, MiddleEllipsis: true),
        new("Id", 92, Mono: true),
        new("Name", 0, Flexible: true, MiddleEllipsis: true),
        new("Language", 90, Muted: true),
        new("Length", 72, Right: true, Muted: true),
        new("Ch", 42, Right: true, Muted: true),
        new("Rate", 64, Right: true, Muted: true),
        new("Bank", 76, Right: true, Muted: true),
    ];
    private static readonly AssetColumn[] NarrowAudioColumns =
    [
        new("Pack", 92, Muted: true, MiddleEllipsis: true),
        new("Id", 82, Mono: true),
        new("Name", 0, Flexible: true, MiddleEllipsis: true),
        new("Language", 64, Muted: true),
    ];
    private CancellationTokenSource? _loadCancel;
    private CancellationTokenSource? _allRowsCancel;
    private int _allRowsVersion;
    private bool _allRowsBuilding;
    private BrowserView _allRowsView;
    private string _allRowsQuery = "";
    private IReadOnlyList<AssetRow>? _allRowsCache;
    private BrowserView _allRowsCacheView;
    private string _allRowsCacheQuery = "";
    private CancellationTokenSource? _listCancel;
    private int _listVersion;
    private bool _listBuilding;
    private BrowserNavigation? _navigation;
    private IReadOnlyList<AssetEntry> _assets = Array.Empty<AssetEntry>();
    private IReadOnlyList<ArchiveChoice> _archives = Array.Empty<ArchiveChoice>();
    private int _totalTextureCount;
    private string _lastArchive = "";
    private string _typesArchive = "";
    private IReadOnlyDictionary<string, int> _animationCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, int> _textureCounts =
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
    private readonly MaterialSummaryCache _materialCache = new();
    private CancellationTokenSource? _materialListCancel;
    private CancellationTokenSource? _materialCatalogCancel;
    private Task? _materialCatalogTask;
    private int _materialListVersion;
    private bool _materialListBuilding;
    private string _materialListArchive = "";
    private Stopwatch? _materialEntryWatch;
    private Stopwatch? _materialFilterWatch;
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
        WireMeshViews();
        WireAudio();
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

            if (_navigation.State.View == BrowserView.Audio)
            {
                AudioSort sort = _sortCombo.SelectedIndex switch
                {
                    1 => AudioSort.Id,
                    2 => AudioSort.Name,
                    3 => AudioSort.Length,
                    4 => AudioSort.Channels,
                    5 => AudioSort.Rate,
                    6 => AudioSort.Language,
                    7 => AudioSort.Bank,
                    _ => AudioSort.Pack,
                };
                _navigation.SetAudioSort(sort, _navigation.State.AudioSortDescending);
            }
            else
            {
                TextureSort sort = _sortCombo.SelectedIndex switch
                {
                    1 => TextureSort.Hash,
                    2 => TextureSort.Size,
                    3 => TextureSort.Mips,
                    _ => TextureSort.Archive,
                };
                _navigation.SetSort(sort, _navigation.State.SortDescending);
            }

            RenderState();
        };
        _sortDirection.Click += (_, _) =>
        {
            if (_navigation is null)
            {
                return;
            }

            if (_navigation.State.View == BrowserView.Audio)
            {
                _navigation.SetAudioSort(
                    _navigation.State.AudioSort,
                    !_navigation.State.AudioSortDescending);
            }
            else
            {
                _navigation.SetSort(
                    _navigation.State.Sort,
                    !_navigation.State.SortDescending);
            }

            RenderState();
        };
        _exportSelected.Click += async (_, _) => await ExportSelectedAsync();
        _cancelExport.Click += (_, _) => _exportCancel?.Cancel();
        _copyHash.Click += (_, _) => CopySelectedHash();
        KeyDown += OnKeyDown;
        Application.AddMessageFilter(this);
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ApplySearch();
        };
        Resize += (_, _) => OnResize();
        _inner.Panel1.Resize += (_, _) => UpdateToolbarLayout();
        Shown += async (_, _) => await OnShownAsync();
        FormClosing += (_, _) =>
        {
            _loadCancel?.Cancel();
            _listCancel?.Cancel();
            _allRowsCancel?.Cancel();
            _materialCancel?.Cancel();
            _materialCatalogCancel?.Cancel();
            _materialListCancel?.Cancel();
            _skinLinksCancel?.Cancel();
            _skinSkeletonsCancel?.Cancel();
            _skeletonCancel?.Cancel();
            _meshCancel?.Cancel();
            _skinCancel?.Cancel();
            _skeletonDecodeCancel?.Cancel();
            _audioCancel?.Cancel();
            _audioListCancel?.Cancel();
            _exportCancel?.Cancel();
            SaveLayout();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(this);
            _thumbnailCache?.Dispose();
            _meshThumbnails?.Dispose();
            _loadCancel?.Dispose();
            _allRowsCancel?.Dispose();
            _materialCancel?.Dispose();
            _materialCatalogCancel?.Dispose();
            _materialListCancel?.Dispose();
            _exportCancel?.Dispose();
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
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel brand = new()
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 12, 0),
            Anchor = AnchorStyles.Left,
        };
        brand.Controls.Add(new PictureBox
        {
            Image = Theme.AppMark,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(28, 27),
            Margin = new Padding(0, 0, 4, 0),
        });
        brand.Controls.Add(new Label
        {
            Text = "APTStudio",
            AutoSize = true,
            Font = BrowserVisual.Brand,
            ForeColor = Theme.Text,
            Margin = new Padding(0, 5, 0, 0),
        });

        FlowLayoutPanel nav = new()
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 8, 0),
            Anchor = AnchorStyles.Left,
        };
        BrowserButtons.Style(_back, BrowserButtonRole.Ordinary, Theme.Surface);
        BrowserButtons.Style(_forward, BrowserButtonRole.Ordinary, Theme.Surface);
        _back.Text = "←";
        _forward.Text = "→";
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

        BrowserButtons.Style(_scopeButton, BrowserButtonRole.Ordinary, Theme.Surface);
        _scopeButton.Text = "Scope";
        _scopeButton.AutoSize = false;
        _scopeButton.Width = 78;
        _scopeButton.Height = 26;
        _scopeButton.AutoEllipsis = true;
        _scopeButton.Margin = new Padding(0, 0, 10, 0);
        _scopeButton.AccessibleName = "Toggle scope pane";

        _crumbs.Dock = DockStyle.Fill;
        _crumbs.Font = BrowserVisual.Row;
        _crumbs.TextAlign = ContentAlignment.MiddleLeft;
        _crumbs.ForeColor = BrowserVisual.Crumb;
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

        BrowserButtons.Style(_filterButton, BrowserButtonRole.Ordinary, Theme.Surface);
        _filterButton.Text = "Filter";
        _filterButton.AutoSize = false;
        _filterButton.Width = 64;
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
            Height = 28,
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

        StyleQuietButton(_openSkin, "Open skin");
        _openSkin.Visible = false;
        _openSkin.Enabled = false;
        _openSkin.Click += (_, _) => OpenSkinFromList();
        titleLine.Controls.Add(_openSkin);

        StyleQuietButton(_openSkeleton, "Open skeleton");
        _openSkeleton.Visible = false;
        _openSkeleton.Enabled = false;
        _openSkeleton.Click += (_, _) => OpenSkeletonFromList();
        titleLine.Controls.Add(_openSkeleton);

        StyleQuietButton(_openMaterial, "Open material");
        _openMaterial.Visible = false;
        _openMaterial.Enabled = false;
        _openMaterial.Click += (_, _) => OpenMaterialFromList();
        titleLine.Controls.Add(_openMaterial);

        StyleQuietButton(_copyHash, "Copy hash");
        _copyHash.Visible = false;
        _copyHash.Enabled = false;
        titleLine.Controls.Add(_copyHash);

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
        _sortLabel.Text = "Sort";
        _sortLabel.ForeColor = Theme.Muted;
        _sortLabel.Font = BrowserVisual.RowSmall;
        _sortLabel.AutoSize = true;
        _sortLabel.Anchor = AnchorStyles.Left;
        _sortLabel.Margin = new Padding(0, 0, 5, 0);
        _sortLabel.TextAlign = ContentAlignment.MiddleLeft;
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
        BrowserButtons.Style(_sortDirection, BrowserButtonRole.Ordinary, Theme.Background);
        _sortDirection.Text = "Asc";
        _sortDirection.AutoSize = false;
        _sortDirection.Width = 60;
        _sortDirection.Height = 26;
        _sortDirection.Anchor = AnchorStyles.Left;
        _sortDirection.Margin = new Padding(0, 0, 10, 0);
        BrowserButtons.Style(_exportSelected, BrowserButtonRole.Ordinary, Theme.Background);
        _exportSelected.Text = "Export selected";
        _exportSelected.AutoSize = true;
        _exportSelected.Height = 26;
        _exportSelected.Anchor = AnchorStyles.Left;
        _exportSelected.Margin = new Padding(0, 0, 8, 0);
        _exportSelected.Enabled = false;
        BrowserButtons.Style(_cancelExport, BrowserButtonRole.Ordinary, Theme.Background);
        _cancelExport.Text = "Cancel";
        _cancelExport.AutoSize = true;
        _cancelExport.Height = 26;
        _cancelExport.Anchor = AnchorStyles.Left;
        _cancelExport.Margin = new Padding(0);
        _cancelExport.Visible = false;
        _selectionCount.Text = "0 selected";
        _selectionCount.ForeColor = Theme.Muted;
        _selectionCount.Font = BrowserVisual.RowSmall;
        _selectionCount.AutoSize = true;
        _selectionCount.Anchor = AnchorStyles.Right;
        _selectionCount.Margin = new Padding(0);
        _selectionCount.TextAlign = ContentAlignment.MiddleRight;
        _textureToolbar.Controls.Add(_sortLabel, 0, 0);
        _textureToolbar.Controls.Add(_sortCombo, 1, 0);
        _textureToolbar.Controls.Add(_sortDirection, 2, 0);
        _textureToolbar.Controls.Add(_exportSelected, 3, 0);
        _textureToolbar.Controls.Add(_cancelExport, 4, 0);
        _textureToolbar.Controls.Add(_selectionCount, 5, 0);

        _textureHost.Controls.Add(_textureList);
        _textureHost.Controls.Add(_textureHeader);

        _chooser.Dock = DockStyle.Fill;
        _chooser.Visible = false;

        _materialSlots.Dock = DockStyle.Fill;
        _materialSlots.Visible = false;

        _meshSkins.Dock = DockStyle.Fill;
        _meshSkins.Visible = false;
        _skeletonSkins.Dock = DockStyle.Fill;
        _skeletonSkins.Visible = false;
        _poseHost.Dock = DockStyle.Fill;
        _poseHost.Visible = false;
        _animationLargeHost.Dock = DockStyle.Fill;
        _animationLargeHost.Visible = false;
        _skinView.Dock = DockStyle.Fill;
        _skinView.Visible = false;
        _skeletonView.Dock = DockStyle.Fill;
        _skeletonView.Visible = false;

        _titleLine = titleLine;
        _results.Controls.Add(_chooser);
        _results.Controls.Add(_materialSlots);
        _results.Controls.Add(_meshSkins);
        _results.Controls.Add(_skeletonSkins);
        _results.Controls.Add(_poseHost);
        _results.Controls.Add(_animationLargeHost);
        _results.Controls.Add(_skinView);
        _results.Controls.Add(_skeletonView);
        _results.Controls.Add(_textureHost);
        _results.Controls.Add(_textureToolbar);
        _results.Controls.Add(_resultsBody);
        _results.Controls.Add(titleLine);
        _textureToolbar.PerformLayout();

        _inspector.Dock = DockStyle.Fill;
        _inspector.Padding = new Padding(14, 13, 14, 10);
        _inspector.BackColor = Theme.Surface;
        _textureInspector.Dock = DockStyle.Fill;
        _textureInspector.Visible = false;
        _slotInspector.Dock = DockStyle.Fill;
        _slotInspector.Visible = false;
        _animationInspector.Dock = DockStyle.Fill;
        _animationInspector.Visible = false;
        _meshInspector.Dock = DockStyle.Fill;
        _meshInspector.Visible = false;
        _audioView.Dock = DockStyle.Fill;
        _audioView.Visible = false;
        _inspector.Controls.Add(_textureInspector);
        _inspector.Controls.Add(_slotInspector);
        _inspector.Controls.Add(_animationInspector);
        _inspector.Controls.Add(_meshInspector);
        _inspector.Controls.Add(_audioView);
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

    private static void StyleQuietButton(Button button, string text)
    {
        BrowserButtons.Style(button, BrowserButtonRole.Quiet, Theme.Background);
        button.Text = text;
        button.AutoSize = true;
        button.Height = 26;
        button.Margin = new Padding(8, 0, 0, 0);
    }

    private void WireScope()
    {
        _scope.OpenGameRequested += (_, _) => OpenGameFolder();
        _scope.RescanRequested += (_, _) => Rescan();
        _scope.ArchiveSelected += path => SwitchArchive(path);
        _scope.TypeSelected += name => SwitchType(name);
    }

    private void WireTextureList()
    {
        _thumbnailCache = new ThumbnailCache();
        _meshThumbnails = new MeshThumbnailCache();
        _textureList.ThumbnailProvider = row =>
        {
            if (row.Tag is not AssetEntry entry)
            {
                return _thumbnailCache.Placeholder;
            }

            if (entry.ClassHash == AssetClass.Mesh)
            {
                return _meshThumbnails.GetCached(entry);
            }

            return entry.IsPreviewable && entry.Texture is { } texture
                ? _thumbnailCache.GetCached(texture)
                : _thumbnailCache.Placeholder;
        };
        _textureList.ThumbnailRequested += row =>
        {
            if (row.Tag is AssetEntry entry
                && entry.ClassHash != AssetClass.Mesh
                && entry.IsPreviewable
                && entry.Texture is { } texture)
            {
                _thumbnailCache.Request(texture);
            }
        };
        _textureList.VisibleRowsChanged += rows =>
        {
            if (_navigation?.State.View != BrowserView.Meshes)
            {
                return;
            }

            List<AssetEntry> entries = new(rows.Count);
            foreach (AssetRow row in rows)
            {
                if (row.Tag is AssetEntry { ClassHash: AssetClass.Mesh } entry)
                {
                    entries.Add(entry);
                }
            }

            _meshThumbnails.SetVisible(entries);
        };
        _thumbnailCache.ThumbnailReady += (_, _) => _textureList.Invalidate();
        _meshThumbnails.ThumbnailReady += (_, _) => _textureList.Invalidate();
        _textureList.OpenRequested += (_, _) =>
        {
            if (_navigation?.State.View == BrowserView.Skins)
            {
                OpenSkinFromList();
            }
            else if (_navigation?.State.View == BrowserView.Skeletons)
            {
                OpenSkeletonFromList();
            }
            else if (_navigation?.State.View == BrowserView.Materials)
            {
                OpenMaterialFromList();
            }
        };
        _textureList.SelectionChanged += (_, _) =>
        {
            OnListSelected();
            UpdateCopyHashEnabled();
        };
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
        else if (_navigation.State.View == BrowserView.Meshes)
        {
            OnMeshSelected();
        }
        else if (_navigation.State.View == BrowserView.Skins)
        {
            if (_textureList.SelectedRow?.Tag is AssetEntry entry
                && entry.ClassHash == AssetClass.Skin)
            {
                _navigation.SetSkinListSelection(
                    ToSkin(entry), _textureList.TopIndex);
                _navigation.SetListScroll(_textureList.ScrollOffset);
            }

            UpdateOpenSkinEnabled();
            return;
        }
        else if (_navigation.State.View == BrowserView.Skeletons)
        {
            if (_textureList.SelectedRow?.Tag is SkeletonEntry skeleton)
            {
                _navigation.SetSkeletonListSelection(
                    ToSkeleton(skeleton), _textureList.TopIndex);
                _navigation.SetListScroll(_textureList.ScrollOffset);
            }

            UpdateOpenSkeletonEnabled();
            return;
        }
        else if (_navigation.State.View == BrowserView.Materials)
        {
            if (_textureList.SelectedRow?.Tag is MaterialRow material)
            {
                _navigation.SetMaterialListSelection(
                    ToMaterial(material), _textureList.TopIndex, _textureList.ScrollOffset);
            }

            UpdateOpenMaterialEnabled();
            return;
        }
        else if (_navigation.State.View == BrowserView.Audio)
        {
            OnAudioSelected();
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
        _animationInspector.LargeRequested += (_, large) => OpenAnimationLarge(large);
        _materialSlots.SelectionChanged += (_, _) => OnMaterialSlotSelected();
        _materialSlots.OpenTextureRequested += (_, slot) =>
        {
            _navigation?.SelectSlot(slot);
            OpenSlotTexture();
        };
        _slotInspector.OpenTextureRequested += (_, _) => OpenSlotTexture();
        _slotInspector.CopyChanged += (_, index) => OnSlotCopyChanged(index);
    }

    private void WireMeshViews()
    {
        _meshInspector.UsedByRequested += async (_, _) => await OpenMeshSkinsAsync();
        _meshInspector.CameraChanged += (_, _) => UpdateMeshViewState();
        _meshInspector.WireframeChanged += (_, _) => UpdateMeshViewState();
        _meshSkins.OpenRequested += (_, row) => OpenSkin((SkinLinkEntry)row.Tag);
        _skeletonSkins.OpenRequested += (_, row) => OpenSkinFromSkeleton((SkinSkeletonUse)row.Tag);
        _skeletonView.UsedByRequested += (_, _) => ShowSkeletonSkins();
        _skinView.SkeletonRequested += async root => await OpenSkeletonAsync(root);
        _skinView.MeshRequested += hash => OpenMeshFromSkin(hash);
        _skinView.PoseRequested += root => OpenPoseFromSkin(root);
        _skinView.PartChanged += part =>
        {
            if (_navigation?.State.View == BrowserView.Skin)
            {
                _navigation.SetSkinPart(part);
            }
        };
    }

    private void WireAudio()
    {
        _audioView.PlayRequested += () => _ = PlayAudioAsync();
        _audioView.ExportRequested += () => _ = ExportAudioAsync();
    }

    private void UpdateMeshViewState()
    {
        if (_navigation?.State.View == BrowserView.Meshes)
        {
            _navigation.SetMeshView(_meshInspector.Camera, _meshInspector.IsWireframe);
        }
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
        if (!CanChangeGame())
        {
            return;
        }

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

    private void Rescan()
    {
        if (_profile is not null && CanChangeGame())
        {
            _ = LoadGameAsync(_profile.Root);
        }
    }

    private bool CanChangeGame()
    {
        if (_loading)
        {
            _status.Text = "Wait for the current game to finish loading.";
            return false;
        }

        if (_exporting)
        {
            _status.Text = "Cancel the texture export before changing games.";
            return false;
        }

        if (_audioBusy)
        {
            _status.Text = "Wait for audio to finish before changing games.";
            return false;
        }

        return true;
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg is not (WmXButtonDown or WmXButtonUp))
        {
            return false;
        }

        Control? target = Control.FromHandle(message.HWnd);
        if (target is null || target.FindForm() != this)
        {
            return false;
        }

        int button = (int)((message.WParam.ToInt64() >> 16) & 0xFFFF);
        BrowserSideButton action = BrowserShortcuts.MapSideButton(button);
        if (action == BrowserSideButton.None)
        {
            return false;
        }

        if (message.Msg == WmXButtonDown)
        {
            if (action == BrowserSideButton.Back)
            {
                Back();
            }
            else
            {
                Forward();
            }
        }

        return true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        BrowserCommand command = BrowserShortcuts.Map(e.KeyCode, e.Control);
        if (command == BrowserCommand.None)
        {
            return;
        }

        if (command == BrowserCommand.CancelExport)
        {
            if (!_exporting)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            _exportCancel?.Cancel();
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        switch (command)
        {
            case BrowserCommand.OpenGame:
                OpenGameFolder();
                break;
            case BrowserCommand.Export:
                if (!_loading && !_exporting && !_audioBusy)
                {
                    _ = ExportSelectedAsync();
                }

                break;
            case BrowserCommand.Rescan:
                Rescan();
                break;
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

        if (!CanChangeGame())
        {
            return;
        }

        _loadCancel?.Cancel();
        _loadCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _loadCancel = cancel;
        _loading = true;
        _status.Text = $"Scanning {Path.GetFileName(profile.Root)}...";
        Stopwatch watch = Stopwatch.StartNew();
        IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
        {
            if (!IsDisposed && cancel == _loadCancel)
            {
                _status.Text =
                    $"Scanning {Path.GetFileName(item.ArchivePath)} {item.Index}/{item.Total}";
            }
        });

        bool committed = false;
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
            ApplyLoadedGame(profile, catalog);
            committed = true;
            BrowserTiming.Write(
                $"catalog {watch.ElapsedMilliseconds}ms {catalog.Archives.Count} archives " +
                $"{catalog.Entries.Count} rows");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidDataException)
        {
            if (!IsDisposed && cancel == _loadCancel)
            {
                _status.Text =
                    $"Could not open {Path.GetFileName(profile.Root)}: {ex.Message}";
            }
        }
        finally
        {
            if (cancel == _loadCancel)
            {
                _loading = false;
                if (committed && !_listBuilding)
                {
                    UpdateStatus();
                }
            }
        }
    }

    private void ApplyLoadedGame(GameProfile profile, AssetCatalog catalog)
    {
        _materialCancel?.Cancel();
        _materialCancel?.Dispose();
        _materialCancel = null;
        _listCancel?.Cancel();
        _listCancel = null;
        _listVersion++;
        _listBuilding = false;
        _allRowsCancel?.Cancel();
        _allRowsCancel?.Dispose();
        _allRowsCancel = null;
        _allRowsVersion++;
        _allRowsBuilding = false;
        _allRowsCache = null;
        _allRowsCacheQuery = "";
        _skinLinksCancel?.Cancel();
        _skinLinksCancel = null;
        _skinLinks = null;
        _skinLinksTask = null;
        _skinSkeletonsCancel?.Cancel();
        _skinSkeletonsCancel = null;
        _skinSkeletons = null;
        _skinSkeletonsTask = null;
        _skeletonSkinsOpen = false;
        _skeletonSkins.SetRows([], "No skin is explicitly bound to this skeleton.");
        ClearPoseView();
        ClearAnimationLargeView();
        _animationInspector.ResetForGame();
        _skeletonCancel?.Cancel();
        _skeletonCancel = null;
        _skeletonCatalog = null;
        _skeletonTask = null;
        _meshCancel?.Cancel();
        _meshCancel = null;
        _skinCancel?.Cancel();
        _skinCancel = null;
        _skeletonDecodeCancel?.Cancel();
        _skeletonDecodeCancel = null;
        _skinLoadedKey = null;
        _skeletonLoadedKey = null;
        _meshSkinsOpen = false;
        _meshInspector.ShowEmpty();
        _skinView.Reset();
        _skeletonView.Reset();
        _meshSkins.SetRows([], "No skin uses this mesh.");
        _audioCancel?.Cancel();
        _audioCancel = null;
        _audioGeneration++;
        _audio = Array.Empty<AudioEntry>();
        _audioLoaded = false;
        _audioLoading = false;
        _audioListCancel?.Cancel();
        _audioListCancel = null;
        _audioListVersion++;
        _audioListBuilding = false;
        _audioBusy = false;
        _audioOgg = null;
        _audioOggKey = null;
        _audioView.Reset();
        _materials = null;
        _materialTextures = null;
        _materialLoading = false;
        _materialBodies.Clear();
        _materialListCancel?.Cancel();
        _materialListCancel?.Dispose();
        _materialListCancel = null;
        _materialListVersion++;
        _materialListBuilding = false;
        _materialListArchive = "";
        _materialEntryWatch = null;
        _materialFilterWatch = null;
        _materialCatalogCancel?.Cancel();
        _materialCatalogCancel?.Dispose();
        _materialCatalogCancel = null;
        _materialCatalogTask = null;
        _materialCache.Reset();
        _slotCopies = Array.Empty<TextureOccurrence>();
        _slotCopyHash = 0;
        _slotCopyArchive = "";
        _slotCopyIndex = 0;
        _chooser.Visible = false;
        _textureInspector.ShowEmpty();
        _profile = profile;
        Text = $"APTStudio - {profile.Kind}";
        _scope.SetGame(profile.Kind.ToString(), "read-only");
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

        _totalTextureCount = textureCounts.Values.Sum();
        _textureCounts = textureCounts;
        ArchiveChoice[] archiveRows = catalog.Archives
            .Select(archive => new ArchiveChoice(
                archive.Path,
                textureCounts.GetValueOrDefault(archive.Path)))
            .OrderBy(archive => archive.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _archives =
        [
            new ArchiveChoice("", _totalTextureCount),
            .. archiveRows,
        ];
        _animationCounts = BuildAnimationCounts(_assets);
        _animationOccurrences = _assets
            .Where(entry => entry.ClassHash == AssetClass.Animation)
            .GroupBy(entry => entry.NameHash)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<string, HashSet<(ulong Name, long Offset)>> meshKeys =
            new(StringComparer.OrdinalIgnoreCase);
        Dictionary<ulong, AssetEntry> meshRows = new();
        foreach (AssetEntry entry in _assets)
        {
            if (entry.ClassHash != AssetClass.Mesh)
            {
                continue;
            }

            meshRows.TryAdd(entry.NameHash, entry);
            if (!meshKeys.TryGetValue(entry.ArchivePath, out HashSet<(ulong, long)>? keys))
            {
                keys = new HashSet<(ulong, long)>();
                meshKeys[entry.ArchivePath] = keys;
            }

            keys.Add((entry.NameHash, entry.Row.BodyOffset));
        }

        _meshRows = meshRows;
        _meshCounts = meshKeys.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Count,
            StringComparer.OrdinalIgnoreCase);

        Dictionary<string, HashSet<string>> skinKeys =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (AssetEntry entry in _assets)
        {
            if (entry.ClassHash != AssetClass.Skin)
            {
                continue;
            }

            if (!skinKeys.TryGetValue(entry.ArchivePath, out HashSet<string>? keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                skinKeys[entry.ArchivePath] = keys;
            }

            keys.Add(BrowserKeys.Skin(entry));
        }

        _skinCounts = skinKeys.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Count,
            StringComparer.OrdinalIgnoreCase);

        string selected = ChooseInitialArchive();
        _lastArchive = selected;
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
        SetTypesFor(selected, "Textures");
        _search.Enabled = true;
        _filterButton.Enabled = true;
        RenderState();
        _status.Text = $"{ScopeName(selected)} · loading textures...";
        if (_saveSettings)
        {
            Settings.SaveLastGameRoot(profile.Root);
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

        return _archives.FirstOrDefault(archive => archive.Count > 0 && archive.Path.Length > 0)?.Path
            ?? _archives.FirstOrDefault(archive => archive.Path.Length > 0)?.Path
            ?? "";
    }

    private int TextureCount(string archivePath) =>
        archivePath.Length == 0 ? _totalTextureCount : _textureCounts.GetValueOrDefault(archivePath);

    private IReadOnlyList<TypeChoice> BuildTypes(string archivePath)
    {
        bool allArchives = archivePath.Length == 0;
        int textures = TextureCount(archivePath);
        int animations = allArchives
            ? _animationCounts.Values.Sum()
            : _animationCounts.GetValueOrDefault(archivePath);
        int meshes = allArchives
            ? _meshCounts.Values.Sum()
            : _meshCounts.GetValueOrDefault(archivePath);
        int skins = allArchives
            ? _skinCounts.Values.Sum()
            : _skinCounts.GetValueOrDefault(archivePath);
        int skeletons = allArchives
            ? SkeletonCount("")
            : SkeletonCount(archivePath);
        int materials = MaterialCount(archivePath);
        int audio = _audioLoaded ? _audio.Count : 0;
        return
        [
            new TypeChoice("Textures", textures),
            new TypeChoice("Animations", animations),
            new TypeChoice("Materials", materials),
            new TypeChoice("Meshes", meshes),
            new TypeChoice("Skins", skins),
            new TypeChoice("Skeletons", skeletons),
            new TypeChoice("Audio", audio),
        ];
    }

    private static IReadOnlyDictionary<string, int> BuildAnimationCounts(
        IReadOnlyList<AssetEntry> assets)
    {
        Dictionary<string, HashSet<string>> keys =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (AssetEntry entry in assets)
        {
            if (entry.ClassHash != AssetClass.Animation)
            {
                continue;
            }

            if (!keys.TryGetValue(entry.ArchivePath, out HashSet<string>? archiveKeys))
            {
                archiveKeys = new HashSet<string>(StringComparer.Ordinal);
                keys[entry.ArchivePath] = archiveKeys;
            }

            archiveKeys.Add($"{entry.Row.BodyOffset}|{entry.NameHash:X16}");
        }

        return keys.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Count,
            StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, int> BuildArchiveCounts(string active)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
        switch (active)
        {
            case "Animations":
                foreach ((string path, int count) in _animationCounts)
                {
                    counts[path] = count;
                }

                counts[""] = _animationCounts.Values.Sum();
                break;
            case "Meshes":
                foreach ((string path, int count) in _meshCounts)
                {
                    counts[path] = count;
                }

                counts[""] = _meshCounts.Values.Sum();
                break;
            case "Skins":
                foreach ((string path, int count) in _skinCounts)
                {
                    counts[path] = count;
                }

                counts[""] = _skinCounts.Values.Sum();
                break;
            case "Skeletons":
                if (_skeletonCatalog is not null)
                {
                    Dictionary<string, HashSet<string>> keys =
                        new(StringComparer.OrdinalIgnoreCase);
                    foreach (SkeletonEntry entry in _skeletonCatalog.Entries)
                    {
                        if (!keys.TryGetValue(
                            entry.ArchivePath, out HashSet<string>? archiveKeys))
                        {
                            archiveKeys = new HashSet<string>(StringComparer.Ordinal);
                            keys[entry.ArchivePath] = archiveKeys;
                        }

                        archiveKeys.Add($"{entry.Offset}|{entry.NameHash:X16}");
                    }

                    foreach ((string path, HashSet<string> value) in keys)
                    {
                        counts[path] = value.Count;
                    }

                    counts[""] = counts.Values.Sum();
                }
                else
                {
                    counts[""] = 0;
                }

                break;
            case "Materials":
                if (_materials is not null)
                {
                    foreach (MaterialArchive archive in _materials.Archives)
                    {
                        counts[archive.Path] = archive.Count;
                    }

                    counts[""] = _materials.Entries.Count;
                }
                else
                {
                    counts[""] = 0;
                }

                break;
            case "Audio":
                counts[""] = _audioLoaded ? _audio.Count : 0;
                break;
            default:
                foreach ((string path, int count) in _textureCounts)
                {
                    counts[path] = count;
                }

                counts[""] = _totalTextureCount;
                break;
        }

        return counts;
    }

    private int ActiveCount(string archivePath)
    {
        string active = _navigation is null ? "Textures" : TypeName(_navigation.State);
        return BuildArchiveCounts(active).GetValueOrDefault(archivePath);
    }

    private int MaterialCount(string archivePath)
    {
        if (_materials is null)
        {
            return 0;
        }

        if (archivePath.Length == 0)
        {
            return _materials.Entries.Count;
        }

        return _materials.Archives.FirstOrDefault(archive =>
            archive.Path.Equals(archivePath, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
    }

    private bool HasMaterialRows(string archivePath) =>
        _materialCache.GetRows(archivePath) is not null;

    private int SkeletonCount(string archivePath)
    {
        if (_skeletonCatalog is null)
        {
            return 0;
        }

        if (archivePath.Length == 0)
        {
            return _skeletonCatalog.Entries
                .Select(BrowserKeys.Skeleton)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        return _skeletonCatalog.Entries
            .Where(entry => entry.ArchivePath.Equals(
                archivePath, StringComparison.OrdinalIgnoreCase))
            .Select(BrowserKeys.Skeleton)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private void SetTypesFor(string archivePath, string active)
    {
        _scope.SetTypes(BuildTypes(archivePath), active);
        UpdateArchiveCounts(active);
        _typesArchive = archivePath;
    }

    private void UpdateArchiveCounts(string active)
    {
        Dictionary<string, int> counts = BuildArchiveCounts(active);
        _scope.SetArchiveCounts(
            counts, active == "Audio" ? "SOUNDBANKS" : "All archives");
        if (active == "Audio")
        {
            _scope.SelectArchive("");
        }
    }

    private void SwitchArchive(string archivePath)
    {
        if (_navigation is null
            || _navigation.State.ArchivePath.Equals(archivePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        BrowserState state = _navigation.State;
        _meshSkinsOpen = false;
        _skeletonSkinsOpen = false;
        BrowserView view = state.View switch
        {
            BrowserView.Animations => BrowserView.Animations,
            BrowserView.AnimationLarge => BrowserView.Animations,
            BrowserView.Skins => BrowserView.Skins,
            BrowserView.Skeletons => BrowserView.Skeletons,
            BrowserView.Materials => BrowserView.Materials,
            BrowserView.Meshes => BrowserView.Meshes,
            BrowserView.Skin => state.Mesh is null
                ? BrowserView.Skins
                : BrowserView.Meshes,
            BrowserView.SkinPose => state.Mesh is null
                ? BrowserView.Skins
                : BrowserView.Meshes,
            BrowserView.Skeleton => state.Mesh is not null
                ? BrowserView.Meshes
                : state.Skin is not null
                    ? BrowserView.Skins
                    : BrowserView.Skeletons,
            BrowserView.Material => state.MaterialFromList
                ? BrowserView.Materials
                : BrowserView.Textures,
            BrowserView.LinkedTexture => state.MaterialFromList
                ? BrowserView.Materials
                : BrowserView.Textures,
            _ => BrowserView.Textures,
        };

        if (archivePath.Length > 0)
        {
            _lastArchive = archivePath;
        }

        if (view == BrowserView.Materials
            && !HasMaterialRows(archivePath))
        {
            _materialEntryWatch = Stopwatch.StartNew();
            BrowserTiming.Write($"materials-first start archive='{archivePath}'");
        }
        else if (view != BrowserView.Materials)
        {
            _materialEntryWatch = null;
        }

        _navigation.SwitchScope(archivePath, view);
        SetTypesFor(archivePath, TypeName(_navigation.State));
        RenderState();
    }

    private void SwitchType(string name)
    {
        if (_navigation is null)
        {
            return;
        }

        _meshSkinsOpen = false;
        _skeletonSkinsOpen = false;
        BrowserView view = name switch
        {
            "Textures" => BrowserView.Textures,
            "Animations" => BrowserView.Animations,
            "Meshes" => BrowserView.Meshes,
            "Skins" => BrowserView.Skins,
            "Skeletons" => BrowserView.Skeletons,
            "Materials" => BrowserView.Materials,
            "Audio" => BrowserView.Audio,
            _ => BrowserView.Textures,
        };
        string archive = _navigation.State.ArchivePath;
        if (view == _navigation.State.View
            && archive.Equals(_navigation.State.ArchivePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (view == BrowserView.Materials
            && !HasMaterialRows(archive))
        {
            _materialEntryWatch = Stopwatch.StartNew();
            BrowserTiming.Write($"materials-first start archive='{archive}'");
        }
        else if (view != BrowserView.Materials)
        {
            _materialEntryWatch = null;
        }

        _navigation.SwitchScope(archive, view);
        SetTypesFor(archive, TypeName(_navigation.State));
        RenderState();
    }

    private void Back()
    {
        if (_meshSkinsOpen)
        {
            CloseMeshSkins();
            return;
        }

        if (_skeletonSkinsOpen)
        {
            CloseSkeletonSkins();
            return;
        }

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
        _navigation.SetListScroll(0);
        if (_navigation.State.View == BrowserView.Materials
            && HasMaterialRows(_navigation.State.ArchivePath))
        {
            _materialFilterWatch = Stopwatch.StartNew();
        }

        RenderState();
    }

    private void OnFilterRequested()
    {
        if (_navigation is null)
        {
            return;
        }

        if (_navigation.State.View == BrowserView.Audio)
        {
            ShowAudioFilterMenu(_navigation.State);
            return;
        }

        if (_navigation.State.View != BrowserView.Textures)
        {
            return;
        }

        BrowserState state = _navigation.State;
        bool allArchives = string.IsNullOrEmpty(state.ArchivePath);
        List<AssetEntry> textures = _assets
            .Where(entry => entry.IsBitmap
                && entry.Bitmap is not null
                && (allArchives
                    || entry.ArchivePath.Equals(
                        state.ArchivePath, StringComparison.OrdinalIgnoreCase)))
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
            _back.Enabled = false;
            _forward.Enabled = false;
            _scopeButton.Text = "Scope";
            _crumbs.Text = "";
            _textureToolbar.Visible = false;
            return;
        }

        BrowserState state = _navigation.State;
        if (state.View != BrowserView.SkinPose && _poseView is not null)
        {
            ClearPoseView();
        }

        if (state.View != BrowserView.AnimationLarge && _animationLargeView is not null)
        {
            ClearAnimationLargeView();
        }

        if (_allRowsBuilding
            && (state.ArchivePath.Length > 0
                || state.View is not (BrowserView.Animations or BrowserView.Meshes
                    or BrowserView.Skins or BrowserView.Skeletons)))
        {
            _allRowsCancel?.Cancel();
            _allRowsBuilding = false;
            _allRowsVersion++;
        }

        if (!string.IsNullOrEmpty(state.ArchivePath))
        {
            _lastArchive = state.ArchivePath;
        }

        if (state.View != BrowserView.Textures && _listBuilding)
        {
            _listCancel?.Cancel();
            _listBuilding = false;
            _listVersion++;
        }

        if (state.View != BrowserView.Audio && _audioListBuilding)
        {
            _audioListCancel?.Cancel();
            _audioListBuilding = false;
            _audioListVersion++;
        }

        if (state.View != BrowserView.Materials && _materialListBuilding)
        {
            _materialListCancel?.Cancel();
            _materialListBuilding = false;
            _materialListVersion++;
        }

        _scope.SelectArchive(state.ArchivePath);
        if (!state.ArchivePath.Equals(_typesArchive, StringComparison.OrdinalIgnoreCase))
        {
            SetTypesFor(state.ArchivePath, TypeName(state));
        }
        else
        {
            _scope.SetActiveType(TypeName(state));
            UpdateArchiveCounts(TypeName(state));
        }
        _scopeButton.Text = "Scope";
        _back.Enabled = _navigation.CanBack || _meshSkinsOpen || _skeletonSkinsOpen;
        _forward.Enabled = _navigation.CanForward;
        _crumbs.Text = _navigation.IsChooserOpen ? ChooserCrumbs(state) : Crumbs(state);
        _search.PlaceholderText = state.View switch
        {
            BrowserView.Audio => "pack, language, id",
            BrowserView.Materials => "hash, code, archive",
            BrowserView.Textures => "hash, dimensions",
            _ => "hash, archive",
        };
        if (_search.Text != state.Query)
        {
            _search.Text = state.Query;
        }

        bool textureView = state.View == BrowserView.Textures || state.View == BrowserView.LinkedTexture;
        bool detailView = state.View is BrowserView.Skin or BrowserView.SkinPose
            or BrowserView.Skeleton or BrowserView.AnimationLarge;
        bool skeletonChooser = state.View == BrowserView.Skeleton && _skeletonSkinsOpen;
        bool fullWidth = detailView || state.View is BrowserView.Skins or BrowserView.Skeletons
            or BrowserView.Materials;
        _textureInspector.Visible = textureView;
        _slotInspector.Visible = state.View == BrowserView.Material;
        _animationInspector.Visible = state.View == BrowserView.Animations;
        _meshInspector.Visible = state.View == BrowserView.Meshes;
        _audioView.Visible = state.View == BrowserView.Audio;
        _materialSlots.Visible = state.View == BrowserView.Material
            || state.View == BrowserView.LinkedTexture;
        _skinView.Visible = state.View == BrowserView.Skin;
        _poseHost.Visible = false;
        _animationLargeHost.Visible = false;
        _skeletonView.Visible = state.View == BrowserView.Skeleton;
        _meshSkins.Visible = false;
        _skeletonSkins.Visible = false;
        if (fullWidth && !_inner.Panel2Collapsed)
        {
            _inspectorWidth = _inner.Panel2.Width;
        }

        _inner.Panel2Collapsed = fullWidth;
        _titleLine.Visible = !detailView || skeletonChooser;
        _openSkin.Visible = state.View == BrowserView.Skins;
        _openSkeleton.Visible = state.View == BrowserView.Skeletons;
        _openMaterial.Visible = state.View == BrowserView.Materials;
        _copyHash.Visible = _profile is not null;
        UpdateCopyHashEnabled();
        if (state.View != BrowserView.Audio && _audioView.Entry is not null)
        {
            _audioView.Reset();
        }

        if (_navigation.IsChooserOpen)
        {
            _textureHost.Visible = false;
            _textureToolbar.Visible = false;
            _chooser.Visible = true;
            _materialSlots.Visible = false;
            _slotInspector.Visible = false;
            _animationInspector.Visible = false;
            _textureInspector.Visible = true;
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
                $"{state.Texture?.ArchiveName ?? ScopeName(state.ArchivePath)} · " +
                $"{_chooser.RowCount:N0} material uses · all archives";
            return;
        }

        _chooser.Visible = false;
        _search.Enabled = state.View is BrowserView.Textures
            or BrowserView.Animations or BrowserView.Meshes or BrowserView.Skins
            or BrowserView.Skeletons or BrowserView.Materials or BrowserView.Audio;
        _filterButton.Enabled = state.View == BrowserView.Textures
            || (state.View == BrowserView.Audio && _audioLoaded);
        if (state.View == BrowserView.Audio)
        {
            UpdateAudioFilterButton(state);
        }
        else
        {
            UpdateFilterButton(state.Filter);
        }

        _textureHost.Visible = state.View is BrowserView.Textures
            or BrowserView.Animations or BrowserView.Meshes or BrowserView.Skins
            or BrowserView.Skeletons or BrowserView.Materials or BrowserView.Audio;
        _textureToolbar.Visible = state.View is BrowserView.Textures or BrowserView.Audio;
        _resultsBody.Visible = !detailView || skeletonChooser;
        _resultsBody.Height = 18;

        switch (state.View)
        {
            case BrowserView.Textures:
                _resultsTitle.Text = "Textures";
                SetListMode(BrowserView.Textures);
                StartTextureList(state);
                break;
            case BrowserView.Animations:
                _resultsTitle.Text = "Animations";
                SetListMode(BrowserView.Animations);
                RenderAnimationList(state);
                _resultsCount.Text = $"{_textureList.Rows.Count:N0}";
                string clipNoun = _textureList.Rows.Count == 1 ? "clip" : "clips";
                _resultsBody.Text =
                    $"{ScopeName(state.ArchivePath)} · {_textureList.Rows.Count:N0} " +
                    $"{clipNoun} · hash order";
                break;
            case BrowserView.Meshes:
                SetListMode(BrowserView.Meshes);
                if (_meshSkinsOpen)
                {
                    _resultsTitle.Text = "Skins";
                    _resultsCount.Text = $"{_meshSkins.RowCount:N0}";
                    _resultsBody.Text =
                        $"0x{state.Mesh?.Hash:X16} · skins that link this mesh";
                    _textureHost.Visible = false;
                    _textureToolbar.Visible = false;
                    _meshSkins.Visible = true;
                }
                else
                {
                    _resultsTitle.Text = "Meshes";
                    RenderMeshList(state);
                }

                break;
            case BrowserView.Skins:
                _resultsTitle.Text = "Skins";
                SetListMode(BrowserView.Skins);
                RenderSkinList(state);
                break;
            case BrowserView.Skeletons:
                _resultsTitle.Text = "Skeletons";
                SetListMode(BrowserView.Skeletons);
                RenderSkeletonList(state);
                break;
            case BrowserView.Materials:
                _resultsTitle.Text = "Materials";
                SetListMode(BrowserView.Materials);
                RenderMaterialList(state);
                break;
            case BrowserView.Skin:
                _textureHost.Visible = false;
                _textureToolbar.Visible = false;
                _skinView.Visible = true;
                _ = RenderSkinAsync(state);
                break;
            case BrowserView.SkinPose:
                _textureHost.Visible = false;
                _textureToolbar.Visible = false;
                _skinView.Visible = false;
                _poseHost.Visible = true;
                RenderSkinPose(state);
                break;
            case BrowserView.AnimationLarge:
                _textureHost.Visible = false;
                _textureToolbar.Visible = false;
                _animationLargeHost.Visible = true;
                RenderAnimationLarge(state);
                break;
            case BrowserView.Skeleton:
                _textureHost.Visible = false;
                _textureToolbar.Visible = false;
                if (_skeletonSkinsOpen)
                {
                    _skeletonView.Visible = false;
                    _skeletonSkins.Visible = true;
                    _resultsTitle.Text = "Skins";
                    _resultsCount.Text = $"{_skeletonSkins.RowCount:N0}";
                    _resultsBody.Text = state.Skeleton is { } skeleton
                        ? $"0x{skeleton.Hash:X16} · explicitly bound skins"
                        : "";
                }
                else
                {
                    _skeletonView.Visible = true;
                    _ = RenderSkeletonAsync(state);
                }

                break;
            case BrowserView.Audio:
                _resultsTitle.Text = "Audio";
                SetListMode(BrowserView.Audio);
                if (!_audioLoaded)
                {
                    _textureHost.Visible = true;
                    _textureToolbar.Visible = false;
                    _resultsCount.Text = "";
                    _resultsBody.Text = "SOUNDBANKS · scanning...";
                    _audioView.Reset();
                    _ = LoadAudioAsync();
                    break;
                }

                _textureToolbar.Visible = true;
                if (state.Audio is { } current
                    && (_audioView.Entry is null
                        || BrowserKeys.Audio(_audioView.Entry) != BrowserKeys.Audio(current)))
                {
                    _audioView.SetAudio(current);
                }

                StartAudioList(state);
                break;
            case BrowserView.Material:
                _resultsTitle.Text = "Material";
                ShowMaterialSlots(state);
                RenderSlotInspector();
                if (state.MaterialError is { } error)
                {
                    _resultsCount.Text = "";
                    _resultsBody.Text = state.Material is { } failed
                        ? $"0x{failed.Hash:X16} · read error · {error}"
                        : error;
                    break;
                }

                MaterialData? materialData = MaterialFor(state);
                int filled = materialData?.Slots.Count ?? 0;
                _resultsCount.Text = $"{filled:N0} of {MaterialSlotRoles.Count} slots";
                _resultsBody.Text = state.Material is { } material
                    ? $"0x{material.Hash:X16} · {material.Code} · {material.ArchiveName} · " +
                        (filled == 0
                            ? "no filled texture slots"
                            : $"{filled:N0} filled of {MaterialSlotRoles.Count} slots")
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
        }

        if (!_loading && !_listBuilding && !_audioListBuilding && !_audioLoading
            && !_materialListBuilding && !_allRowsBuilding)
        {
            UpdateStatus();
        }
    }

    private void StartTextureList(BrowserState state)
    {
        _listCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _listCancel = cancel;
        int version = ++_listVersion;
        _listBuilding = true;
        Stopwatch watch = Stopwatch.StartNew();
        long firstMs = -1;
        _resultsCount.Text = "";
        _resultsBody.Text = $"{ScopeName(state.ArchivePath)} · searching...";
        _status.Text = "Searching textures...";
        BrowserTiming.Write(
            $"start v{version} archive='{state.ArchivePath}' query='{state.Query}' " +
            $"sort={state.Sort} desc={state.SortDescending}");
        IProgress<int> progress = new Progress<int>(count =>
        {
            if (IsDisposed || !_listBuilding
                || version != _listVersion || cancel != _listCancel)
            {
                return;
            }

            if (firstMs < 0)
            {
                firstMs = watch.ElapsedMilliseconds;
                BrowserTiming.Write($"first v{version} {firstMs}ms {count} matches");
            }

            _resultsCount.Text = $"{count:N0}+";
            _resultsBody.Text =
                $"{ScopeName(state.ArchivePath)} · searching... {count:N0} matches";
            _status.Text = $"Searching... {count:N0} matches";
        });
        _ = BuildTextureListAsync(state, cancel, version, watch, progress);
    }

    private async Task BuildTextureListAsync(
        BrowserState state,
        CancellationTokenSource cancel,
        int version,
        Stopwatch watch,
        IProgress<int> progress)
    {
        try
        {
            List<AssetRow> rows = await Task.Run(
                () => TextureListBuilder.Build(_assets, state, cancel.Token, progress),
                cancel.Token);
            if (IsDisposed || version != _listVersion || cancel != _listCancel)
            {
                BrowserTiming.Write($"discard v{version}");
                return;
            }

            long settled = watch.ElapsedMilliseconds;
            BrowserTiming.Write($"settled v{version} {settled}ms {rows.Count} rows");
            _listBuilding = false;
            ApplyTextureRows(rows);
        }
        catch (OperationCanceledException)
        {
            BrowserTiming.Write($"canceled v{version}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidDataException)
        {
            if (!IsDisposed && version == _listVersion && cancel == _listCancel)
            {
                _listBuilding = false;
                _status.Text = ex.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_listCancel, cancel))
            {
                _listCancel = null;
            }

            cancel.Dispose();
        }
    }

    private void ApplyTextureRows(IReadOnlyList<AssetRow> rows)
    {
        if (_navigation is null || _navigation.State.View != BrowserView.Textures)
        {
            return;
        }

        BrowserState current = _navigation.State;
        string? keep = current.Texture is null ? null : BrowserKeys.Texture(current.Texture);
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

        _textureList.TopIndex = current.ListIndex;
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
        _resultsCount.Text = $"{rows.Count:N0}";
        _resultsBody.Text =
            $"{ScopeName(current.ArchivePath)} · {rows.Count:N0} of " +
            $"{TextureCount(current.ArchivePath):N0} textures · {SortLabel(current)}";
        BrowserTiming.Write(
            $"apply v{_listVersion} selected={_textureList.SelectedKeys.Count} " +
            $"keep={(keep is null ? "none" : keep)}");
        if (!_loading)
        {
            UpdateStatus();
        }
    }

    private void UpdateSelectionCount()
    {
        int count = _textureList.SelectedKeys.Count;
        _selectionCount.Text = SelectionCountText(count, CompactToolbar);
        _exportSelected.Enabled = !_exporting && !_loading && count > 0
            && _textureList.SelectedRows.Any(row =>
                row.Tag is AssetEntry entry && entry.IsPreviewable);
    }

    private bool CompactToolbar =>
        _inner.Panel1.Width > 0 && _inner.Panel1.Width < 560;

    private string SelectionCountText(int count, bool compact) =>
        compact ? $"{count:N0} sel" : $"{count:N0} selected";

    private void UpdateToolbarLayout()
    {
        bool compact = CompactToolbar;
        _sortLabel.Visible = !compact;
        _sortCombo.Width = compact ? 104 : 124;
        _sortDirection.Width = compact ? 56 : 60;
        _exportSelected.Text = compact ? "Export" : "Export selected";
        _selectionCount.Text = SelectionCountText(
            _textureList.SelectedKeys.Count, compact);
    }

    private void SetListMode(BrowserView view)
    {
        bool narrow = ClientSize.Width < BrowserLayout.CollapseWidth;
        IReadOnlyList<AssetColumn> columns = view switch
        {
            BrowserView.Animations => AnimationColumns,
            BrowserView.Meshes => MeshColumns,
            BrowserView.Skins => SkinColumns,
            BrowserView.Skeletons => SkeletonColumns,
            BrowserView.Materials => MaterialColumns,
            BrowserView.Audio => narrow ? NarrowAudioColumns : AudioColumns,
            _ => narrow ? NarrowTextureColumns : TextureColumns,
        };
        bool textures = view == BrowserView.Textures;
        bool meshes = view == BrowserView.Meshes;
        bool audio = view == BrowserView.Audio;
        bool thumbnails = textures || meshes;
        _textureHeader.Columns = columns;
        _textureList.Columns = columns;
        _textureHeader.ShowThumbnails = thumbnails;
        _textureList.ShowThumbnails = thumbnails;
        _textureHeader.Height = thumbnails ? 28 : 24;
        _textureList.SetRowHeight(thumbnails ? 46 : 30);
        if (!meshes)
        {
            _meshThumbnails?.SetVisible(Array.Empty<AssetEntry>());
        }

        _textureToolbar.Visible = textures || audio;
        _exportSelected.Visible = textures;
        _selectionCount.Visible = textures;
        _textureList.AllowMultiSelect = textures;
        if ((textures || audio) && _navigation is not null)
        {
            SyncSortControls(_navigation.State);
        }

        UpdateToolbarLayout();
    }

    private void SyncSortControls(BrowserState state)
    {
        bool audio = state.View == BrowserView.Audio;
        string[] items = audio
            ? ["Pack", "Id", "Name", "Length", "Channels", "Rate", "Language", "Bank"]
            : ["Archive order", "Hash", "Size", "Mips"];
        _suppressSort = true;
        try
        {
            bool same = _sortCombo.Items.Count == items.Length;
            for (int index = 0; same && index < items.Length; index++)
            {
                same = string.Equals(
                    _sortCombo.Items[index]?.ToString(), items[index], StringComparison.Ordinal);
            }

            if (!same)
            {
                _sortCombo.Items.Clear();
                _sortCombo.Items.AddRange(items);
            }

            _sortCombo.SelectedIndex = audio
                ? (int)state.AudioSort
                : state.Sort switch
                {
                    TextureSort.Hash => 1,
                    TextureSort.Size => 2,
                    TextureSort.Mips => 3,
                    _ => 0,
                };
            _sortDirection.Text = (audio ? state.AudioSortDescending : state.SortDescending)
                ? "Desc"
                : "Asc";
        }
        finally
        {
            _suppressSort = false;
        }
    }

    private void RenderAnimationList(BrowserState state)
    {
        if (TryRenderAllRows(state))
        {
            return;
        }

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

        if (state.Animation is null)
        {
            _textureList.TopIndex = state.ListIndex;
            _textureList.EnsureVisible(_textureList.SelectedIndex);
        }
        else
        {
            _textureList.ScrollOffset = state.ListScroll;
        }

        if (rows.Count == 0)
        {
            _animationInspector.ShowEmpty();
        }
        else
        {
            RenderAnimation();
        }
    }

    private IReadOnlyList<AssetRow> BuildAnimationRows(BrowserState state) =>
        AssetListBuilder.Animations(
            _assets, _animationOccurrences, state.ArchivePath, state.Query);

    private IReadOnlyList<AssetRow> BuildMeshRows(BrowserState state) =>
        AssetListBuilder.Meshes(_assets, state.ArchivePath, state.Query);

    private IReadOnlyList<AssetRow> BuildSkinRows(BrowserState state) =>
        AssetListBuilder.Skins(_assets, state.ArchivePath, state.Query);

    private IReadOnlyList<AssetRow> BuildSkeletonRows(BrowserState state) =>
        _skeletonCatalog is null
            ? Array.Empty<AssetRow>()
            : AssetListBuilder.Skeletons(
                _skeletonCatalog.Entries, state.ArchivePath, state.Query);

    private bool TryRenderAllRows(BrowserState state)
    {
        if (state.ArchivePath.Length != 0
            || state.View is not (BrowserView.Animations or BrowserView.Meshes
                or BrowserView.Skins or BrowserView.Skeletons))
        {
            return false;
        }

        if (state.View == BrowserView.Skeletons && _skeletonCatalog is null)
        {
            return false;
        }

        string query = state.Query;
        if (_allRowsCache is not null
            && _allRowsCacheView == state.View
            && _allRowsCacheQuery.Equals(query, StringComparison.Ordinal))
        {
            ApplyAllRows(state, _allRowsCache);
            return true;
        }

        if (_allRowsBuilding
            && _allRowsView == state.View
            && _allRowsQuery.Equals(query, StringComparison.Ordinal))
        {
            _resultsCount.Text = "";
            _resultsBody.Text = "All archives · searching...";
            _status.Text = "Searching...";
            return true;
        }

        StartAllRows(state);
        return true;
    }

    private void StartAllRows(BrowserState state)
    {
        _allRowsCancel?.Cancel();
        _allRowsCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _allRowsCancel = cancel;
        int version = ++_allRowsVersion;
        _allRowsBuilding = true;
        _allRowsView = state.View;
        _allRowsQuery = state.Query;
        _allRowsCache = null;
        _resultsCount.Text = "";
        _resultsBody.Text = "All archives · searching...";
        _status.Text = "Searching...";
        BrowserTiming.Write($"all-rows start view={state.View} query='{state.Query}'");
        _ = BuildAllRowsAsync(state, cancel, version);
    }

    private async Task BuildAllRowsAsync(
        BrowserState state,
        CancellationTokenSource cancel,
        int version)
    {
        BrowserView view = state.View;
        string query = state.Query;
        IReadOnlyList<AssetEntry> assets = _assets;
        IReadOnlyDictionary<ulong, int> occurrences = _animationOccurrences;
        IReadOnlyList<SkeletonEntry> skeletons =
            _skeletonCatalog?.Entries ?? Array.Empty<SkeletonEntry>();
        try
        {
            IReadOnlyList<AssetRow> rows = await Task.Run(
                () => view switch
                {
                    BrowserView.Animations => AssetListBuilder.Animations(
                        assets, occurrences, "", query, cancel.Token),
                    BrowserView.Meshes => AssetListBuilder.Meshes(
                        assets, "", query, cancel.Token),
                    BrowserView.Skins => AssetListBuilder.Skins(
                        assets, "", query, cancel.Token),
                    _ => AssetListBuilder.Skeletons(
                        skeletons, "", query, cancel.Token),
                },
                cancel.Token);
            if (IsDisposed || cancel != _allRowsCancel || version != _allRowsVersion
                || _navigation?.State.View != view
                || _navigation.State.ArchivePath.Length != 0
                || !_navigation.State.Query.Equals(query, StringComparison.Ordinal))
            {
                return;
            }

            _allRowsBuilding = false;
            _allRowsCache = rows;
            _allRowsCacheView = view;
            _allRowsCacheQuery = query;
            ApplyAllRows(_navigation.State, rows);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (cancel == _allRowsCancel)
            {
                _allRowsBuilding = false;
            }
        }
    }

    private void ApplyAllRows(BrowserState state, IReadOnlyList<AssetRow> rows)
    {
        _textureHost.Visible = true;
        _textureToolbar.Visible = false;
        _meshSkins.Visible = false;
        string? keep = state.View switch
        {
            BrowserView.Animations => state.Animation is null
                ? null
                : AnimationKey(state.Animation),
            BrowserView.Meshes => state.Mesh is null ? null : BrowserKeys.Mesh(state.Mesh),
            BrowserView.Skins => state.Skin is null ? null : BrowserKeys.Skin(state.Skin),
            _ => state.Skeleton is null ? null : BrowserKeys.Skeleton(state.Skeleton),
        };
        _textureList.SetRows(rows, keep);
        bool keepFound = keep is not null
            && _textureList.SelectedRow?.Key.Equals(keep, StringComparison.Ordinal) == true;
        if (!keepFound)
        {
            if (state.View is BrowserView.Animations or BrowserView.Meshes)
            {
                _textureList.SelectedIndex = rows.Count > 0 ? 0 : -1;
                if (rows.Count > 0)
                {
                    if (state.View == BrowserView.Animations)
                    {
                        OnAnimationSelected();
                    }
                    else
                    {
                        OnMeshSelected();
                    }
                }
            }
            else
            {
                _textureList.SelectedIndex = -1;
            }
        }

        bool restoring = keep is not null;
        if (restoring)
        {
            _textureList.ScrollOffset = state.ListScroll;
        }
        else
        {
            _textureList.TopIndex = state.ListIndex;
            if (_textureList.SelectedIndex >= 0)
            {
                _textureList.EnsureVisible(_textureList.SelectedIndex);
            }
        }

        _resultsCount.Text = $"{rows.Count:N0}";
        _resultsBody.Text =
            $"All archives · {rows.Count:N0} {AllRowsNoun(state.View)} · hash order";
        _status.Text = $"All archives · {rows.Count:N0} {AllRowsNoun(state.View)}";
        BrowserTiming.Write(
            $"all-rows applied view={state.View} query='{state.Query}' rows={rows.Count:N0}");
        if (state.View == BrowserView.Animations)
        {
            if (rows.Count == 0)
            {
                _animationInspector.ShowEmpty();
            }
            else
            {
                RenderAnimation();
            }
        }
        else if (state.View == BrowserView.Meshes)
        {
            if (rows.Count == 0)
            {
                _meshInspector.ShowEmpty();
            }
            else if (keepFound)
            {
                _ = RenderMeshInspectorAsync(state);
            }
        }
        else if (state.View == BrowserView.Skins)
        {
            _openSkin.Visible = true;
            UpdateOpenSkinEnabled();
        }
        else
        {
            _openSkeleton.Visible = true;
            UpdateOpenSkeletonEnabled();
        }
    }

    private static string AllRowsNoun(BrowserView view) => view switch
    {
        BrowserView.Animations => "animations",
        BrowserView.Meshes => "meshes",
        BrowserView.Skins => "skins",
        _ => "skeletons",
    };

    private void OnAnimationSelected()
    {
        if (_navigation is null
            || _navigation.State.View != BrowserView.Animations
            || _textureList.SelectedRow?.Tag is not AssetEntry entry)
        {
            return;
        }

        _navigation.SetAnimation(ToAnimation(entry), _textureList.TopIndex);
        _navigation.SetListScroll(_textureList.ScrollOffset);
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

        AssetEntry? entry = FindAnimationEntry(_assets, animation);
        if (entry is null)
        {
            _animationInspector.ShowEmpty();
            return;
        }

        _animationInspector.SetClip(entry, _assets, _profile);
    }

    private void OpenAnimationLarge(AnimationLargeState large)
    {
        if (_navigation?.State.View != BrowserView.Animations
            || _navigation.State.Animation is null)
        {
            return;
        }

        _navigation.OpenAnimationLarge(large);
        RenderState();
    }

    private void RenderAnimationLarge(BrowserState state)
    {
        if (_profile is null || state.Animation is not { } animation
            || state.AnimationLarge is not { } large)
        {
            ClearAnimationLargeView();
            return;
        }

        AssetEntry? clip = FindAnimationEntry(_assets, animation);
        AssetEntry? skin = _assets.FirstOrDefault(entry =>
            entry.ClassHash == AssetClass.Skin
            && entry.NameHash == large.SkinHash
            && entry.ArchivePath.Equals(
                large.SkinArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.Row.BodyOffset == large.SkinBodyOffset);
        if (clip is null || skin is null)
        {
            _status.Text = "The animation or its preview skin is no longer indexed.";
            return;
        }

        string key =
            $"{BrowserKeys.Skin(skin)}|{large.SkeletonRoot:X16}|{large.Frame}|{large.Applied}";
        if (_animationLargeKey == key && _animationLargeView is { IsDisposed: false })
        {
            return;
        }

        ClearAnimationLargeView();
        AnimationLargeView view = new() { Dock = DockStyle.Fill };
        view.BackRequested += (_, _) => Back();
        _animationLargeHost.Controls.Add(view);
        _animationLargeView = view;
        _animationLargeKey = key;
        view.ShowState(_profile, _assets, skin, clip, large);
        _status.Text =
            $"Animation 0x{animation.Hash:X16} · large preview · " +
            $"skin 0x{large.SkinHash:X16}";
    }

    private void ClearAnimationLargeView()
    {
        if (_animationLargeView is null)
        {
            return;
        }

        _animationLargeHost.Controls.Remove(_animationLargeView);
        _animationLargeView.Dispose();
        _animationLargeView = null;
        _animationLargeKey = null;
    }

    internal static AssetEntry? FindAnimationEntry(
        IReadOnlyList<AssetEntry> assets,
        AnimationOccurrence animation) => assets.FirstOrDefault(entry =>
            entry.ClassHash == AssetClass.Animation
            && entry.NameHash == animation.Hash
            && entry.ArchivePath.Equals(
                animation.ArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.BodyArchivePath.Equals(
                animation.BodyArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.Row.BodyOffset == animation.BodyOffset);

    private static AnimationOccurrence ToAnimation(AssetEntry entry) => new(
        entry.NameHash,
        entry.ArchivePath,
        entry.BodyArchivePath,
        entry.Row.BodyOffset);

    private static string AnimationKey(AssetEntry entry) =>
        $"{entry.ArchivePath}|{entry.BodyArchivePath}|{entry.Row.BodyOffset}|{entry.NameHash:X16}";

    private static string AnimationKey(AnimationOccurrence animation) =>
        $"{animation.ArchivePath}|{animation.BodyArchivePath}|" +
        $"{animation.BodyOffset}|{animation.Hash:X16}";

    private void RenderMeshList(BrowserState state)
    {
        _meshSkins.Visible = false;
        _textureHost.Visible = true;
        _textureToolbar.Visible = false;
        if (TryRenderAllRows(state))
        {
            return;
        }

        IReadOnlyList<AssetRow> rows = BuildMeshRows(state);
        string? keep = state.Mesh is null ? null : BrowserKeys.Mesh(state.Mesh);
        _textureList.SetRows(rows, keep);
        bool keepFound = keep is not null
            && _textureList.SelectedRow?.Key.Equals(keep, StringComparison.Ordinal) == true;
        if (!keepFound)
        {
            _textureList.SelectedIndex = rows.Count > 0 ? 0 : -1;
            if (rows.Count > 0)
            {
                OnMeshSelected();
            }
        }

        if (state.Mesh is null)
        {
            _textureList.TopIndex = state.ListIndex;
            _textureList.EnsureVisible(_textureList.SelectedIndex);
        }
        else
        {
            _textureList.ScrollOffset = state.ListScroll;
        }

        _resultsCount.Text = $"{rows.Count:N0}";
        _resultsBody.Text =
            $"{ScopeName(state.ArchivePath)} · {rows.Count:N0} meshes · hash order";
        if (rows.Count == 0)
        {
            _meshInspector.ShowEmpty();
        }
        else if (keepFound)
        {
            _ = RenderMeshInspectorAsync(state);
        }
    }

    private void RenderSkinList(BrowserState state)
    {
        _skinView.Visible = false;
        _textureHost.Visible = true;
        _textureToolbar.Visible = false;
        if (TryRenderAllRows(state))
        {
            return;
        }

        IReadOnlyList<AssetRow> rows = BuildSkinRows(state);
        string? keep = state.Skin is null ? null : BrowserKeys.Skin(state.Skin);
        _textureList.SetRows(rows, keep);
        if (state.Skin is null)
        {
            _textureList.TopIndex = state.ListIndex;
            if (_textureList.SelectedIndex >= 0)
            {
                _textureList.EnsureVisible(_textureList.SelectedIndex);
            }
        }
        else
        {
            _textureList.ScrollOffset = state.ListScroll;
        }

        _resultsCount.Text = $"{rows.Count:N0}";
        _resultsBody.Text =
            $"{ScopeName(state.ArchivePath)} · {rows.Count:N0} skins · hash order";
        _openSkin.Visible = true;
        UpdateOpenSkinEnabled();
    }

    private void UpdateOpenSkinEnabled()
    {
        _openSkin.Enabled = _navigation?.State.View == BrowserView.Skins
            && _textureList.SelectedRow?.Tag is AssetEntry entry
            && entry.ClassHash == AssetClass.Skin;
    }

    private void OpenSkinFromList()
    {
        if (_navigation?.State.View != BrowserView.Skins
            || _textureList.SelectedRow?.Tag is not AssetEntry entry
            || entry.ClassHash != AssetClass.Skin)
        {
            return;
        }

        SkinOccurrence skin = ToSkin(entry);
        _navigation.SetSkinListSelection(skin, _textureList.TopIndex);
        _navigation.SetListScroll(_textureList.ScrollOffset);
        _navigation.OpenSkin(skin, 0);
        RenderState();
    }

    private void RenderSkeletonList(BrowserState state)
    {
        _skeletonView.Visible = false;
        _textureHost.Visible = true;
        _textureToolbar.Visible = false;
        if (_skeletonCatalog is null)
        {
            _textureList.SetRows(Array.Empty<AssetRow>(), null);
            _resultsCount.Text = "";
            _resultsBody.Text = "Skel_Z objects · scanning...";
            _status.Text = "Indexing skeletons...";
            _ = EnsureSkeletonCatalogAsync();
            UpdateOpenSkeletonEnabled();
            return;
        }

        if (TryRenderAllRows(state))
        {
            return;
        }

        IReadOnlyList<AssetRow> rows = BuildSkeletonRows(state);
        string? keep = state.Skeleton is null ? null : BrowserKeys.Skeleton(state.Skeleton);
        _textureList.SetRows(rows, keep);
        if (state.Skeleton is null)
        {
            _textureList.TopIndex = state.ListIndex;
            if (_textureList.SelectedIndex >= 0)
            {
                _textureList.EnsureVisible(_textureList.SelectedIndex);
            }
        }
        else
        {
            _textureList.ScrollOffset = state.ListScroll;
        }

        _resultsCount.Text = $"{rows.Count:N0}";
        _resultsBody.Text =
            $"{ScopeName(state.ArchivePath)} · {rows.Count:N0} skeletons · hash order";
        _openSkeleton.Visible = true;
        UpdateOpenSkeletonEnabled();
    }

    private void UpdateOpenSkeletonEnabled()
    {
        _openSkeleton.Enabled = _navigation?.State.View == BrowserView.Skeletons
            && _textureList.SelectedRow?.Tag is SkeletonEntry;
    }

    private void OpenSkeletonFromList()
    {
        if (_navigation?.State.View != BrowserView.Skeletons
            || _textureList.SelectedRow?.Tag is not SkeletonEntry entry)
        {
            return;
        }

        SkeletonOccurrence skeleton = ToSkeleton(entry);
        _navigation.SetSkeletonListSelection(skeleton, _textureList.TopIndex);
        _navigation.SetListScroll(_textureList.ScrollOffset);
        _navigation.OpenSkeleton(skeleton);
        RenderState();
    }

    private static SkeletonOccurrence ToSkeleton(SkeletonEntry entry) => new(
        entry.NameHash,
        entry.ArchivePath,
        entry.Offset);

    private void RenderMaterialList(BrowserState state)
    {
        _textureHost.Visible = true;
        _textureToolbar.Visible = false;
        if (_materials is null)
        {
            _textureList.SetRows(Array.Empty<AssetRow>(), null);
            _resultsCount.Text = "";
            _resultsBody.Text = "Material_Z objects · scanning...";
            _status.Text = "Scanning materials...";
            _ = EnsureMaterialCatalogAsync();
            UpdateOpenMaterialEnabled();
            return;
        }

        if (state.ArchivePath.Length == 0)
        {
            if (_materialCache.GetRows("") is not null)
            {
                _materialEntryWatch = null;
                ApplyMaterialList(
                    state, firstEntry: false, firstWatch: null,
                    filterWatch: _materialFilterWatch);
                return;
            }

            if (_materialListBuilding && _materialListArchive.Length == 0)
            {
                return;
            }

            StartMaterialList(state);
            return;
        }

        if (_materialCache.GetRows(state.ArchivePath) is not null)
        {
            _materialEntryWatch = null;
            ApplyMaterialList(
                state, firstEntry: false, firstWatch: null, filterWatch: _materialFilterWatch);
            return;
        }

        if (_materialListBuilding
            && _materialListArchive.Equals(state.ArchivePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StartMaterialList(state);
    }

    private void StartMaterialList(BrowserState state)
    {
        _materialListCancel?.Cancel();
        _materialListCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _materialListCancel = cancel;
        CancellationToken token = cancel.Token;
        int version = ++_materialListVersion;
        _materialListBuilding = true;
        _materialListArchive = state.ArchivePath;
        _resultsCount.Text = "";
        _resultsBody.Text = state.ArchivePath.Length == 0
            ? "All archives · decoding materials..."
            : $"{ScopeName(state.ArchivePath)} · decoding materials...";
        _status.Text = "Decoding materials...";
        BrowserTiming.Write(state.ArchivePath.Length == 0
            ? "materials-all start"
            : $"materials-first start archive='{state.ArchivePath}'");
        Stopwatch watch = _materialEntryWatch ?? Stopwatch.StartNew();
        _materialEntryWatch = watch;
        int generation = _materialCache.Generation;
        _ = BuildMaterialListAsync(
            state.ArchivePath, cancel, token, version, generation, watch);
    }

    private async Task BuildMaterialListAsync(
        string archive,
        CancellationTokenSource cancel,
        CancellationToken token,
        int version,
        int generation,
        Stopwatch watch)
    {
        MaterialCatalog? catalog = _materials;
        if (catalog is null)
        {
            return;
        }

        List<MaterialEntry> entries = archive.Length == 0
            ? catalog.Entries.ToList()
            : catalog.Entries
                .Where(entry => entry.ArchivePath.Equals(
                    archive, StringComparison.OrdinalIgnoreCase))
                .ToList();
        Dictionary<string, IReadOnlyList<MaterialRow>> cached =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in entries
            .Select(entry => entry.ArchivePath)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_materialCache.GetRows(path) is { } rows)
            {
                cached[path] = rows;
            }
        }

        Progress<int>? progress = archive.Length == 0
            ? null
            : new Progress<int>(count =>
            {
                if (IsDisposed || !_materialListBuilding || version != _materialListVersion
                    || cancel != _materialListCancel)
                {
                    return;
                }

                _resultsBody.Text =
                    $"{ScopeName(archive)} · decoding materials... " +
                    $"{count:N0} of {entries.Count:N0}";
                _status.Text = $"Decoding materials {count:N0}/{entries.Count:N0}";
            });

        try
        {
            MaterialWork work = await Task.Run(
                () => BuildMaterialWork(entries, cached, progress, token),
                token);
            if (IsDisposed || cancel != _materialListCancel || version != _materialListVersion)
            {
                return;
            }

            if (archive.Length == 0)
            {
                foreach ((string path, MaterialListResult result) in work.Results)
                {
                    if (!_materialCache.TryStore(generation, path, result))
                    {
                        return;
                    }
                }

                if (!_materialCache.TryStoreAll(generation, work.Rows))
                {
                    return;
                }
            }
            else
            {
                MaterialListResult result = work.Results.Count > 0
                    ? work.Results[0].Result
                    : new MaterialListResult(work.Rows, 0);
                if (!_materialCache.TryStore(generation, archive, result))
                {
                    return;
                }
            }

            _materialListBuilding = false;
            BrowserTiming.Write(archive.Length == 0
                ? $"materials-all decoded {watch.ElapsedMilliseconds}ms " +
                    $"rows={work.Rows.Count:N0} decodes={_materialCache.DecodeCount:N0}"
                : $"materials-first decoded {watch.ElapsedMilliseconds}ms " +
                    $"rows={work.Rows.Count:N0} decodes={_materialCache.DecodeCount:N0}");
            if (_navigation?.State.View != BrowserView.Materials
                || !_navigation.State.ArchivePath.Equals(
                    archive, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyMaterialList(
                _navigation.State, firstEntry: true, firstWatch: watch, filterWatch: null);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _materialListCancel && version == _materialListVersion)
            {
                _status.Text = ex.Message;
                _resultsBody.Text = ex.Message;
            }
        }
        finally
        {
            if (cancel == _materialListCancel)
            {
                _materialListBuilding = false;
            }
        }
    }

    private MaterialWork BuildMaterialWork(
        List<MaterialEntry> entries,
        IReadOnlyDictionary<string, IReadOnlyList<MaterialRow>> cached,
        IProgress<int>? progress,
        CancellationToken token)
    {
        List<MaterialRow> rows = new();
        List<(string Archive, MaterialListResult Result)> results = new();
        foreach (IGrouping<string, MaterialEntry> group in entries
            .GroupBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            if (cached.TryGetValue(group.Key, out IReadOnlyList<MaterialRow>? cachedRows))
            {
                rows.AddRange(cachedRows);
                continue;
            }

            using FileStream stream = new(
                group.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            MaterialListResult result = MaterialListBuilder.Build(
                group.ToList(),
                entry =>
                {
                    MaterialData data = MaterialDecoder.Read(
                        stream, entry.ArchivePath, entry.Hit);
                    return (data.Code, data.Slots.Count);
                },
                token,
                progress);
            results.Add((group.Key, result));
            rows.AddRange(result.Rows);
        }

        rows.Sort((left, right) =>
        {
            int order = left.Hash.CompareTo(right.Hash);
            if (order == 0)
            {
                order = string.Compare(
                    left.Entry.ArchivePath,
                    right.Entry.ArchivePath,
                    StringComparison.OrdinalIgnoreCase);
            }

            return order == 0
                ? left.Entry.Offset.CompareTo(right.Entry.Offset)
                : order;
        });
        token.ThrowIfCancellationRequested();
        return new MaterialWork(rows, results);
    }

    private void ApplyMaterialList(
        BrowserState state,
        bool firstEntry,
        Stopwatch? firstWatch,
        Stopwatch? filterWatch)
    {
        IReadOnlyList<MaterialRow>? cached = _materialCache.GetRows(state.ArchivePath);
        if (_navigation is null || state.View != BrowserView.Materials || cached is null)
        {
            return;
        }

        IReadOnlyList<MaterialRow> rows = MaterialListBuilder.Filter(cached, state.Query);
        List<AssetRow> assetRows = new(rows.Count);
        foreach (MaterialRow row in rows)
        {
            assetRows.Add(new AssetRow(
                BrowserKeys.Material(row.Entry),
                [
                    $"0x{row.Hash:X16}",
                    row.DisplayCode,
                    row.DisplaySlots,
                    row.ArchiveName,
                ],
                row));
        }

        string? keep = state.Material is null ? null : BrowserKeys.Material(state.Material);
        _textureList.SetRows(assetRows, keep);
        if (state.Material is not null)
        {
            _textureList.ScrollOffset = state.ListScroll;
        }
        else
        {
            _textureList.TopIndex = state.ListIndex;
        }

        _resultsCount.Text = rows.Count == cached.Count
            ? $"{rows.Count:N0}"
            : $"{rows.Count:N0} of {cached.Count:N0}";
        _resultsBody.Text =
            $"{ScopeName(state.ArchivePath)} · {cached.Count:N0} materials · hash order" +
            (state.Query.Trim().Length == 0 ? "" : " · filtered");
        _openMaterial.Visible = true;
        UpdateOpenMaterialEnabled();
        UpdateStatus();
        if (firstEntry && firstWatch is not null)
        {
            BrowserTiming.Write(
                $"materials-first applied {firstWatch.ElapsedMilliseconds}ms " +
                $"rows={rows.Count:N0} decodes={_materialCache.DecodeCount:N0}");
            _materialEntryWatch = null;
            _materialFilterWatch = null;
        }
        else if (filterWatch is not null)
        {
            BrowserTiming.Write(
                $"materials-filter applied {filterWatch.ElapsedMilliseconds}ms " +
                $"query='{state.Query}' rows={rows.Count:N0} decodes={_materialCache.DecodeCount:N0}");
            _materialFilterWatch = null;
        }
    }

    private void UpdateOpenMaterialEnabled()
    {
        _openMaterial.Enabled = _navigation?.State.View == BrowserView.Materials
            && _textureList.SelectedRow?.Tag is MaterialRow;
    }

    private void OpenMaterialFromList()
    {
        if (_navigation?.State.View != BrowserView.Materials
            || _textureList.SelectedRow?.Tag is not MaterialRow row)
        {
            return;
        }

        MaterialOccurrence occurrence = ToMaterial(row);
        if (row.State == MaterialRowState.Error)
        {
            _navigation.SetMaterialListSelection(
                occurrence, _textureList.TopIndex, _textureList.ScrollOffset);
            _navigation.OpenMaterialFromList(occurrence, -1, row.Error);
            RenderState();
            return;
        }

        MaterialData? data = MaterialDataFor(row);
        if (data is null)
        {
            string message = _status.Text.Length > 0
                ? _status.Text
                : "Could not read this material.";
            _navigation.SetMaterialListSelection(
                occurrence, _textureList.TopIndex, _textureList.ScrollOffset);
            _navigation.OpenMaterialFromList(occurrence, -1, message);
            RenderState();
            return;
        }

        int slot = data.Slots.Count > 0 ? data.Slots[0].Index : -1;
        _navigation.SetMaterialListSelection(
            occurrence, _textureList.TopIndex, _textureList.ScrollOffset);
        _navigation.OpenMaterialFromList(occurrence, slot);
        RenderState();
    }

    private MaterialData? MaterialDataFor(MaterialRow row)
    {
        (string Archive, long Offset, ulong Hash) key = (
            row.Entry.ArchivePath, row.Entry.Offset, row.Hash);
        if (_materialBodies.TryGetValue(key, out MaterialData? cached))
        {
            return cached;
        }

        try
        {
            using FileStream stream = new(
                row.Entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            MaterialData data = MaterialDecoder.Read(stream, row.Entry.ArchivePath, row.Entry.Hit);
            _materialBodies[key] = data;
            _materialCache.AddDecodes(1);
            return data;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
            return null;
        }
    }

    private static MaterialOccurrence ToMaterial(MaterialRow row) => new(
        row.Hash,
        row.Entry.ArchivePath,
        row.Entry.Offset,
        row.Code);

    private async Task EnsureMaterialCatalogAsync()
    {
        if (_materials is not null)
        {
            return;
        }

        _materialCatalogTask ??= LoadMaterialCatalogAsync();
        await _materialCatalogTask;
    }

    private async Task LoadMaterialCatalogAsync()
    {
        if (_profile is null)
        {
            return;
        }

        GameProfile profile = _profile;
        _materialCatalogCancel?.Cancel();
        _materialCatalogCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _materialCatalogCancel = cancel;
        Stopwatch watch = Stopwatch.StartNew();
        _status.Text = "Scanning materials...";
        try
        {
            Progress<CatalogProgress> progress = new(item =>
            {
                if (!IsDisposed && cancel == _materialCatalogCancel)
                {
                    _status.Text =
                        $"Materials {Path.GetFileName(item.ArchivePath)} " +
                        $"{item.Index:N0}/{item.Total:N0}";
                }
            });
            MaterialCatalog catalog = await Task.Run(
                () => MaterialCatalog.Load(profile, false, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _materialCatalogCancel)
            {
                return;
            }

            _materials = catalog;
            BrowserTiming.Write(
                $"materials-catalog loaded {watch.ElapsedMilliseconds}ms " +
                $"entries={catalog.Entries.Count:N0} archives={catalog.Archives.Count:N0}");
            _status.Text =
                $"{catalog.Entries.Count:N0} materials · {catalog.Archives.Count:N0} archives";
            if (_navigation is not null)
            {
                SetTypesFor(
                    _navigation.State.ArchivePath,
                    TypeName(_navigation.State));
                if (_navigation.State.View == BrowserView.Materials)
                {
                    RenderState();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _materialCatalogCancel)
            {
                _status.Text = ex.Message;
                _resultsBody.Text = ex.Message;
            }
        }
        finally
        {
            if (cancel == _materialCatalogCancel)
            {
                _materialCatalogTask = null;
            }
        }
    }

    private void OnMeshSelected()
    {
        if (_navigation is null
            || _navigation.State.View != BrowserView.Meshes
            || _textureList.SelectedRow?.Tag is not AssetEntry entry
            || entry.ClassHash != AssetClass.Mesh)
        {
            return;
        }

        _navigation.SelectMesh(ToMesh(entry), _textureList.TopIndex);
        _navigation.SetListScroll(_textureList.ScrollOffset);
        _ = RenderMeshInspectorAsync(_navigation.State);
        UpdateStatus();
    }

    private async Task RenderMeshInspectorAsync(BrowserState state)
    {
        if (state.Mesh is not { } occurrence)
        {
            _meshInspector.ShowEmpty();
            return;
        }

        AssetEntry? entry = FindMeshEntry(occurrence);
        if (entry is null)
        {
            _meshInspector.ShowMessage(
                $"Mesh 0x{occurrence.Hash:X16} was not found.", true);
            return;
        }

        string key = BrowserKeys.Mesh(entry);
        if (_meshInspector.MeshKey == key)
        {
            _meshInspector.SetCamera(
                state.MeshCamera ?? new MeshCamera(0.65f, 0.35f, 1f, PointF.Empty));
            _meshInspector.SetWireframe(state.MeshWireframe);
            UpdateMeshUsedBy();
            return;
        }

        _meshCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _meshCancel = cancel;
        _meshInspector.SetLoading(entry);
        _meshInspector.SetUsedByPending();
        _ = EnsureSkinLinksAsync();
        try
        {
            MeshGeometry geometry = await Task.Run(() =>
            {
                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                MeshData data = MeshDecoder.Read(stream, entry.BodyArchivePath, entry.Row);
                return data.Streams.Count == 0
                    ? new MeshGeometry([], [], Vector3.Zero, Vector3.Zero)
                    : data.ToGeometry();
            }, cancel.Token);
            if (IsDisposed || cancel != _meshCancel
                || _navigation?.State.View != BrowserView.Meshes
                || _navigation.State.Mesh is not { } current
                || current.Hash != occurrence.Hash
                || !current.ArchivePath.Equals(
                    occurrence.ArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            BrowserState now = _navigation.State;
            _meshInspector.SetMesh(entry, geometry, now.MeshCamera, now.MeshWireframe);
            UpdateMeshUsedBy();
            _status.Text =
                $"{Path.GetFileName(entry.ArchivePath)} · " +
                $"{geometry.Positions.Length:N0} verts · " +
                $"{geometry.Indices.Length / 3:N0} tris";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _meshCancel)
            {
                _meshInspector.ShowMessage(ex.Message, true);
            }
        }
    }

    private async Task OpenMeshSkinsAsync()
    {
        if (_navigation?.State is not { View: BrowserView.Meshes, Mesh: { } mesh })
        {
            return;
        }

        _navigation.SetMeshView(_meshInspector.Camera, _meshInspector.IsWireframe);
        await EnsureSkinLinksAsync();
        if (IsDisposed
            || _navigation?.State is not { View: BrowserView.Meshes, Mesh: { } current }
            || current.Hash != mesh.Hash
            || !current.ArchivePath.Equals(mesh.ArchivePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IReadOnlyList<SkinLinkEntry> uses = _skinLinks is null
            ? Array.Empty<SkinLinkEntry>()
            : _skinLinks.ByMesh.GetValueOrDefault(mesh.Hash) ?? Array.Empty<SkinLinkEntry>();
        _meshSkins.SetRows(
            uses.Select(row => new SkinChoiceRow(
                row.NameHash,
                row.ArchiveName,
                row.Links.MeshLinks.Count.ToString("N0"),
                $"{row.Links.MeshLinks.Count:N0} parts",
                row)).ToArray(),
            "No skin uses this mesh.");
        _meshSkinsOpen = true;
        RenderState();
    }

    private void CloseMeshSkins()
    {
        _meshSkinsOpen = false;
        RenderState();
    }

    private void ShowSkeletonSkins()
    {
        if (_navigation?.State.View != BrowserView.Skeleton
            || _navigation.State.Skeleton is not { } skeleton)
        {
            return;
        }

        if (_skinSkeletons is null)
        {
            _status.Text = "Skin bindings are still indexing.";
            return;
        }

        if (!_skinSkeletons.BySkeleton.TryGetValue(
                skeleton.Hash, out IReadOnlyList<SkinSkeletonUse>? uses)
            || uses.Count == 0)
        {
            _status.Text = "No skin is explicitly bound to this skeleton.";
            return;
        }

        _skeletonSkins.SetRows(
            uses.Select(row => new SkinChoiceRow(
                row.NameHash,
                row.ArchiveName,
                "",
                "explicitly bound",
                row)).ToArray(),
            "No skin is explicitly bound to this skeleton.");
        _skeletonSkinsOpen = true;
        RenderState();
    }

    private void CloseSkeletonSkins()
    {
        _skeletonSkinsOpen = false;
        RenderState();
    }

    private void OpenSkinFromSkeleton(SkinSkeletonUse row)
    {
        _skeletonSkinsOpen = false;
        _navigation?.OpenSkin(ToSkin(row.Row), 0);
        RenderState();
    }

    private async Task EnsureSkinSkeletonsAsync()
    {
        if (_skinSkeletons is not null)
        {
            UpdateSkeletonUsedBy();
            return;
        }

        _skinSkeletonsTask ??= LoadSkinSkeletonsAsync();
        await _skinSkeletonsTask;
        UpdateSkeletonUsedBy();
    }

    private async Task LoadSkinSkeletonsAsync()
    {
        if (_profile is null)
        {
            return;
        }

        IReadOnlyList<AssetEntry> assets = _assets;
        _skinSkeletonsCancel?.Cancel();
        _skinSkeletonsCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _skinSkeletonsCancel = cancel;
        _status.Text = "Indexing skin bindings...";
        try
        {
            Progress<CatalogProgress> progress = new(value =>
            {
                if (!IsDisposed && cancel == _skinSkeletonsCancel)
                {
                    _status.Text =
                        $"Indexing skin bindings {value.Index:N0}/{value.Total:N0}";
                }
            });
            SkinSkeletonIndex index = await Task.Run(
                () => SkinSkeletonIndex.Load(assets, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _skinSkeletonsCancel)
            {
                return;
            }

            _skinSkeletons = index;
            _status.Text =
                $"{index.BoundCount:N0} bound · {index.NoSkeleton:N0} none · " +
                $"{index.Failures.Count:N0} failed";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _skinSkeletonsCancel)
            {
                _status.Text = ex.Message;
                _skeletonView.SetUsedBy(0);
            }
        }
        finally
        {
            if (cancel == _skinSkeletonsCancel)
            {
                _skinSkeletonsTask = null;
            }
        }
    }

    private void UpdateSkeletonUsedBy()
    {
        if (_navigation?.State.View != BrowserView.Skeleton
            || _navigation.State.Skeleton is not { } skeleton)
        {
            return;
        }

        if (_skinSkeletons is null)
        {
            _skeletonView.SetUsedBy(-1);
            return;
        }

        int count = _skinSkeletons.BySkeleton.TryGetValue(
            skeleton.Hash, out IReadOnlyList<SkinSkeletonUse>? uses)
                ? uses.Count
                : 0;
        _skeletonView.SetUsedBy(count);
    }

    private void PrepareSkeletonUsedBy()
    {
        _skeletonView.SetUsedBy(-1);
        if (_skinSkeletons is null)
        {
            _ = EnsureSkinSkeletonsAsync();
        }
        else
        {
            UpdateSkeletonUsedBy();
        }
    }

    private void OpenSkin(SkinLinkEntry row)
    {
        if (_navigation?.State.Mesh is not { } mesh)
        {
            return;
        }

        int part = 0;
        for (int index = 0; index < row.Links.MeshLinks.Count; index++)
        {
            if (row.Links.MeshLinks[index] == mesh.Hash)
            {
                part = index;
                break;
            }
        }

        _meshSkinsOpen = false;
        _navigation.OpenSkin(ToSkin(row.Row), part);
        RenderState();
    }

    private void OpenMeshFromSkin(ulong hash)
    {
        if (_navigation?.State.View != BrowserView.Skin)
        {
            return;
        }

        if (!_meshRows.TryGetValue(hash, out AssetEntry? entry))
        {
            _status.Text = $"Mesh 0x{hash:X16} is not indexed.";
            return;
        }

        _navigation.OpenMeshFromSkin(ToMesh(entry), 0);
        SetTypesFor(
            _navigation.State.ArchivePath,
            TypeName(_navigation.State));
        RenderState();
    }

    private void OpenPoseFromSkin(ulong root)
    {
        if (_navigation?.State.View != BrowserView.Skin
            || _navigation.State.Skin is not { } skin)
        {
            return;
        }

        _navigation.SetSkinPart(_skinView.SelectedPart);
        _navigation.OpenSkinPose(skin, root);
        RenderState();
    }

    private void RenderSkinPose(BrowserState state)
    {
        if (_profile is null || state.Skin is not { } skin)
        {
            return;
        }

        AssetEntry? entry = FindSkinEntry(skin);
        if (entry is null)
        {
            _status.Text = $"Skin 0x{skin.Hash:X16} is not indexed.";
            return;
        }

        string key = $"{BrowserKeys.Skin(skin)}|{state.PoseSkeletonRoot:X16}";
        if (_poseKey == key && _poseView is { IsDisposed: false })
        {
            return;
        }

        ClearPoseView();
        _poseView = new PoseView(
            _profile, _assets, entry, state.PoseSkeletonRoot, showAnimation: false);
        _poseView.Dock = DockStyle.Fill;
        _poseHost.Controls.Add(_poseView);
        _poseKey = key;
        _status.Text =
            $"Pose 0x{skin.Hash:X16} · skeleton 0x{state.PoseSkeletonRoot:X16}";
        _ = _poseView.LoadAsync();
    }

    private void ClearPoseView()
    {
        if (_poseView is null)
        {
            return;
        }

        _poseHost.Controls.Remove(_poseView);
        _poseView.Dispose();
        _poseView = null;
        _poseKey = null;
    }

    private async Task RenderSkinAsync(BrowserState state)
    {
        if (state.Skin is not { } occurrence)
        {
            _skinView.Reset();
            _skinView.SetNote("Select a skin.");
            return;
        }

        AssetEntry? entry = FindSkinEntry(occurrence);
        if (entry is null)
        {
            _skinView.Reset();
            _skinView.SetNote($"Skin 0x{occurrence.Hash:X16} was not found.");
            return;
        }

        string key = BrowserKeys.Skin(entry);
        if (_skinLoadedKey == key)
        {
            _skinView.SelectPart(state.SkinPart);
            _status.Text =
                $"{Path.GetFileName(entry.ArchivePath)} · Skin 0x{entry.NameHash:X16}";
            return;
        }

        _skinCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _skinCancel = cancel;
        _skinLoadedKey = null;
        _skinView.Reset();
        _skinView.SetSkin(
            $"Skin_Z 0x{entry.NameHash:X16}",
            "Loading...",
            "Loading...",
            0,
            Array.Empty<SkinPartDisplay>(),
            -1,
            "");
        try
        {
            (SkinData Skin, SkinBinding? Binding, string? BindingError,
                SkinPartRow[] Parts, MeshGeometry?[] Geometries) result =
                await Task.Run(() =>
                {
                    Dictionary<ulong, MeshGeometry?> geometries = new();
                    MeshGeometry? Geometry(ulong mesh)
                    {
                        if (geometries.TryGetValue(mesh, out MeshGeometry? known))
                        {
                            return known;
                        }

                        MeshGeometry? geometry = null;
                        if (_meshRows.TryGetValue(mesh, out AssetEntry? row))
                        {
                            try
                            {
                                using FileStream stream = new(
                                    row.BodyArchivePath,
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.ReadWrite);
                                MeshData data = MeshDecoder.Read(
                                    stream, row.BodyArchivePath, row.Row);
                                geometry = data.Streams.Count == 0
                                    ? new MeshGeometry([], [], Vector3.Zero, Vector3.Zero)
                                    : data.ToGeometry();
                            }
                            catch (InvalidDataException)
                            {
                                geometry = null;
                            }
                        }

                        geometries[mesh] = geometry;
                        return geometry;
                    }

                    int? Verts(ulong mesh) => Geometry(mesh)?.Positions.Length;

                    using FileStream stream = new(
                        entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    SkinData skin = SkinDecoder.Read(
                        stream, entry.BodyArchivePath, entry.Row, Verts);
                    SkinBinding? binding = null;
                    string? bindingError = null;
                    try
                    {
                        binding = SkinBinding.Read(
                            stream, entry.BodyArchivePath, entry.Row);
                    }
                    catch (InvalidDataException ex)
                    {
                        bindingError = ex.Message;
                    }

                    SkinPartRow[] parts = skin.Parts
                        .Select(part => new SkinPartRow(
                            part.Index,
                            part.MeshHash,
                            part.VertexCount,
                            part.Stride,
                            part.PacketCount,
                            part.PacketsParsed,
                            part.MaterialHash))
                        .ToArray();
                    MeshGeometry?[] partGeometries = skin.Parts
                        .Select(part => Geometry(part.MeshHash))
                        .ToArray();
                    return (skin, binding, bindingError, parts, partGeometries);
                }, cancel.Token);
            if (IsDisposed || cancel != _skinCancel
                || _navigation?.State.View != BrowserView.Skin
                || _navigation.State.Skin is not { } current
                || current.Hash != occurrence.Hash
                || !current.ArchivePath.Equals(
                    occurrence.ArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int part = state.SkinPart;
            if (state.Mesh is { } sourceMesh
                && (part < 0 || part >= result.Parts.Length
                    || result.Parts[part].MeshHash != sourceMesh.Hash))
            {
                for (int index = 0; index < result.Parts.Length; index++)
                {
                    if (result.Parts[index].MeshHash == sourceMesh.Hash)
                    {
                        part = index;
                        break;
                    }
                }
            }

            SkinPartDisplay[] displays = new SkinPartDisplay[result.Parts.Length];
            for (int index = 0; index < displays.Length; index++)
            {
                displays[index] = new SkinPartDisplay(
                    result.Parts[index], result.Geometries[index]);
            }

            ulong root = result.Binding?.SkeletonRoot ?? 0;
            string rootText = SkinSkeletonText.Text(result.Binding, result.BindingError);
            string skinBones = $"{result.Skin.Bones.Length:N0} skin bones";
            _skinLoadedKey = key;
            _skinView.SetSkin(
                $"Skin_Z 0x{entry.NameHash:X16}",
                $"v{result.Skin.Head.Version} · {result.Skin.MeshLinks.Length:N0} meshes · " +
                $"{result.Skin.Parts.Length:N0} parts · {skinBones} · " +
                $"{Path.GetFileName(entry.ArchivePath)}",
                rootText,
                root,
                displays,
                part,
                SourcePartNote(state, result.Parts));
            if (part != state.SkinPart)
            {
                _navigation?.SetSkinPart(part);
            }

            _status.Text =
                $"{Path.GetFileName(entry.ArchivePath)} · {result.Skin.Parts.Length:N0} parts · " +
                skinBones;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _skinCancel)
            {
                _skinView.SetSkin(
                    $"Skin_Z 0x{entry.NameHash:X16}",
                    ex.Message,
                    "",
                    0,
                    Array.Empty<SkinPartDisplay>(),
                    -1,
                    ex.Message);
            }
        }
    }

    private string SourcePartNote(
        BrowserState state,
        IReadOnlyList<SkinPartRow> parts)
    {
        if (state.Mesh is { } mesh)
        {
            for (int index = 0; index < parts.Count; index++)
            {
                if (parts[index].MeshHash == mesh.Hash)
                {
                    return $"Part {index} is mesh 0x{mesh.Hash:X16}, the mesh that opened this skin.";
                }
            }
        }

        return "";
    }

    private async Task OpenSkeletonAsync(ulong root)
    {
        if (_navigation?.State.View != BrowserView.Skin
            || _navigation.State.Skin is null)
        {
            return;
        }

        _navigation.SetSkinPart(_skinView.SelectedPart);
        await EnsureSkeletonCatalogAsync();
        if (IsDisposed
            || _navigation?.State.View != BrowserView.Skin
            || _navigation.State.Skin is null)
        {
            return;
        }

        if (_skeletonCatalog is null)
        {
            _skinView.SetNote("The skeleton catalog could not be read.");
            _status.Text = "skeleton catalog unavailable";
            return;
        }

        SkeletonEntry? entry = _skeletonCatalog.Entries
            .FirstOrDefault(item => item.NameHash == root);
        if (entry is null)
        {
            string missing = SkinSkeletonText.Missing(root);
            _skinView.SetNote(missing);
            _status.Text = missing;
            return;
        }

        _navigation.OpenSkeleton(new SkeletonOccurrence(
            entry.NameHash, entry.ArchivePath, entry.Offset));
        RenderState();
    }

    private async Task RenderSkeletonAsync(BrowserState state)
    {
        if (state.Skeleton is not { } occurrence)
        {
            _skeletonView.SetMessage("Skeleton", "Select a skeleton.");
            return;
        }

        if (_skeletonCatalog is null)
        {
            _skeletonView.SetMessage("Skeleton", "Skeleton catalog unavailable.");
            return;
        }

        SkeletonEntry? entry = _skeletonCatalog.Entries.FirstOrDefault(
            item => item.NameHash == occurrence.Hash
                && item.Offset == occurrence.Offset
                && item.ArchivePath.Equals(
                    occurrence.ArchivePath, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            _skeletonView.SetMessage(
                "Skeleton not found",
                $"0x{occurrence.Hash:X16} is not in the catalog.");
            return;
        }

        string key = $"{entry.ArchivePath}|{entry.Offset}|{entry.NameHash:X16}";
        if (_skeletonLoadedKey == key)
        {
            _status.Text =
                $"{Path.GetFileName(entry.ArchivePath)} · {entry.NameHash:X16} skeleton";
            PrepareSkeletonUsedBy();
            return;
        }

        _skeletonDecodeCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _skeletonDecodeCancel = cancel;
        _skeletonLoadedKey = null;
        _skeletonView.SetMessage($"Skel_Z 0x{entry.NameHash:X16}", "Loading...");
        try
        {
            SkeletonData data = await Task.Run(() =>
            {
                using FileStream stream = new(
                    entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return SkeletonDecoder.Read(stream, entry.ArchivePath, entry.Hit);
            }, cancel.Token);
            if (IsDisposed || cancel != _skeletonDecodeCancel
                || _navigation?.State.View != BrowserView.Skeleton
                || _navigation.State.Skeleton is not { } current
                || current.Hash != occurrence.Hash
                || current.Offset != occurrence.Offset)
            {
                return;
            }

            _skeletonLoadedKey = key;
            _skeletonView.SetSkeleton(
                $"Skel_Z 0x{entry.NameHash:X16}",
                $"{Path.GetFileName(entry.ArchivePath)} · offset {entry.Offset:N0} · " +
                $"v{data.Head.Version} · {data.Bones.Count:N0} bones",
                data);
            _status.Text =
                $"{Path.GetFileName(entry.ArchivePath)} · {data.Bones.Count:N0} bones";
            PrepareSkeletonUsedBy();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _skeletonDecodeCancel)
            {
                _skeletonView.SetMessage(
                    $"Skel_Z 0x{entry.NameHash:X16}", "Could not read this skeleton.");
                _status.Text = ex.Message;
            }
        }
    }

    private async Task EnsureSkinLinksAsync()
    {
        if (_skinLinks is not null)
        {
            return;
        }

        _skinLinksTask ??= LoadSkinLinksAsync();
        await _skinLinksTask;
    }

    private async Task LoadSkinLinksAsync()
    {
        if (_profile is null)
        {
            return;
        }

        IReadOnlyList<AssetEntry> assets = _assets;
        _skinLinksCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _skinLinksCancel = cancel;
        _status.Text = "Indexing skins...";
        try
        {
            Progress<CatalogProgress> progress = new(value =>
            {
                if (!IsDisposed && cancel == _skinLinksCancel)
                {
                    _status.Text =
                        $"Indexing skins {value.Index:N0}/{value.Total:N0}";
                }
            });
            SkinLinkIndex index = await Task.Run(
                () => SkinLinkIndex.Load(assets, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _skinLinksCancel)
            {
                return;
            }

            _skinLinks = index;
            _status.Text =
                $"{index.Entries.Count:N0} skins · {index.ByMesh.Count:N0} meshes";
            UpdateMeshUsedBy();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _skinLinksCancel)
            {
                _status.Text = ex.Message;
            }
        }
        finally
        {
            if (cancel == _skinLinksCancel)
            {
                _skinLinksTask = null;
            }
        }
    }

    private async Task EnsureSkeletonCatalogAsync()
    {
        if (_skeletonCatalog is not null)
        {
            return;
        }

        _skeletonTask ??= LoadSkeletonCatalogAsync();
        await _skeletonTask;
    }

    private async Task LoadSkeletonCatalogAsync()
    {
        if (_profile is null)
        {
            return;
        }

        GameProfile profile = _profile;
        _skeletonCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _skeletonCancel = cancel;
        _status.Text = "Indexing skeletons...";
        try
        {
            Progress<CatalogProgress> progress = new(value =>
            {
                if (!IsDisposed && cancel == _skeletonCancel)
                {
                    _status.Text =
                        $"Indexing skeletons {value.Index:N0}/{value.Total:N0}";
                }
            });
            SkeletonCatalog catalog = await Task.Run(
                () => SkeletonCatalog.Load(profile, false, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _skeletonCancel)
            {
                return;
            }

            _skeletonCatalog = catalog;
            _status.Text =
                $"{catalog.Entries.Count:N0} skeletons · {catalog.UniqueNameCount:N0} names";
            if (_navigation is not null)
            {
                SetTypesFor(
                    _navigation.State.ArchivePath,
                    TypeName(_navigation.State));
                if (_navigation.State.View == BrowserView.Skeletons)
                {
                    RenderState();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _skeletonCancel)
            {
                _status.Text = ex.Message;
            }
        }
        finally
        {
            if (cancel == _skeletonCancel)
            {
                _skeletonTask = null;
            }
        }
    }

    private void UpdateMeshUsedBy()
    {
        if (_navigation?.State.Mesh is not { } mesh)
        {
            _meshInspector.SetUsedBy(0);
            return;
        }

        if (_skinLinks is null)
        {
            _meshInspector.SetUsedByPending();
            return;
        }

        int count = _skinLinks.ByMesh.TryGetValue(
            mesh.Hash, out IReadOnlyList<SkinLinkEntry>? uses)
            ? uses.Count
            : 0;
        _meshInspector.SetUsedBy(count);
    }

    private AssetEntry? FindMeshEntry(MeshOccurrence occurrence) =>
        _assets.FirstOrDefault(entry =>
            entry.ClassHash == AssetClass.Mesh
            && entry.NameHash == occurrence.Hash
            && entry.ArchivePath.Equals(occurrence.ArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.BodyArchivePath.Equals(
                occurrence.BodyArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.Row.BodyOffset == occurrence.BodyOffset);

    private AssetEntry? FindSkinEntry(SkinOccurrence occurrence) =>
        _assets.FirstOrDefault(entry =>
            entry.ClassHash == SkinHead.SkinClass
            && entry.NameHash == occurrence.Hash
            && entry.ArchivePath.Equals(occurrence.ArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.BodyArchivePath.Equals(
                occurrence.BodyArchivePath, StringComparison.OrdinalIgnoreCase)
            && entry.Row.BodyOffset == occurrence.BodyOffset);

    private static MeshOccurrence ToMesh(AssetEntry entry) => new(
        entry.NameHash,
        entry.ArchivePath,
        entry.BodyArchivePath,
        entry.Row.BodyOffset);

    private static SkinOccurrence ToSkin(AssetEntry entry) => new(
        entry.NameHash,
        entry.ArchivePath,
        entry.BodyArchivePath,
        entry.Row.BodyOffset);

    private async Task LoadAudioAsync()
    {
        if (_profile is null || _audioLoaded || _audioLoading)
        {
            return;
        }

        GameProfile profile = _profile;
        _audioLoading = true;
        _audioCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _audioCancel = cancel;
        _status.Text = "Scanning SOUNDBANKS...";
        try
        {
            Progress<CatalogProgress> progress = new(value =>
            {
                if (!IsDisposed && cancel == _audioCancel)
                {
                    _status.Text =
                        $"Scanning SOUNDBANKS {Path.GetFileName(value.ArchivePath)} " +
                        $"{value.Index}/{value.Total}";
                }
            });
            AudioCatalog catalog = await Task.Run(
                () => AudioCatalog.Load(profile, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _audioCancel)
            {
                return;
            }

            _audio = catalog.Entries;
            _audioLoaded = true;
            _status.Text =
                $"{_audio.Count:N0} audio media · {catalog.Archives.Count:N0} packs · " +
                $"{catalog.Previews:N0} previews skipped · {catalog.Failures.Count:N0} failed";
            if (_navigation is not null)
            {
                SetTypesFor(
                    _navigation.State.ArchivePath,
                    TypeName(_navigation.State));
                if (_navigation.State.View == BrowserView.Audio)
                {
                    RenderState();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _audioCancel)
            {
                _status.Text = ex.Message;
                _resultsBody.Text = ex.Message;
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

    private void StartAudioList(BrowserState state)
    {
        _audioListCancel?.Cancel();
        CancellationTokenSource cancel = new();
        _audioListCancel = cancel;
        int version = ++_audioListVersion;
        _audioListBuilding = true;
        _resultsCount.Text = "";
        _resultsBody.Text = $"SOUNDBANKS · {AudioFilterLabel(state)} · filtering...";
        _status.Text = "Filtering audio...";
        IProgress<int> progress = new Progress<int>(count =>
        {
            if (IsDisposed || !_audioListBuilding
                || version != _audioListVersion || cancel != _audioListCancel)
            {
                return;
            }

            _resultsCount.Text = $"{count:N0}+";
            _resultsBody.Text =
                $"SOUNDBANKS · {AudioFilterLabel(state)} · filtering... " +
                $"{count:N0} matches";
            _status.Text = $"Filtering audio... {count:N0} matches";
        });
        _ = BuildAudioListAsync(state, cancel, version, progress);
    }

    private async Task BuildAudioListAsync(
        BrowserState state,
        CancellationTokenSource cancel,
        int version,
        IProgress<int> progress)
    {
        try
        {
            List<AssetRow> rows = await Task.Run(
                () => AudioListBuilder.Build(
                    _audio,
                    state.AudioPack,
                    state.AudioLanguage,
                    state.Query,
                    state.AudioSort,
                    state.AudioSortDescending,
                    cancel.Token,
                    progress),
                cancel.Token);
            if (IsDisposed || version != _audioListVersion || cancel != _audioListCancel)
            {
                return;
            }

            _audioListBuilding = false;
            ApplyAudioRows(rows);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidDataException)
        {
            if (!IsDisposed && version == _audioListVersion && cancel == _audioListCancel)
            {
                _audioListBuilding = false;
                _status.Text = ex.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_audioListCancel, cancel))
            {
                _audioListCancel = null;
            }

            cancel.Dispose();
        }
    }

    private void ApplyAudioRows(IReadOnlyList<AssetRow> rows)
    {
        if (_navigation is null || _navigation.State.View != BrowserView.Audio)
        {
            return;
        }

        BrowserState current = _navigation.State;
        string? keep = current.Audio is null ? null : BrowserKeys.Audio(current.Audio);
        _textureList.SetRows(rows, keep);
        bool keepFound = keep is not null
            && _textureList.SelectedRow?.Key.Equals(keep, StringComparison.Ordinal) == true;
        if (current.Audio is not null && !keepFound)
        {
            _audioView.Reset();
            _navigation.ClearAudio();
        }
        else if (current.Audio is null)
        {
            if (rows.Count > 0)
            {
                _textureList.SelectedIndex = 0;
                OnAudioSelected();
            }
            else
            {
                _audioView.Reset();
            }
        }
        else if (_audioView.Entry is null)
        {
            _audioView.SetAudio(current.Audio);
        }

        _textureList.TopIndex = current.ListIndex;
        _textureList.EnsureVisible(_textureList.SelectedIndex);
        _resultsCount.Text = $"{rows.Count:N0}";
        _resultsBody.Text =
            $"SOUNDBANKS · {AudioFilterLabel(current)} · " +
            $"{rows.Count:N0} of {_audio.Count:N0} media · " +
            AudioText.SortLabel(current.AudioSort, current.AudioSortDescending);
        BrowserTiming.Write(
            $"audio list session {_audioView.SessionId} playing {_audioView.IsPlaying} " +
            $"pos {_audioView.Position.TotalSeconds:F2} keep={(keep ?? "none")} " +
            $"found={keepFound} rows={rows.Count}");
        UpdateStatus();
    }

    private void OnAudioSelected()
    {
        if (_navigation is null
            || _navigation.State.View != BrowserView.Audio
            || _textureList.SelectedRow?.Tag is not AudioEntry entry)
        {
            return;
        }

        _navigation.SelectAudio(entry, _textureList.TopIndex);
        if (_audioView.Entry is null
            || BrowserKeys.Audio(_audioView.Entry) != BrowserKeys.Audio(entry))
        {
            _audioView.SetAudio(entry);
        }

        UpdateStatus();
    }

    private async Task PlayAudioAsync()
    {
        if (_navigation?.State.Audio is not { } media || _audioBusy || _loading || _exporting)
        {
            return;
        }

        string key = BrowserKeys.Audio(media);
        int generation = _audioGeneration;
        _audioBusy = true;
        _audioView.SetBusy("Converting...");
        try
        {
            byte[] ogg;
            if (_audioOgg is not null && _audioOggKey == key)
            {
                ogg = _audioOgg;
            }
            else
            {
                ogg = await Task.Run(() => ConvertAudio(media));
            }

            if (IsDisposed || generation != _audioGeneration)
            {
                return;
            }

            if (_navigation?.State.Audio is not { } current
                || BrowserKeys.Audio(current) != key)
            {
                _audioView.SetStatus("Ready");
                return;
            }

            _audioOgg = ogg;
            _audioOggKey = key;
            _audioView.StartPlayback(ogg);
            BrowserTiming.Write(
                $"audio play session {_audioView.SessionId} {key}");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && generation == _audioGeneration)
            {
                _audioView.ShowError(ex.Message);
            }
        }
        finally
        {
            if (generation == _audioGeneration)
            {
                _audioBusy = false;
            }
        }
    }

    private async Task ExportAudioAsync()
    {
        if (_navigation?.State.Audio is not { } media || _audioBusy
            || _loading || _exporting || _profile is not { } profile)
        {
            return;
        }

        string key = BrowserKeys.Audio(media);
        int generation = _audioGeneration;
        using SaveFileDialog dialog = new()
        {
            Filter = "Ogg Vorbis (*.ogg)|*.ogg",
            FileName = AudioText.ExportName(media),
            Title = "Export audio",
            AddExtension = true,
            DefaultExt = "ogg",
            InitialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string full = Path.GetFullPath(dialog.FileName);
        if (GameFolderGuard.IsInside(profile.Root, Path.GetDirectoryName(full)!))
        {
            _status.Text = "Refusing to write inside the game folder.";
            return;
        }

        _audioBusy = true;
        _audioView.SetBusy("Exporting...");
        try
        {
            byte[] ogg = _audioOgg is not null && _audioOggKey == key
                ? _audioOgg
                : await Task.Run(() => ConvertAudio(media));
            if (IsDisposed || generation != _audioGeneration)
            {
                return;
            }

            string? directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, ogg);
                File.Move(temp, full, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }

            if (generation != _audioGeneration)
            {
                return;
            }

            _status.Text = $"Exported {Path.GetFileName(full)}";
            _audioView.SetStatus($"Exported {Path.GetFileName(full)}");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && generation == _audioGeneration)
            {
                _status.Text = ex.Message;
                _audioView.ShowError(ex.Message);
            }
        }
        finally
        {
            if (generation == _audioGeneration)
            {
                _audioBusy = false;
            }
        }
    }

    private static byte[] ConvertAudio(AudioEntry media)
    {
        byte[] wem = new byte[(int)media.Size];
        using (FileStream stream = new(
            media.PackPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            stream.Seek(media.Offset, SeekOrigin.Begin);
            stream.ReadExactly(wem);
        }

        return WemConverter.ToOgg(wem);
    }

    private void UpdateAudioFilterButton(BrowserState state)
    {
        int active = (state.AudioPack is null ? 0 : 1)
            + (state.AudioLanguage is null ? 0 : 1);
        _filterButton.Text = active == 0 ? "Filter" : $"Filter ({active})";
    }

    private void ShowAudioFilterMenu(BrowserState state)
    {
        ContextMenuStrip menu = new()
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            ShowImageMargin = false,
            MinimumSize = new Size(220, 0),
        };

        void Add(ToolStripItemCollection items, string text, bool check, Action action)
        {
            ToolStripMenuItem item = new(text)
            {
                Checked = check,
                ForeColor = Theme.Text,
            };
            item.Click += (_, _) =>
            {
                menu.Close();
                action();
            };
            items.Add(item);
        }

        ToolStripMenuItem pack = new("Pack") { ForeColor = Theme.Text };
        Add(pack.DropDownItems, "All packs", state.AudioPack is null,
            () => ApplyAudioFilter(null, state.AudioLanguage));
        foreach (string name in _audio
            .Select(entry => entry.PackName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            Add(pack.DropDownItems, name, state.AudioPack == name,
                () => ApplyAudioFilter(name, state.AudioLanguage));
        }

        ToolStripMenuItem language = new("Language") { ForeColor = Theme.Text };
        Add(language.DropDownItems, "All languages", state.AudioLanguage is null,
            () => ApplyAudioFilter(state.AudioPack, null));
        foreach (string name in _audio
            .Select(entry => entry.Language)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            Add(language.DropDownItems, name, state.AudioLanguage == name,
                () => ApplyAudioFilter(state.AudioPack, name));
        }

        menu.Items.Add(pack);
        menu.Items.Add(language);
        menu.Items.Add(new ToolStripSeparator());
        Add(menu.Items, "Clear filters", state.AudioPack is null && state.AudioLanguage is null,
            () => ApplyAudioFilter(null, null));
        menu.Show(_filterButton, new Point(_filterButton.Width - 220, _filterButton.Height));
    }

    private void ApplyAudioFilter(string? pack, string? language)
    {
        if (_navigation is null)
        {
            return;
        }

        _navigation.SetAudioFilter(pack, language);
        _navigation.SetListIndex(0);
        RenderState();
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
            if (BrowserKeys.Texture(_inspectorCopies[index]) == BrowserKeys.Texture(occurrence))
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
            if (keys.Add(BrowserKeys.Texture(occurrence)))
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

    private void CopySelectedHash()
    {
        string? text = BrowserKeys.HashText(_textureList.SelectedRow?.Tag);
        if (text is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            _status.Text = $"Copied {text}.";
        }
        catch (ExternalException)
        {
            _status.Text = "Clipboard is busy. Try again.";
        }
    }

    private void UpdateCopyHashEnabled() =>
        _copyHash.Enabled = BrowserKeys.HashText(_textureList.SelectedRow?.Tag) is not null;

    private async Task ExportSelectedAsync()
    {
        if (_profile is not { } profile || _exporting || _loading || _audioBusy)
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

        if (!ReferenceEquals(_profile, profile) || _loading)
        {
            _status.Text = "The game changed. Export cancelled.";
            return;
        }

        if (GameFolderGuard.IsInside(profile.Root, dialog.SelectedPath))
        {
            _status.Text = "Refusing to write inside the game folder.";
            return;
        }

        string folder = dialog.SelectedPath;
        _exporting = true;
        _exportSelected.Enabled = false;
        _cancelExport.Visible = true;
        _cancelExport.Enabled = true;
        CancellationTokenSource cancel = new();
        _exportCancel = cancel;
        int completed = 0;
        int skipped = 0;
        _status.Text = $"Exporting 0/{entries.Count:N0} textures...";
        IProgress<int> progress = new Progress<int>(value =>
        {
            if (!IsDisposed && _exporting)
            {
                _status.Text = $"Exporting {value:N0}/{entries.Count:N0} textures...";
            }
        });

        try
        {
            await Task.Run(() =>
            {
                HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
                foreach (AssetEntry entry in entries)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    try
                    {
                        string name = ExportNames.Unique(
                            folder, entry.ArchivePath, entry.NameHash, used);
                        TextureExport.ExportPng(entry.Texture!, Path.Combine(folder, name));
                    }
                    catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
                        or IOException or UnauthorizedAccessException)
                    {
                        skipped++;
                    }

                    completed++;
                    progress.Report(completed);
                }
            }, cancel.Token);

            if (!IsDisposed)
            {
                _status.Text =
                    $"Exported {completed - skipped:N0}, skipped {skipped:N0}.";
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                _status.Text = $"Export cancelled after {completed:N0} of {entries.Count:N0}.";
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
                _cancelExport.Visible = false;
                UpdateSelectionCount();
            }
        }
    }

    private void ExportTexture()
    {
        if (_textureInspector.Texture is not { } texture
            || _textureInspector.Occurrence is not { } occurrence
            || _profile is not { } profile
            || _exporting
            || _loading
            || _audioBusy)
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
        if (folder is null || GameFolderGuard.IsInside(profile.Root, folder))
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
            || BrowserKeys.Texture(currentSource) != BrowserKeys.Texture(source)
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
        try
        {
            if (_materials is null)
            {
                await EnsureMaterialCatalogAsync();
            }

            if (IsDisposed || _materials is null)
            {
                return false;
            }

            _materialCancel?.Cancel();
            _materialCancel?.Dispose();
            CancellationTokenSource cancel = new();
            _materialCancel = cancel;
            IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
            {
                if (!IsDisposed && cancel == _materialCancel)
                {
                    _status.Text =
                        $"Linking {Path.GetFileName(item.ArchivePath)} {item.Index}/{item.Total}";
                }
            });
            _status.Text = "Linking materials to textures...";
            MaterialTextureIndex index = await Task.Run(
                () => MaterialTextureIndex.Load(_materials, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _materialCancel)
            {
                return false;
            }

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
            if (!IsDisposed)
            {
                _status.Text = ex.Message;
            }

            return false;
        }
        finally
        {
            _materialLoading = false;
            if (!IsDisposed)
            {
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
        if (state.MaterialError is { } error)
        {
            _materialSlots.ShowNotice("Could not read this material", true);
            _status.Text = error;
            return;
        }

        MaterialData? data = MaterialFor(state);
        if (data is null)
        {
            _materialSlots.ShowNotice("Material unavailable", true);
            return;
        }

        if (data.Slots.Count == 0)
        {
            _materialSlots.ShowNotice("No filled texture slots", false);
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

        if (_navigation.State.MaterialError is { } error)
        {
            _slotInspector.ShowNotice("Could not read this material", error, true);
            return;
        }

        MaterialData? data = MaterialFor(_navigation.State);
        if (data is null)
        {
            _slotInspector.ShowEmpty();
            return;
        }

        if (data.Slots.Count == 0)
        {
            _slotInspector.ShowNotice(
                "No filled texture slots",
                "This material has no texture slots.",
                false);
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
            if (keys.Add(BrowserKeys.Texture(occurrence)))
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
        if (_navigation is null || _loading || _listBuilding)
        {
            return;
        }

        BrowserState state = _navigation.State;
        int archiveCount = _archives.Count(archive => archive.Path.Length > 0);
        if (state.View == BrowserView.Textures)
        {
            _status.Text =
                $"{ScopeName(state.ArchivePath)} · {_textureList.Rows.Count:N0} of " +
                $"{TextureCount(state.ArchivePath):N0} textures · {archiveCount:N0} archives";
            return;
        }

        if (state.View == BrowserView.Animations)
        {
            string noun = _textureList.Rows.Count == 1 ? "clip" : "clips";
            _status.Text =
                $"{ScopeName(state.ArchivePath)} · {_textureList.Rows.Count:N0} {noun} · " +
                $"{archiveCount:N0} archives";
            return;
        }

        if (state.View == BrowserView.Meshes)
        {
            _status.Text =
                $"{ScopeName(state.ArchivePath)} · {_textureList.Rows.Count:N0} meshes · " +
                $"{archiveCount:N0} archives";
            return;
        }

        if (state.View == BrowserView.Skins)
        {
            _status.Text =
                $"{ScopeName(state.ArchivePath)} · {_textureList.Rows.Count:N0} skins · " +
                $"{archiveCount:N0} archives";
            return;
        }

        if (state.View == BrowserView.Skeletons)
        {
            _status.Text =
                $"{ScopeName(state.ArchivePath)} · {_textureList.Rows.Count:N0} skeletons · " +
                $"{archiveCount:N0} archives";
            return;
        }

        if (state.View == BrowserView.Materials)
        {
            _status.Text =
                $"{ScopeName(state.ArchivePath)} · {_textureList.Rows.Count:N0} materials · " +
                $"{archiveCount:N0} archives";
            return;
        }

        if (state.View == BrowserView.Audio)
        {
            _status.Text =
                $"SOUNDBANKS · {AudioFilterLabel(state)} · " +
                $"{_textureList.Rows.Count:N0} of {_audio.Count:N0} media · " +
                AudioText.SortLabel(state.AudioSort, state.AudioSortDescending);
            return;
        }

        if (state.View is BrowserView.Skin or BrowserView.SkinPose or BrowserView.Skeleton
            or BrowserView.AnimationLarge)
        {
            return;
        }

        if (state.View == BrowserView.Material)
        {
            if (state.MaterialError is { } error)
            {
                _status.Text = $"read error · {error}";
                return;
            }

            if (state.Material is { } material)
            {
                MaterialData? data = MaterialFor(state);
                _status.Text = data is null
                    ? $"{material.ArchiveName} · {material.Code}"
                    : $"{material.ArchiveName} · {material.Code} · {data.Slots.Count:N0} slots";
            }

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
            $"{ScopeName(state.ArchivePath)} · {TypeName(state)} · " +
            $"{ActiveCount(state.ArchivePath):N0} rows · {archiveCount:N0} archives";
    }

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
        BrowserView.AnimationLarge => "Animations",
        BrowserView.Meshes => "Meshes",
        BrowserView.Skins => "Skins",
        BrowserView.Skeletons => "Skeletons",
        BrowserView.Materials => "Materials",
        BrowserView.Material => state.MaterialFromList ? "Materials" : "Textures",
        BrowserView.LinkedTexture => state.MaterialFromList ? "Materials" : "Textures",
        BrowserView.Skin => state.Mesh is null ? "Skins" : "Meshes",
        BrowserView.SkinPose => state.Mesh is null ? "Skins" : "Meshes",
        BrowserView.Skeleton => DetailSource(state),
        BrowserView.Audio => "Audio",
        _ => "Textures",
    };

    private static string DetailSource(BrowserState state) => state switch
    {
        { Mesh: not null } => "Meshes",
        { Skin: not null } => "Skins",
        _ => "Skeletons",
    };

    private static string ScopeName(string archivePath) =>
        string.IsNullOrEmpty(archivePath) ? "All archives" : Path.GetFileName(archivePath);

    private static string AudioFilterLabel(BrowserState state)
    {
        if (state.AudioPack is null && state.AudioLanguage is null)
        {
            return "all packs and languages";
        }

        if (state.AudioPack is null)
        {
            return $"language {state.AudioLanguage}";
        }

        return state.AudioLanguage is null
            ? $"pack {state.AudioPack}"
            : $"{state.AudioPack} · {state.AudioLanguage}";
    }

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
        string archive = ScopeName(state.ArchivePath);
        bool compact = ClientSize.Width < BrowserLayout.CollapseWidth;
        return state.View switch
        {
            BrowserView.Textures => $"{archive} / Textures",
            BrowserView.Animations => $"{archive} / Animations",
            BrowserView.AnimationLarge => state.Animation is { } largeClip
                ? $"{archive} / Animations / 0x{largeClip.Hash:X16}"
                : $"{archive} / Animations",
            BrowserView.Meshes => $"{archive} / Meshes",
            BrowserView.Skins => $"{archive} / Skins",
            BrowserView.Skeletons => $"{archive} / Skeletons",
            BrowserView.Skin => state.Skin is { } skin
                ? $"{archive} / {(state.Mesh is null ? "Skins" : "Meshes")} / Skin 0x{skin.Hash:X16}"
                : $"{archive} / {(state.Mesh is null ? "Skins" : "Meshes")} / Skin",
            BrowserView.SkinPose => state.Skin is { } poseSkin
                ? $"{archive} / {(state.Mesh is null ? "Skins" : "Meshes")} / " +
                    $"Skin 0x{poseSkin.Hash:X16} / Pose"
                : $"{archive} / Skins / Pose",
            BrowserView.Skeleton => state.Skeleton is { } skeleton
                ? $"{archive} / {DetailSource(state)} / Skeleton 0x{skeleton.Hash:X16}"
                    + (_skeletonSkinsOpen ? " / Skins" : "")
                : $"{archive} / {DetailSource(state)} / Skeleton",
            BrowserView.Audio => "SOUNDBANKS / Audio",
            BrowserView.Materials => $"{archive} / Materials",
            BrowserView.Material => state.Material is { } material
                ? state.MaterialFromList
                    ? $"{archive} / Materials / 0x{material.Hash:X16}"
                    : compact
                        ? $"{archive} / 0x{material.Hash:X16}"
                        : $"{archive} / Textures / 0x{material.Hash:X16} / Materials"
                : $"{archive} / Materials",
            BrowserView.LinkedTexture => state.LinkedTexture is { } linked
                ? state.MaterialFromList
                    ? compact
                        ? $"{archive} / Materials / 0x{linked.Hash:X16}"
                        : $"{archive} / Materials / " +
                            (state.Material is { } source ? $"0x{source.Hash:X16} / " : "") +
                            $"{linked.ArchiveName} occurrence"
                            + (linked.IsExternal ? $" / data {linked.BodyArchiveName}" : "")
                    : compact
                        ? $"{archive} / {linked.ArchiveName}"
                            + (linked.IsExternal ? $" / {linked.BodyArchiveName}" : "")
                        : $"{archive} / Textures / Materials / {linked.ArchiveName} occurrence"
                            + (linked.IsExternal ? $" / data {linked.BodyArchiveName}" : "")
                : state.MaterialFromList
                    ? $"{archive} / Materials"
                    : $"{archive} / Textures / Materials",
            _ => $"{archive} / {TypeName(state)}",
        };
    }

    private string ChooserCrumbs(BrowserState state)
    {
        string archive = ScopeName(state.ArchivePath);
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
        UpdateToolbarLayout();
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
        if (!_scopeOpen && !_outer.Panel1Collapsed)
        {
            _sidebarWidth = _outer.SplitterDistance;
        }

        _outer.Panel1Collapsed = !_scopeOpen;
        _scopeButton.Text = "Scope";
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
        int inspector = _inner.Panel2Collapsed ? _inspectorWidth : _inner.Panel2.Width;
        Settings.SaveBrowserLayout(
            BrowserLayout.ClampSidebar(sidebar, ClientSize.Width),
            BrowserLayout.ClampInspector(inspector, ClientSize.Width));
    }

    private sealed record MaterialWork(
        List<MaterialRow> Rows,
        List<(string Archive, MaterialListResult Result)> Results);
}
