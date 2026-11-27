using System.Drawing;
using System.Globalization;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal sealed record TextureViewState(float Zoom, PointF Pan, bool Fit);

internal sealed class TextureInspector : Panel
{
    private readonly Label _type = new();
    private readonly Label _hash = new();
    private readonly Label _subtitle = new();
    private readonly Label _context = new();
    private readonly PreviewControl _preview = new();
    private readonly ComboBox _mip = new();
    private readonly ComboBox _copies = new();
    private readonly Label _copiesLabel = new();
    private readonly FlowLayoutPanel _channels = new();
    private readonly FlowLayoutPanel _copyRow = new();
    private readonly FlowLayoutPanel _actions = new();
    private readonly Button _usedBy = new();
    private readonly Button _reveal = new();
    private readonly Button _export = new();
    private readonly Button _fit = new();
    private readonly Button _actual = new();
    private readonly Button _details = new();
    private readonly Panel _detailsPanel = new();
    private readonly LoadGeneration _generation = new();
    private readonly Dictionary<ChannelView, Button> _channelButtons = new();
    private TextureOccurrence? _occurrence;
    private TextureEntry? _texture;
    private IReadOnlyList<TextureOccurrence> _copyList = Array.Empty<TextureOccurrence>();
    private int _copyIndex;
    private int _mipIndex;
    private ChannelView _channel;
    private bool _linked;
    private bool _suppress;
    private bool _busy;

    public TextureInspector()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(12, 10, 12, 10);

        _type.Text = "BITMAP_Z · TEXTURE";
        _type.ForeColor = Theme.Muted;
        _type.Font = new Font("Segoe UI", 8f);
        _type.Dock = DockStyle.Top;
        _type.Height = 16;

        _hash.Font = new Font("Consolas", 10.5f, FontStyle.Bold);
        _hash.ForeColor = Theme.Text;
        _hash.Dock = DockStyle.Top;
        _hash.Height = 22;
        _hash.AutoEllipsis = true;

        _subtitle.ForeColor = Theme.Muted;
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 20;
        _subtitle.AutoEllipsis = true;

        _context.ForeColor = Theme.Muted;
        _context.Dock = DockStyle.Top;
        _context.Height = 32;
        _context.AutoEllipsis = true;
        _context.Visible = false;

        _preview.Dock = DockStyle.Fill;
        _preview.ZoomChanged += (_, _) =>
        {
            if (!_suppress)
            {
                ViewChanged?.Invoke(this, new TextureViewState(
                    _preview.Zoom, _preview.Origin, _preview.IsFit));
            }
        };

        _mip.DropDownStyle = ComboBoxStyle.DropDownList;
        _mip.Width = 54;
        StyleCombo(_mip);
        _mip.SelectedIndexChanged += (_, _) =>
        {
            if (_suppress || _mip.SelectedIndex < 0)
            {
                return;
            }

            _mipIndex = _mip.SelectedIndex;
            if (_texture is not null && _occurrence is not null)
            {
                MipChanged?.Invoke(this, _mipIndex);
                _ = DecodeAsync(
                    _texture, _mipIndex, _preview.IsFit, _preview.Zoom, _preview.Origin);
            }
        };

        _copies.DropDownStyle = ComboBoxStyle.DropDownList;
        _copies.Width = 200;
        StyleCombo(_copies);
        _copies.SelectedIndexChanged += (_, _) =>
        {
            if (_suppress || _copies.SelectedIndex < 0)
            {
                return;
            }

            _copyIndex = _copies.SelectedIndex;
            CopyChanged?.Invoke(this, _copyIndex);
        };

        _copiesLabel.Text = "Copy";
        _copiesLabel.ForeColor = Theme.Muted;
        _copiesLabel.AutoSize = true;
        _copiesLabel.Margin = new Padding(8, 5, 2, 0);

        ConfigureChannelButtons();
        _channels.AutoSize = true;
        _channels.WrapContents = true;
        _channels.Margin = new Padding(0, 3, 0, 0);
        _channels.Controls.Add(new Label
        {
            Text = "Mip",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 5, 4, 0),
        });
        _channels.Controls.Add(_mip);
        _channels.Controls.Add(new Label
        {
            Text = "Channel",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(10, 5, 4, 0),
        });
        foreach (Button button in _channelButtons.Values)
        {
            _channels.Controls.Add(button);
        }

        _fit.Text = "Fit";
        _actual.Text = "1:1";
        StyleSmallButton(_fit);
        StyleSmallButton(_actual);
        _fit.Click += (_, _) => _preview.Fit();
        _actual.Click += (_, _) => _preview.ActualSize();
        _channels.Controls.Add(_fit);
        _channels.Controls.Add(_actual);

        _copyRow.AutoSize = true;
        _copyRow.WrapContents = false;
        _copyRow.Margin = new Padding(0, 4, 0, 0);
        _copyRow.Controls.Add(_copiesLabel);
        _copyRow.Controls.Add(_copies);

        _usedBy.Text = "Used by";
        _usedBy.FlatStyle = FlatStyle.Flat;
        _usedBy.BackColor = Theme.Accent;
        _usedBy.ForeColor = Color.FromArgb(6, 32, 43);
        _usedBy.Font = new Font("Segoe UI Semibold", 9f);
        _usedBy.AutoSize = true;
        _usedBy.Padding = new Padding(10, 3, 10, 3);
        _usedBy.UseVisualStyleBackColor = false;
        _usedBy.FlatAppearance.BorderSize = 0;
        _usedBy.Click += (_, _) => UsedByRequested?.Invoke(this, EventArgs.Empty);

        _reveal.Text = "Reveal in archive";
        StyleActionButton(_reveal);
        _reveal.Visible = false;
        _reveal.Click += (_, _) => RevealRequested?.Invoke(this, EventArgs.Empty);

        _export.Text = "Export PNG";
        StyleActionButton(_export);
        _export.Click += (_, _) => ExportRequested?.Invoke(this, EventArgs.Empty);

        _actions.AutoSize = true;
        _actions.WrapContents = true;
        _actions.Margin = new Padding(0, 6, 0, 0);
        _actions.Controls.Add(_usedBy);
        _actions.Controls.Add(_reveal);
        _actions.Controls.Add(_export);

        _details.Text = "Details";
        _details.FlatStyle = FlatStyle.Flat;
        _details.ForeColor = Theme.Muted;
        _details.BackColor = Theme.Surface;
        _details.UseVisualStyleBackColor = false;
        _details.FlatAppearance.BorderSize = 0;
        _details.AutoSize = true;
        _details.Padding = new Padding(0, 3, 0, 0);
        _details.Click += (_, _) =>
        {
            _detailsPanel.Visible = !_detailsPanel.Visible;
        };

        _detailsPanel.Dock = DockStyle.Top;
        _detailsPanel.AutoSize = true;
        _detailsPanel.Visible = false;
        _detailsPanel.BackColor = Theme.Surface;

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Theme.Surface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(HeaderPanel(), 0, 0);
        layout.Controls.Add(_preview, 0, 1);
        layout.Controls.Add(_channels, 0, 2);
        layout.Controls.Add(_copyRow, 0, 3);
        layout.Controls.Add(_actions, 0, 4);
        layout.Controls.Add(_details, 0, 5);
        layout.Controls.Add(_detailsPanel, 0, 6);
        Controls.Add(layout);
        ShowEmpty();
    }

    public event EventHandler? UsedByRequested;

    public event EventHandler? RevealRequested;

    public event EventHandler? ExportRequested;

    public event EventHandler<int>? MipChanged;

    public event EventHandler<ChannelView>? ChannelChanged;

    public event EventHandler<int>? CopyChanged;

    public event EventHandler<TextureViewState>? ViewChanged;

    public TextureOccurrence? Occurrence => _occurrence;

    public TextureEntry? Texture => _texture;

    public int CopyIndex => _copyIndex;

    public void ShowEmpty()
    {
        _generation.Begin();
        _occurrence = null;
        _texture = null;
        _copyList = Array.Empty<TextureOccurrence>();
        _usedBy.Enabled = false;
        _reveal.Visible = false;
        _copies.Visible = false;
        _copiesLabel.Visible = false;
        _type.Text = "BITMAP_Z · TEXTURE";
        _hash.Text = "";
        _subtitle.Text = "Select a texture to inspect it.";
        _context.Text = "";
        _context.Visible = false;
        _preview.ShowMessage("Select a texture.", false);
        _detailsPanel.Visible = false;
    }

    public void SetTexture(
        TextureOccurrence occurrence,
        TextureEntry texture,
        IReadOnlyList<TextureOccurrence> copies,
        int copyIndex,
        int mip,
        ChannelView channel,
        bool fit,
        float zoom,
        PointF pan,
        bool linked)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(texture);

        bool sameTexture = _texture is not null
            && _texture.ArchivePath.Equals(texture.ArchivePath, StringComparison.OrdinalIgnoreCase)
            && _texture.Row.BodyOffset == texture.Row.BodyOffset;
        int previousMip = _mipIndex;
        _occurrence = occurrence;
        _texture = texture;
        _copyList = copies;
        _copyIndex = Math.Clamp(copyIndex, 0, Math.Max(0, copies.Count - 1));
        _mipIndex = Math.Clamp(mip, 0, Math.Max(0, occurrence.Head.MipCount - 1));
        _channel = channel;
        _linked = linked;

        _type.Text = "BITMAP_Z · TEXTURE";
        _hash.Text = $"0x{occurrence.Hash:X16}";
        _subtitle.Text = Subtitle(occurrence, occurrence.Head.Width, occurrence.Head.Height);
        _usedBy.Enabled = !_busy;
        _usedBy.Text = _busy ? "Loading materials..." : "Used by";
        _reveal.Visible = linked;
        _copies.Visible = copies.Count > 1;
        _copiesLabel.Visible = copies.Count > 1;
        _suppress = true;
        try
        {
            _mip.Items.Clear();
            for (int index = 0; index < occurrence.Head.MipCount; index++)
            {
                _mip.Items.Add(index.ToString(CultureInfo.InvariantCulture));
            }

            _mip.SelectedIndex = _mipIndex;
            _copies.Items.Clear();
            foreach (TextureOccurrence copy in copies)
            {
                _copies.Items.Add(copy.IsExternal
                    ? $"{copy.ArchiveName} → {copy.BodyArchiveName}"
                    : copy.ArchiveName);
            }

            if (_copies.Items.Count > 0)
            {
                _copies.SelectedIndex = _copyIndex;
            }
        }
        finally
        {
            _suppress = false;
        }

        SetChannelButtons();
        UpdateDetails(occurrence, copies);
        if (!sameTexture || previousMip != _mipIndex)
        {
            _ = DecodeAsync(texture, _mipIndex, fit, zoom, pan);
        }
        else
        {
            ApplyView(fit, zoom, pan);
        }
    }

    public void SetBusy(bool busy)
    {
        _busy = busy;
        _usedBy.Enabled = !busy && _occurrence is not null;
        _usedBy.Text = busy ? "Loading materials..." : "Used by";
    }

    public void SetContext(string? text)
    {
        _context.Text = text ?? "";
        _context.Visible = !string.IsNullOrWhiteSpace(text);
    }

    private Control HeaderPanel()
    {
        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
        };
        header.Controls.Add(_type, 0, 0);
        header.Controls.Add(_hash, 0, 1);
        header.Controls.Add(_subtitle, 0, 2);
        header.Controls.Add(_context, 0, 3);
        return header;
    }

    private void ConfigureChannelButtons()
    {
        foreach (ChannelView channel in Enum.GetValues<ChannelView>())
        {
            Button button = new()
            {
                Text = channel switch
                {
                    ChannelView.Rgb => "RGB",
                    ChannelView.Alpha => "A",
                    ChannelView.Red => "R",
                    ChannelView.Green => "G",
                    _ => "B",
                },
                Width = channel == ChannelView.Rgb ? 38 : 26,
                Height = 23,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 1, 2, 0),
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Theme.Border;
            button.Click += (_, _) =>
            {
                _channel = channel;
                _preview.SetChannel(channel);
                SetChannelButtons();
                ChannelChanged?.Invoke(this, channel);
            };
            _channelButtons[channel] = button;
        }
    }

    private void SetChannelButtons()
    {
        foreach ((ChannelView channel, Button button) in _channelButtons)
        {
            bool selected = channel == _channel;
            button.BackColor = selected ? Theme.Selection : Theme.Surface;
            button.ForeColor = selected ? Theme.Text : Theme.Muted;
        }
    }

    private async Task DecodeAsync(
        TextureEntry texture,
        int mip,
        bool fit,
        float zoom,
        PointF pan)
    {
        int token = _generation.Begin();
        try
        {
            (Bitmap Bitmap, int Width, int Height) result = await Task.Run(() =>
            {
                using FileStream stream = new(
                    texture.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                DecodedTexture image = TextureDecoder.DecodeMip(stream, texture, mip);
                return (PreviewBitmap.Create(image), image.Width, image.Height);
            });

            if (IsDisposed || !_generation.IsCurrent(token))
            {
                result.Bitmap.Dispose();
                return;
            }

            _suppress = true;
            try
            {
                _preview.SetImage(result.Bitmap);
            }
            finally
            {
                _suppress = false;
            }

            ApplyView(fit, zoom, pan, result.Width, result.Height);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
            or IOException or UnauthorizedAccessException)
        {
            if (!IsDisposed && _generation.IsCurrent(token))
            {
                _preview.ShowMessage(ex.Message, true);
            }
        }
    }

    private void ApplyView(bool fit, float zoom, PointF pan, int width = 0, int height = 0)
    {
        _suppress = true;
        try
        {
            _preview.SetChannel(_channel);
            if (fit)
            {
                _preview.Fit();
            }
            else
            {
                _preview.SetView(zoom, pan);
            }
        }
        finally
        {
            _suppress = false;
        }

        if (width > 0)
        {
            _subtitle.Text = _occurrence is null
                ? ""
                : Subtitle(_occurrence, width, height);
        }

        ViewChanged?.Invoke(this, new TextureViewState(
            _preview.Zoom, _preview.Origin, _preview.IsFit));
    }

    private static string Subtitle(TextureOccurrence occurrence, int width, int height) =>
        $"{occurrence.ArchiveName}" +
        (occurrence.IsExternal ? $" → {occurrence.BodyArchiveName}" : "") +
        $" · {width} × {height} · {occurrence.Head.MipCount} mips";

    private void UpdateDetails(
        TextureOccurrence occurrence,
        IReadOnlyList<TextureOccurrence> copies)
    {
        _detailsPanel.Controls.Clear();
        TableLayoutPanel facts = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 7,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 4, 0, 0),
        };
        facts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddFact(facts, 0, "Archive", occurrence.ArchiveName);
        AddFact(facts, 1, "Body", occurrence.BodyArchiveName);
        AddFact(facts, 2, "Offset", occurrence.BodyOffset.ToString("N0", CultureInfo.InvariantCulture));
        AddFact(facts, 3, "Mips", occurrence.Head.MipCount.ToString(CultureInfo.InvariantCulture));
        AddFact(facts, 4, "Format", occurrence.Head.Format.DisplayName);
        AddFact(facts, 5, "Copies", copies.Count.ToString(CultureInfo.InvariantCulture));
        AddFact(facts, 6, "Storage", occurrence.Head.Storage.ToString());
        _detailsPanel.Controls.Add(facts);
    }

    private static void AddFact(TableLayoutPanel table, int row, string name, string value)
    {
        Label label = new()
        {
            Text = name,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 1, 6, 1),
        };
        Label text = new()
        {
            Text = value,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 1, 0, 1),
        };
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(label, 0, row);
        table.Controls.Add(text, 1, row);
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.BackColor = Theme.Background;
        combo.ForeColor = Theme.Text;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Height = 23;
    }

    private static void StyleSmallButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Muted;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Theme.Border;
        button.Height = 23;
        button.AutoSize = true;
        button.Margin = new Padding(2, 1, 2, 0);
    }

    private static void StyleActionButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Text;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Theme.Border;
        button.AutoSize = true;
        button.Padding = new Padding(8, 3, 8, 3);
        button.Margin = new Padding(0, 0, 4, 4);
    }
}
