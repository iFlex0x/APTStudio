using System.Diagnostics;
using System.Globalization;
using System.Numerics;
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
    private readonly MeshInspector _meshInspector = new();
    private readonly MeshSkinChooserView _meshSkins = new();
    private readonly SkinView _skinView = new();
    private readonly SkeletonView _skeletonView = new();
    private readonly AudioView _audioView = new();
    private ThumbnailCache _thumbnailCache = null!;
    private MeshThumbnailCache _meshThumbnails = null!;
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
    private Control _titleLine = new();
    private bool _suppressSort;
    private int _chooserArchiveCount;
    private bool _meshSkinsOpen;
    private SkinLinkIndex? _skinLinks;
    private Task? _skinLinksTask;
    private CancellationTokenSource? _skinLinksCancel;
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
    private IReadOnlyList<AudioEntry> _audio = Array.Empty<AudioEntry>();
    private bool _audioLoaded;
    private bool _audioLoading;
    private CancellationTokenSource? _audioCancel;
    private CancellationTokenSource? _audioListCancel;
    private int _audioListVersion;
    private bool _audioListBuilding;
    private bool _audioBusy;
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
        new("Hash", 0, Flexible: true, Mono: true),
        new("Occurrences", 90, Right: true, Muted: true),
    ];
    private static readonly AssetColumn[] MeshColumns =
    [
        new("Hash", 160, Mono: true),
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
    private CancellationTokenSource? _listCancel;
    private int _listVersion;
    private bool _listBuilding;
    private BrowserNavigation? _navigation;
    private IReadOnlyList<AssetEntry> _assets = Array.Empty<AssetEntry>();
    private IReadOnlyList<ArchiveChoice> _archives = Array.Empty<ArchiveChoice>();
    private int _totalTextureCount;
    private string _lastArchive = "";
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
            _listCancel?.Cancel();
            _materialCancel?.Cancel();
            _skinLinksCancel?.Cancel();
            _skeletonCancel?.Cancel();
            _meshCancel?.Cancel();
            _skinCancel?.Cancel();
            _skeletonDecodeCancel?.Cancel();
            _audioCancel?.Cancel();
            _audioListCancel?.Cancel();
            SaveLayout();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnailCache?.Dispose();
            _meshThumbnails?.Dispose();
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

        _meshSkins.Dock = DockStyle.Fill;
        _meshSkins.Visible = false;
        _skinView.Dock = DockStyle.Fill;
        _skinView.Visible = false;
        _skeletonView.Dock = DockStyle.Fill;
        _skeletonView.Visible = false;

        _titleLine = titleLine;
        _results.Controls.Add(_chooser);
        _results.Controls.Add(_materialSlots);
        _results.Controls.Add(_meshSkins);
        _results.Controls.Add(_skinView);
        _results.Controls.Add(_skeletonView);
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
        else if (_navigation.State.View == BrowserView.Meshes)
        {
            OnMeshSelected();
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
        _meshSkins.OpenRequested += (_, row) => OpenSkin(row);
        _skinView.SkeletonRequested += async root => await OpenSkeletonAsync(root);
        _skinView.PartChanged += part =>
        {
            if (_navigation?.State.View == BrowserView.Skin)
            {
                _navigation.SetSkinPart(part);
            }
        };
        _skinView.SetActionsVisible(openMesh: false, pose: false);
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
        _listCancel?.Cancel();
        _listCancel = null;
        _listVersion++;
        _listBuilding = false;
        _skinLinksCancel?.Cancel();
        _skinLinksCancel = null;
        _skinLinks = null;
        _skinLinksTask = null;
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
        _meshSkins.SetRows([]);
        _audioCancel?.Cancel();
        _audioCancel = null;
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

            _totalTextureCount = textureCounts.Values.Sum();
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
            _animationCounts = _assets
                .Where(entry => entry.ClassHash == AssetClass.Animation)
                .GroupBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
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
            _scope.SetTypes(BuildTypes(selected), "Textures");
            _search.Enabled = true;
            _filterButton.Enabled = true;
            RenderState();
            _status.Text = $"{ScopeName(selected)} · loading textures...";
            BrowserTiming.Write(
                $"catalog {watch.ElapsedMilliseconds}ms {catalog.Archives.Count} archives " +
                $"{_assets.Count} rows");
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
                if (!_listBuilding)
                {
                    UpdateStatus();
                }
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

        return _archives.FirstOrDefault(archive => archive.Count > 0 && archive.Path.Length > 0)?.Path
            ?? _archives.FirstOrDefault(archive => archive.Path.Length > 0)?.Path
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
        int meshes = _meshCounts.GetValueOrDefault(archivePath);
        int audio = _audioLoaded ? _audio.Count : 0;
        return
        [
            new TypeChoice("Textures", textures, true),
            new TypeChoice("Animations", animations, true),
            new TypeChoice("Materials", 0, false),
            new TypeChoice("Meshes", meshes, true),
            new TypeChoice("Skins", 0, false),
            new TypeChoice("Skeletons", 0, false),
            new TypeChoice("Audio", audio, true),
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
        _meshSkinsOpen = false;
        BrowserView view = state.View switch
        {
            BrowserView.Placeholder => BrowserView.Placeholder,
            BrowserView.Animations => BrowserView.Animations,
            BrowserView.Meshes or BrowserView.Skin or BrowserView.Skeleton => BrowserView.Meshes,
            _ => BrowserView.Textures,
        };
        if (archivePath.Length == 0)
        {
            view = BrowserView.Textures;
        }

        if (archivePath.Length > 0)
        {
            _lastArchive = archivePath;
        }

        _navigation.SwitchScope(
            archivePath,
            view,
            view == BrowserView.Placeholder ? state.PlaceholderName : "");
        _scope.SetTypes(BuildTypes(archivePath), TypeName(_navigation.State));
        RenderState();
    }

    private void SwitchType(string name)
    {
        if (_navigation is null)
        {
            return;
        }

        _meshSkinsOpen = false;
        BrowserView view = name switch
        {
            "Textures" => BrowserView.Textures,
            "Animations" => BrowserView.Animations,
            "Meshes" => BrowserView.Meshes,
            "Audio" => BrowserView.Audio,
            _ => BrowserView.Placeholder,
        };
        string archive = _navigation.State.ArchivePath;
        if (name != "Textures" && string.IsNullOrEmpty(archive))
        {
            archive = _lastArchive.Length > 0 ? _lastArchive : ChooseInitialArchive();
            if (archive.Length == 0)
            {
                return;
            }
        }

        if (view == _navigation.State.View
            && archive.Equals(_navigation.State.ArchivePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _navigation.SwitchScope(
            archive,
            view,
            view == BrowserView.Placeholder ? name : "");
        _scope.SetTypes(BuildTypes(archive), TypeName(_navigation.State));
        RenderState();
    }

    private void Back()
    {
        if (_meshSkinsOpen)
        {
            CloseMeshSkins();
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

        _scope.SelectArchive(state.ArchivePath);
        _scope.SetActiveType(TypeName(state));
        _scopeButton.Text = _navigation.IsChooserOpen ? "All archives" : ScopeChip(state);
        _back.Enabled = _navigation.CanBack || _meshSkinsOpen;
        _forward.Enabled = _navigation.CanForward;
        _crumbs.Text = _navigation.IsChooserOpen ? ChooserCrumbs(state) : Crumbs(state);
        _search.PlaceholderText = state.View switch
        {
            BrowserView.Audio => "pack, language, id",
            BrowserView.Textures => "hash, dimensions",
            _ => "hash, archive",
        };
        if (_search.Text != state.Query)
        {
            _search.Text = state.Query;
        }

        bool textureView = state.View == BrowserView.Textures || state.View == BrowserView.LinkedTexture;
        bool detailView = state.View is BrowserView.Skin or BrowserView.Skeleton;
        _textureInspector.Visible = textureView;
        _slotInspector.Visible = state.View == BrowserView.Material;
        _animationInspector.Visible = state.View == BrowserView.Animations;
        _meshInspector.Visible = state.View == BrowserView.Meshes;
        _audioView.Visible = state.View == BrowserView.Audio;
        _materialSlots.Visible = state.View == BrowserView.Material
            || state.View == BrowserView.LinkedTexture;
        _skinView.Visible = state.View == BrowserView.Skin;
        _skeletonView.Visible = state.View == BrowserView.Skeleton;
        _meshSkins.Visible = false;
        _inner.Panel2Collapsed = detailView;
        _titleLine.Visible = !detailView;
        if (state.View != BrowserView.Audio && _audioView.Entry is not null)
        {
            _audioView.Reset();
        }
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
                $"{state.Texture?.ArchiveName ?? ScopeName(state.ArchivePath)} · " +
                $"{_chooser.RowCount:N0} material uses · all archives";
            return;
        }

        _chooser.Visible = false;
        _search.Enabled = state.View is BrowserView.Textures
            or BrowserView.Animations or BrowserView.Meshes or BrowserView.Audio;
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
            or BrowserView.Animations or BrowserView.Meshes or BrowserView.Audio;
        _textureToolbar.Visible = state.View is BrowserView.Textures or BrowserView.Audio;
        _resultsBody.Visible = !detailView;
        _resultsBody.Height = labelView ? 44 : 18;

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
            case BrowserView.Skin:
                _textureHost.Visible = false;
                _textureToolbar.Visible = false;
                _skinView.Visible = true;
                _ = RenderSkinAsync(state);
                break;
            case BrowserView.Skeleton:
                _textureHost.Visible = false;
                _textureToolbar.Visible = false;
                _skeletonView.Visible = true;
                _ = RenderSkeletonAsync(state);
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

        if (!_loading && !_listBuilding && !_audioListBuilding && !_audioLoading)
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
            $"{ArchiveCount(current.ArchivePath):N0} textures · {SortLabel(current)}";
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
        _selectionCount.Text = $"{count:N0} selected";
        _exportSelected.Enabled = count > 0
            && _textureList.SelectedRows.Any(row =>
                row.Tag is AssetEntry entry && entry.IsPreviewable);
    }

    private void SetListMode(BrowserView view)
    {
        bool narrow = ClientSize.Width < BrowserLayout.CollapseWidth;
        IReadOnlyList<AssetColumn> columns = view switch
        {
            BrowserView.Animations => AnimationColumns,
            BrowserView.Meshes => MeshColumns,
            BrowserView.Audio => narrow ? NarrowAudioColumns : AudioColumns,
            _ => narrow ? NarrowTextureColumns : TextureColumns,
        };
        bool textures = view == BrowserView.Textures;
        bool meshes = view == BrowserView.Meshes;
        bool audio = view == BrowserView.Audio;
        _textureHeader.Columns = columns;
        _textureList.Columns = columns;
        _textureHeader.ShowThumbnails = textures || meshes;
        _textureList.ShowThumbnails = textures || meshes;
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

    private IReadOnlyList<AssetRow> BuildMeshRows(BrowserState state)
    {
        List<AssetRow> rows = new();
        bool allArchives = string.IsNullOrEmpty(state.ArchivePath);
        string query = state.Query.Trim();
        string needle = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? query[2..]
            : query;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (AssetEntry entry in _assets)
        {
            if (entry.ClassHash != AssetClass.Mesh)
            {
                continue;
            }

            if (!allArchives
                && !entry.ArchivePath.Equals(state.ArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(BrowserKeys.Mesh(entry)))
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
                BrowserKeys.Mesh(entry),
                [
                    $"0x{entry.NameHash:X16}",
                    Path.GetFileName(entry.ArchivePath),
                ],
                entry));
        }

        rows.Sort((left, right) =>
        {
            AssetEntry a = (AssetEntry)left.Tag!;
            AssetEntry b = (AssetEntry)right.Tag!;
            int order = a.NameHash.CompareTo(b.NameHash);
            if (order == 0)
            {
                order = string.Compare(
                    a.ArchivePath, b.ArchivePath, StringComparison.OrdinalIgnoreCase);
            }

            return order == 0 ? a.Row.BodyOffset.CompareTo(b.Row.BodyOffset) : order;
        });
        return rows;
    }

    private void RenderMeshList(BrowserState state)
    {
        _meshSkins.Visible = false;
        _textureHost.Visible = true;
        _textureToolbar.Visible = false;
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

        _textureList.TopIndex = state.ListIndex;
        _textureList.EnsureVisible(_textureList.SelectedIndex);
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
        _meshSkins.SetRows(uses);
        _meshSkinsOpen = true;
        RenderState();
    }

    private void CloseMeshSkins()
    {
        _meshSkinsOpen = false;
        RenderState();
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
        _skinView.SetActionsVisible(openMesh: false, pose: false);
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
        _skinView.SetSkin($"Skin_Z 0x{entry.NameHash:X16}", "Loading...", [], 0);
        try
        {
            (SkinData Skin, SkinBinding? Binding, string? BindingError, SkinPartRow[] Parts) result =
                await Task.Run(() =>
                {
                    Dictionary<ulong, int?> counts = new();
                    int? Verts(ulong mesh)
                    {
                        if (counts.TryGetValue(mesh, out int? known))
                        {
                            return known;
                        }

                        int? count = null;
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
                                count = data.Streams.Count > 0
                                    ? data.Streams[0].VertexCount
                                    : 0;
                            }
                            catch (InvalidDataException)
                            {
                                count = null;
                            }
                        }

                        counts[mesh] = count;
                        return count;
                    }

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
                    return (skin, binding, bindingError, parts);
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

            ulong root = result.Binding?.SkeletonRoot ?? 0;
            string rootText = SkinSkeletonText.Text(result.Binding, result.BindingError);
            _skinLoadedKey = key;
            _skinView.SetSkin(
                $"Skin_Z 0x{entry.NameHash:X16}",
                $"v{result.Skin.Head.Version} · {result.Skin.MeshLinks.Length:N0} meshes · " +
                $"{result.Skin.Parts.Length:N0} parts · {result.Skin.Bones.Length:N0} bones · " +
                $"{rootText} · {Path.GetFileName(entry.ArchivePath)}",
                result.Parts,
                root);
            _skinView.SelectPart(part);
            if (part != state.SkinPart)
            {
                _navigation?.SetSkinPart(part);
            }

            _skinView.SetNote(SourcePartNote(state, result.Parts, result.BindingError, root));
            _status.Text =
                $"{Path.GetFileName(entry.ArchivePath)} · {result.Skin.Parts.Length:N0} parts · " +
                $"{rootText}";
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
                    [],
                    0);
                _skinView.SetNote(ex.Message);
            }
        }
    }

    private string SourcePartNote(
        BrowserState state,
        IReadOnlyList<SkinPartRow> parts,
        string? bindingError,
        ulong root)
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

        if (bindingError is not null)
        {
            return "The skin binding block could not be read.";
        }

        return root == 0
            ? "No explicit skeleton binding."
            : "Select a part, then Open Skeleton.";
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
            item => item.NameHash == occurrence.Hash && item.Offset == occurrence.Offset);
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
                () => SkinLinkIndex.Load(_assets, progress, cancel.Token),
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
                () => SkeletonCatalog.Load(_profile, false, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _skeletonCancel)
            {
                return;
            }

            _skeletonCatalog = catalog;
            _status.Text =
                $"{catalog.Entries.Count:N0} skeletons · {catalog.UniqueNameCount:N0} names";
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
                () => AudioCatalog.Load(_profile, progress, cancel.Token),
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
                _scope.SetTypes(
                    BuildTypes(_navigation.State.ArchivePath),
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
        if (_navigation?.State.Audio is not { } media || _audioBusy)
        {
            return;
        }

        string key = BrowserKeys.Audio(media);
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

            if (IsDisposed)
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
            if (!IsDisposed)
            {
                _audioView.ShowError(ex.Message);
            }
        }
        finally
        {
            _audioBusy = false;
        }
    }

    private async Task ExportAudioAsync()
    {
        if (_navigation?.State.Audio is not { } media || _audioBusy)
        {
            return;
        }

        string key = BrowserKeys.Audio(media);
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
        if (_profile is not null
            && GameFolderGuard.IsInside(_profile.Root, Path.GetDirectoryName(full)!))
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

            _status.Text = $"Exported {Path.GetFileName(full)}";
            _audioView.SetStatus($"Exported {Path.GetFileName(full)}");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed)
            {
                _status.Text = ex.Message;
                _audioView.ShowError(ex.Message);
            }
        }
        finally
        {
            _audioBusy = false;
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
                $"{ArchiveCount(state.ArchivePath):N0} textures · {archiveCount:N0} archives";
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

        if (state.View == BrowserView.Audio)
        {
            _status.Text =
                $"SOUNDBANKS · {AudioFilterLabel(state)} · " +
                $"{_textureList.Rows.Count:N0} of {_audio.Count:N0} media · " +
                AudioText.SortLabel(state.AudioSort, state.AudioSortDescending);
            return;
        }

        if (state.View is BrowserView.Skin or BrowserView.Skeleton)
        {
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
            $"{ScopeName(state.ArchivePath)} · {TypeName(state)} · " +
            $"{ArchiveCount(state.ArchivePath):N0} rows · {archiveCount:N0} archives";
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
        BrowserView.Meshes or BrowserView.Skin or BrowserView.Skeleton => "Meshes",
        BrowserView.Audio => "Audio",
        BrowserView.Placeholder => state.PlaceholderName,
        _ => "Textures",
    };

    private static string ScopeName(string archivePath) =>
        string.IsNullOrEmpty(archivePath) ? "All archives" : Path.GetFileName(archivePath);

    private static string ScopeChip(BrowserState state) =>
        state.View == BrowserView.Audio ? "SOUNDBANKS" : ScopeName(state.ArchivePath);

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
            BrowserView.Meshes => $"{archive} / Meshes",
            BrowserView.Skin => state.Skin is { } skin
                ? $"{archive} / Meshes / Skin 0x{skin.Hash:X16}"
                : $"{archive} / Meshes / Skin",
            BrowserView.Skeleton => state.Skeleton is { } skeleton
                ? $"{archive} / Meshes / Skeleton 0x{skeleton.Hash:X16}"
                : $"{archive} / Meshes / Skeleton",
            BrowserView.Audio => "SOUNDBANKS / Audio",
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
