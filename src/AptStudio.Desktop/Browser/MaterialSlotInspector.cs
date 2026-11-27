using System.Drawing;
using System.Globalization;
using AptStudio.Core.Materials;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal sealed class MaterialSlotInspector : Panel
{
    private readonly Label _type = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _context = new();
    private readonly PreviewControl _preview = new();
    private readonly ComboBox _copies = new();
    private readonly Label _copiesLabel = new();
    private readonly Button _open = new();
    private readonly Button _fit = new();
    private readonly Button _actual = new();
    private readonly Button _details = new();
    private readonly Panel _detailsPanel = new();
    private readonly LoadGeneration _generation = new();
    private MaterialData? _data;
    private MaterialSlot? _slot;
    private TextureOccurrence? _occurrence;
    private TextureEntry? _texture;
    private IReadOnlyList<TextureOccurrence> _copyList = Array.Empty<TextureOccurrence>();
    private int _copyIndex;
    private bool _suppress;

    public MaterialSlotInspector()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(14, 13, 14, 10);

        _type.Text = "MATERIAL_Z · SLOT";
        _type.ForeColor = BrowserVisual.Faint;
        _type.Font = BrowserVisual.Section;
        _type.Dock = DockStyle.Top;
        _type.Height = 15;

        _title.Font = BrowserVisual.Title;
        _title.ForeColor = Theme.Text;
        _title.Dock = DockStyle.Top;
        _title.Height = 24;
        _title.AutoEllipsis = true;

        _subtitle.Font = BrowserVisual.UiSmall;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 18;
        _subtitle.AutoEllipsis = true;

        _context.Font = BrowserVisual.UiSmall;
        _context.ForeColor = Theme.Muted;
        _context.Dock = DockStyle.Top;
        _context.Height = 30;
        _context.AutoEllipsis = true;

        _preview.Dock = DockStyle.Fill;
        BrowserVisual.Frame(_preview);

        _copies.DropDownStyle = ComboBoxStyle.DropDownList;
        _copies.Width = 200;
        _copies.Font = BrowserVisual.UiSmall;
        _copies.BackColor = Theme.Background;
        _copies.ForeColor = Theme.Text;
        _copies.FlatStyle = FlatStyle.Flat;
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
        _copiesLabel.Font = BrowserVisual.RowSmall;
        _copiesLabel.ForeColor = Theme.Muted;
        _copiesLabel.AutoSize = true;
        _copiesLabel.Margin = new Padding(0, 5, 4, 0);

        _fit.Text = "Fit";
        _actual.Text = "1:1";
        StyleSmall(_fit);
        StyleSmall(_actual);
        _fit.Click += (_, _) => _preview.Fit();
        _actual.Click += (_, _) => _preview.ActualSize();

        TableLayoutPanel controls = new()
        {
            Dock = DockStyle.Top,
            Height = 30,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _copies.Dock = DockStyle.Fill;
        _copies.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        controls.Controls.Add(_copiesLabel, 0, 0);
        controls.Controls.Add(_copies, 1, 0);

        FlowLayoutPanel viewRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 4, 0, 0),
        };
        viewRow.Controls.Add(_fit);
        viewRow.Controls.Add(_actual);

        _open.Text = "Open texture";
        _open.FlatStyle = FlatStyle.Flat;
        _open.BackColor = Theme.Accent;
        _open.ForeColor = BrowserVisual.PrimaryText;
        _open.Font = BrowserVisual.UiSemibold;
        _open.AutoSize = true;
        _open.MinimumSize = new Size(0, 26);
        _open.MaximumSize = new Size(int.MaxValue, 26);
        _open.Padding = new Padding(10, 3, 10, 3);
        _open.UseVisualStyleBackColor = false;
        _open.FlatAppearance.BorderSize = 0;
        _open.FlatAppearance.MouseOverBackColor = Color.FromArgb(111, 208, 251);
        _open.Click += (_, _) => OpenTextureRequested?.Invoke(this, EventArgs.Empty);

        FlowLayoutPanel actions = new()
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 8, 0, 0),
        };
        actions.Controls.Add(_open);

        _details.Text = "Details";
        _details.FlatStyle = FlatStyle.Flat;
        _details.ForeColor = Theme.Muted;
        _details.Font = BrowserVisual.RowSmall;
        _details.BackColor = Theme.Surface;
        _details.UseVisualStyleBackColor = false;
        _details.FlatAppearance.BorderSize = 0;
        _details.FlatAppearance.MouseOverBackColor = BrowserVisual.SurfaceHover;
        _details.AutoSize = true;
        _details.Click += (_, _) => _detailsPanel.Visible = !_detailsPanel.Visible;

        _detailsPanel.Dock = DockStyle.Top;
        _detailsPanel.AutoSize = true;
        _detailsPanel.Visible = false;
        _detailsPanel.BackColor = Theme.Surface;

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            BackColor = Theme.Surface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_type, 0, 0);
        layout.Controls.Add(_title, 0, 1);
        layout.Controls.Add(_subtitle, 0, 2);
        layout.Controls.Add(_context, 0, 3);
        layout.Controls.Add(_preview, 0, 4);
        layout.Controls.Add(controls, 0, 5);
        layout.Controls.Add(viewRow, 0, 6);
        layout.Controls.Add(actions, 0, 7);
        layout.Controls.Add(_details, 0, 8);
        layout.Controls.Add(_detailsPanel, 0, 9);
        Controls.Add(layout);
        ShowEmpty();
    }

    public event EventHandler? OpenTextureRequested;

    public event EventHandler<int>? CopyChanged;

    public MaterialSlot? Slot => _slot;

    public TextureOccurrence? Occurrence => _occurrence;

    public int CopyIndex => _copyIndex;

    public void ShowEmpty()
    {
        _generation.Begin();
        _data = null;
        _slot = null;
        _occurrence = null;
        _texture = null;
        _open.Enabled = false;
        _copies.Visible = false;
        _copiesLabel.Visible = false;
        _title.Text = "Material slot";
        _subtitle.Text = "Select a slot.";
        _context.Text = "";
        _preview.ShowMessage("Select a slot.", false);
        _detailsPanel.Visible = false;
    }

    public void SetSlot(
        MaterialData data,
        MaterialSlot slot,
        TextureOccurrence occurrence,
        TextureEntry texture,
        IReadOnlyList<TextureOccurrence> copies,
        int copyIndex,
        string context)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(texture);

        _data = data;
        _slot = slot;
        _occurrence = occurrence;
        _texture = texture;
        _copyList = copies;
        _copyIndex = Math.Clamp(copyIndex, 0, Math.Max(0, copies.Count - 1));
        _title.Text = $"Slot {slot.Index} · {slot.Role.Authoring}";
        _subtitle.Text =
            $"0x{slot.TextureHash:X16} · {ArchiveLabel(occurrence)} · " +
            $"{occurrence.Head.Width} × {occurrence.Head.Height} · " +
            $"{occurrence.Head.Format.DisplayName}";
        _context.Text = context;
        _open.Enabled = true;
        _copies.Visible = copies.Count > 1;
        _copiesLabel.Visible = copies.Count > 1;
        _suppress = true;
        try
        {
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

        UpdateDetails(data, slot, occurrence);
        _ = DecodeAsync(texture);
    }

    private async Task DecodeAsync(TextureEntry texture)
    {
        int token = _generation.Begin();
        try
        {
            (Bitmap Bitmap, int Width, int Height) result = await Task.Run(() =>
            {
                using FileStream stream = new(
                    texture.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                DecodedTexture image = TextureDecoder.DecodeMip(stream, texture, 0);
                return (PreviewBitmap.Create(image), image.Width, image.Height);
            });

            if (IsDisposed || !_generation.IsCurrent(token))
            {
                result.Bitmap.Dispose();
                return;
            }

            _preview.SetImage(result.Bitmap);
            _preview.Fit();
            _subtitle.Text =
                $"0x{_slot?.TextureHash:X16} · " +
                $"{(_occurrence is null ? "" : ArchiveLabel(_occurrence))} · " +
                $"{result.Width} × {result.Height} · {_occurrence?.Head.Format.DisplayName}";
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

    private void UpdateDetails(MaterialData data, MaterialSlot slot, TextureOccurrence occurrence)
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
        facts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddFact(facts, 0, "Material", $"0x{data.Head.NameHash:X16}");
        AddFact(facts, 1, "Code", data.Code);
        AddFact(facts, 2, "Binding", slot.Role.Binding);
        AddFact(facts, 3, "Archive", occurrence.ArchiveName);
        AddFact(facts, 4, "Data", occurrence.BodyArchiveName);
        AddFact(facts, 5, "Offset", occurrence.BodyOffset.ToString("N0", CultureInfo.InvariantCulture));
        AddFact(facts, 6, "Mips", occurrence.Head.MipCount.ToString(CultureInfo.InvariantCulture));
        _detailsPanel.Controls.Add(facts);
    }

    private static string ArchiveLabel(TextureOccurrence occurrence) => occurrence.ArchiveName;

    private static void AddFact(TableLayoutPanel table, int row, string name, string value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = name,
            Font = BrowserVisual.RowSmall,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 1, 6, 1),
        }, 0, row);
        table.Controls.Add(new Label
        {
            Text = value,
            Font = BrowserVisual.UiSmall,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 1, 0, 1),
        }, 1, row);
    }

    private static void StyleSmall(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Muted;
        button.Font = BrowserVisual.RowSmall;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = BrowserVisual.BorderStrong;
        button.FlatAppearance.MouseOverBackColor = BrowserVisual.SurfaceHover;
        button.Height = 24;
        button.MinimumSize = new Size(0, 24);
        button.MaximumSize = new Size(int.MaxValue, 24);
        button.AutoSize = true;
        button.Padding = new Padding(5, 2, 5, 2);
        button.Margin = new Padding(4, 0, 0, 0);
    }
}
