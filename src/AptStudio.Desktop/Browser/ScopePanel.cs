using System.ComponentModel;

namespace AptStudio.Desktop.Browser;

internal sealed record ArchiveChoice(string Path, int Count, string? Label = null)
{
    public string Name => Label ?? (string.IsNullOrEmpty(Path)
        ? "All archives"
        : System.IO.Path.GetFileName(Path));

    public override string ToString() => Name;
}

internal sealed record TypeChoice(string Name, int Count);

internal sealed class ScopePanel : Panel
{
    private readonly Label _game = new();
    private readonly Label _gameSub = new();
    private readonly Label _gameDot = new();
    private readonly Label _openGame = new();
    private readonly Label _rescan = new();
    private readonly ListBox _archives = new();
    private readonly FlowLayoutPanel _types = new();
    private readonly List<ScopeRow> _typeRows = new();
    private readonly Dictionary<(string Text, int Width), string> _archiveLabels = new();
    private readonly ToolTip _tip = new();
    private int _hoverIndex = -1;
    private bool _suppress;

    public ScopePanel()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(10, 12, 10, 8);

        _gameDot.Text = "●";
        _gameDot.Font = new Font("Segoe UI", 7f);
        _gameDot.ForeColor = Theme.Accent;
        _gameDot.TextAlign = ContentAlignment.MiddleCenter;
        _gameDot.Dock = DockStyle.Fill;
        _gameDot.Margin = new Padding(2, 0, 0, 0);

        _game.Text = "No game folder";
        _game.Font = BrowserVisual.Row;
        _game.ForeColor = Theme.Text;
        _game.AutoEllipsis = true;
        _game.Dock = DockStyle.Fill;
        _game.TextAlign = ContentAlignment.MiddleLeft;
        _game.Margin = new Padding(4, 0, 0, 0);

        TableLayoutPanel gameLine = new()
        {
            Dock = DockStyle.Top,
            Height = 22,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
        };
        gameLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
        gameLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gameLine.Controls.Add(_gameDot, 0, 0);
        gameLine.Controls.Add(_game, 1, 0);

        _gameSub.Text = "Open a supported game folder.";
        _gameSub.Font = BrowserVisual.RowSmall;
        _gameSub.ForeColor = BrowserVisual.Faint;
        _gameSub.AutoEllipsis = true;
        _gameSub.Dock = DockStyle.Top;
        _gameSub.Height = 18;
        _gameSub.Padding = new Padding(18, 0, 0, 0);
        _gameSub.TextAlign = ContentAlignment.MiddleLeft;

        _openGame.Text = "Open game folder";
        _openGame.Font = BrowserVisual.RowSmall;
        _openGame.ForeColor = Theme.Accent;
        _openGame.BackColor = Theme.Surface;
        _openGame.AutoSize = true;
        _openGame.Height = 24;
        _openGame.TextAlign = ContentAlignment.MiddleLeft;
        _openGame.AutoEllipsis = true;
        _openGame.Cursor = Cursors.Hand;
        _openGame.Margin = new Padding(0, 0, 14, 0);
        _openGame.MouseEnter += (_, _) => _openGame.ForeColor = Theme.Text;
        _openGame.MouseLeave += (_, _) => _openGame.ForeColor = Theme.Accent;
        _openGame.Click += (_, _) => OpenGameRequested?.Invoke(this, EventArgs.Empty);

        _rescan.Text = "Rescan";
        _rescan.Font = BrowserVisual.RowSmall;
        _rescan.ForeColor = Theme.Accent;
        _rescan.BackColor = Theme.Surface;
        _rescan.AutoSize = true;
        _rescan.Height = 24;
        _rescan.TextAlign = ContentAlignment.MiddleLeft;
        _rescan.Cursor = Cursors.Hand;
        _rescan.Margin = new Padding(0);
        _rescan.MouseEnter += (_, _) => _rescan.ForeColor = Theme.Text;
        _rescan.MouseLeave += (_, _) => _rescan.ForeColor = Theme.Accent;
        _rescan.Click += (_, _) => RescanRequested?.Invoke(this, EventArgs.Empty);

        FlowLayoutPanel gameActions = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        gameActions.Controls.Add(_openGame);
        gameActions.Controls.Add(_rescan);

        _archives.BorderStyle = BorderStyle.None;
        _archives.BackColor = Theme.Surface;
        _archives.ForeColor = Theme.Text;
        _archives.Font = BrowserVisual.Row;
        _archives.DrawMode = DrawMode.OwnerDrawFixed;
        _archives.ItemHeight = 26;
        _archives.IntegralHeight = true;
        _archives.HorizontalScrollbar = false;
        _archives.Dock = DockStyle.Fill;
        _archives.Margin = new Padding(0);
        _archives.DrawItem += OnArchiveDraw;
        _archives.SelectedIndexChanged += OnArchiveSelected;
        _archives.MouseMove += OnArchiveMouseMove;
        _archives.MouseLeave += (_, _) => SetHover(-1);

        _types.AutoScroll = true;
        _types.FlowDirection = FlowDirection.TopDown;
        _types.WrapContents = false;
        _types.Dock = DockStyle.Fill;
        _types.BackColor = Theme.Surface;
        _types.Margin = new Padding(0);
        _types.Padding = new Padding(0);
        _types.SizeChanged += (_, _) => ResizeTypeRows();

        Label gameLabel = BrowserVisual.CreateSectionLabel("GAME");
        Label scopeLabel = BrowserVisual.CreateSectionLabel("SCOPE");
        Label typeLabel = BrowserVisual.CreateSectionLabel("ASSET TYPE");
        typeLabel.Margin = new Padding(6, 7, 0, 3);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = Theme.Surface,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.Controls.Add(gameLabel, 0, 0);
        root.Controls.Add(gameLine, 0, 1);
        root.Controls.Add(_gameSub, 0, 2);
        root.Controls.Add(gameActions, 0, 3);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface }, 0, 4);
        root.Controls.Add(scopeLabel, 0, 5);
        root.Controls.Add(_archives, 0, 6);
        root.Controls.Add(typeLabel, 0, 7);
        root.Controls.Add(_types, 0, 8);
        Controls.Add(root);
    }

    public event Action<string>? ArchiveSelected;

    public event Action<string>? TypeSelected;

    public event EventHandler? OpenGameRequested;

    public event EventHandler? RescanRequested;

    public void SetGame(string name, string sub)
    {
        _game.Text = name;
        _gameSub.Text = sub;
    }

    public void SetArchives(IReadOnlyList<ArchiveChoice> archives, string? selectedPath)
    {
        _suppress = true;
        try
        {
            _archives.Items.Clear();
            foreach (ArchiveChoice archive in archives)
            {
                _archives.Items.Add(archive);
            }

            if (selectedPath is not null)
            {
                for (int index = 0; index < _archives.Items.Count; index++)
                {
                    if (_archives.Items[index] is ArchiveChoice choice
                        && choice.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _archives.SelectedIndex = index;
                        EnsureArchiveVisible(index);
                        break;
                    }
                }
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    public void SelectArchive(string path)
    {
        _suppress = true;
        try
        {
            for (int index = 0; index < _archives.Items.Count; index++)
            {
                if (_archives.Items[index] is ArchiveChoice choice
                    && choice.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    _archives.SelectedIndex = index;
                    EnsureArchiveVisible(index);
                    break;
                }
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    public void SetArchiveCounts(IReadOnlyDictionary<string, int> counts, string allLabel)
    {
        if (_archives.Items.Count == 0)
        {
            return;
        }

        int selected = _archives.SelectedIndex;
        _suppress = true;
        _archives.BeginUpdate();
        try
        {
            for (int index = 0; index < _archives.Items.Count; index++)
            {
                if (_archives.Items[index] is not ArchiveChoice choice)
                {
                    continue;
                }

                ArchiveChoice next = choice.Path.Length == 0
                    ? new ArchiveChoice(
                        "",
                        counts.GetValueOrDefault(""),
                        allLabel == "All archives" ? null : allLabel)
                    : choice with { Count = counts.GetValueOrDefault(choice.Path) };
                if (next.Count != choice.Count || next.Label != choice.Label)
                {
                    _archives.Items[index] = next;
                }
            }

            _archives.SelectedIndex = selected;
            EnsureArchiveVisible(selected);
        }
        finally
        {
            _archives.EndUpdate();
            _suppress = false;
        }

        _archiveLabels.Clear();
        _archives.Invalidate();
    }

    private void EnsureArchiveVisible(int index)
    {
        if (index < 0 || _archives.Items.Count == 0 || _archives.ClientSize.Height <= 0)
        {
            return;
        }

        int visible = Math.Max(1, _archives.ClientSize.Height / _archives.ItemHeight);
        if (index < _archives.TopIndex)
        {
            _archives.TopIndex = index;
        }
        else if (index >= _archives.TopIndex + visible)
        {
            _archives.TopIndex = Math.Max(0, index - visible + 1);
        }
    }

    public void SetTypes(IReadOnlyList<TypeChoice> types, string active)
    {
        _types.SuspendLayout();
        try
        {
            BrowserVisual.DisposeChildren(_types);
            _typeRows.Clear();
            foreach (TypeChoice type in types)
            {
                ScopeRow row = new()
                {
                    Text = type.Name,
                    Count = type.Count,
                    ShowCount = type.Count > 0,
                    Width = RowWidth(),
                    Margin = new Padding(0, 0, 0, 1),
                };
                TypeChoice captured = type;
                row.Clicked += () => TypeSelected?.Invoke(captured.Name);
                _types.Controls.Add(row);
                _typeRows.Add(row);
            }

            SetActiveType(active);
            ResizeTypeRows();
        }
        finally
        {
            _types.ResumeLayout();
        }
    }

    public void SetActiveType(string active)
    {
        for (int index = 0; index < _typeRows.Count; index++)
        {
            ScopeRow row = _typeRows[index];
            row.Active = row.Text.Equals(active, StringComparison.Ordinal);
        }
    }

    private int RowWidth()
    {
        int width = _types.ClientSize.Width - 2;
        if (_types.VerticalScroll.Visible)
        {
            width -= SystemInformation.VerticalScrollBarWidth;
        }

        return Math.Max(80, width);
    }

    private void ResizeTypeRows()
    {
        int width = RowWidth();
        foreach (ScopeRow row in _typeRows)
        {
            row.Width = width;
        }
    }

    private void OnArchiveSelected(object? sender, EventArgs e)
    {
        if (_suppress || _archives.SelectedItem is not ArchiveChoice choice)
        {
            return;
        }

        _tip.SetToolTip(_archives, choice.Name);
        ArchiveSelected?.Invoke(choice.Path);
    }

    private void OnArchiveMouseMove(object? sender, MouseEventArgs e)
    {
        int index = _archives.IndexFromPoint(e.Location);
        SetHover(index);
        if (index >= 0 && index < _archives.Items.Count
            && _archives.Items[index] is ArchiveChoice choice)
        {
            _tip.SetToolTip(_archives, choice.Name);
        }
    }

    private void SetHover(int index)
    {
        if (_hoverIndex == index)
        {
            return;
        }

        _hoverIndex = index;
        _archives.Invalidate();
    }

    private string ArchiveLabel(string name, int width)
    {
        (string Text, int Width) key = (name, width);
        if (_archiveLabels.TryGetValue(key, out string? label))
        {
            return label;
        }

        if (_archiveLabels.Count > 256)
        {
            _archiveLabels.Clear();
        }

        label = BrowserVisual.EllipsizeMiddle(name, BrowserVisual.Row, width);
        _archiveLabels[key] = label;
        return label;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnArchiveDraw(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _archives.Items.Count
            || _archives.Items[e.Index] is not ArchiveChoice choice)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool hover = e.Index == _hoverIndex && !selected;
        Color background = selected
            ? BrowserVisual.SurfaceRaised
            : hover ? BrowserVisual.SurfaceHover : Theme.Surface;
        using SolidBrush brush = new(background);
        e.Graphics.FillRectangle(brush, e.Bounds);

        if (selected)
        {
            using SolidBrush accent = new(Theme.Accent);
            e.Graphics.FillRectangle(
                accent, e.Bounds.X, e.Bounds.Y, 2, e.Bounds.Height);
            using Pen edge = new(BrowserVisual.BorderStrong);
            e.Graphics.DrawRectangle(
                edge,
                e.Bounds.X,
                e.Bounds.Y,
                e.Bounds.Width - 1,
                e.Bounds.Height - 1);
        }

        int countWidth = choice.Count >= 100_000 ? 56 : choice.Count > 0 ? 44 : 0;
        int countX = e.Bounds.Right - countWidth - 24;
        int nameWidth = Math.Max(20, countX - e.Bounds.X - 8);
        TextRenderer.DrawText(
            e.Graphics,
            ArchiveLabel(choice.Name, nameWidth),
            BrowserVisual.Row,
            new Rectangle(e.Bounds.X + 8, e.Bounds.Y, nameWidth, e.Bounds.Height),
            selected ? Theme.Text : Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (choice.Count > 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                choice.Count.ToString("N0"),
                BrowserVisual.RowSmall,
                new Rectangle(countX, e.Bounds.Y, countWidth, e.Bounds.Height),
                selected ? Theme.Muted : BrowserVisual.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}

internal sealed class ScopeRow : Control
{
    private bool _active;
    private bool _hover;

    public ScopeRow()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        Height = 26;
        TabStop = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.StandardClick, true);
    }

    public event Action? Clicked;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Count { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowCount { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value)
            {
                return;
            }

            _active = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color background = _active
            ? BrowserVisual.SurfaceRaised
            : _hover ? BrowserVisual.SurfaceHover : Theme.Surface;
        using SolidBrush brush = new(background);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        if (_active)
        {
            using SolidBrush accent = new(Theme.Accent);
            e.Graphics.FillRectangle(accent, 0, 0, 2, ClientSize.Height);
            using Pen edge = new(BrowserVisual.BorderStrong);
            e.Graphics.DrawRectangle(edge, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
        else if (Focused)
        {
            using Pen focus = new(Theme.Accent);
            e.Graphics.DrawRectangle(focus, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        int countWidth = ShowCount ? 54 : 0;
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            BrowserVisual.Row,
            new Rectangle(8, 0, Math.Max(20, ClientSize.Width - 16 - countWidth), ClientSize.Height),
            _active ? Theme.Text : Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (ShowCount)
        {
            TextRenderer.DrawText(
                e.Graphics,
                Count.ToString("N0"),
                BrowserVisual.RowSmall,
                new Rectangle(ClientSize.Width - countWidth - 6, 0, countWidth, ClientSize.Height),
                _active ? Theme.Muted : BrowserVisual.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        Focus();
        Clicked?.Invoke();
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            Clicked?.Invoke();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}
