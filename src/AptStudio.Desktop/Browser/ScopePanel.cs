namespace AptStudio.Desktop.Browser;

internal sealed record ArchiveChoice(string Path, int Count)
{
    public string Name => System.IO.Path.GetFileName(Path);

    public string Display => Count > 0 ? $"{Name}  {Count:N0}" : Name;

    public override string ToString() => Name;
}

internal sealed record TypeChoice(string Name, int Count, bool Wired)
{
    public string Display => Count > 0 ? $"{Name}  {Count:N0}" : Name;
}

internal sealed class ScopePanel : Panel
{
    private readonly Label _game = new();
    private readonly Label _gameSub = new();
    private readonly Button _openGame = new();
    private readonly ListBox _archives = new();
    private readonly FlowLayoutPanel _types = new();
    private readonly Button _legacy = new();
    private readonly Dictionary<string, Button> _typeButtons = new(StringComparer.Ordinal);
    private readonly ToolTip _tip = new();
    private bool _suppress;

    public ScopePanel()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(8);

        _game.Text = "No game folder";
        _game.Font = new Font("Segoe UI Semibold", 9.5f);
        _game.ForeColor = Theme.Text;
        _game.AutoEllipsis = true;
        _game.Dock = DockStyle.Top;
        _game.Height = 22;

        _gameSub.Text = "Open a supported game folder.";
        _gameSub.ForeColor = Theme.Muted;
        _gameSub.AutoEllipsis = true;
        _gameSub.Dock = DockStyle.Top;
        _gameSub.Height = 18;

        _openGame.Text = "Open Game Folder";
        _openGame.FlatStyle = FlatStyle.Flat;
        _openGame.BackColor = Theme.Surface;
        _openGame.ForeColor = Theme.Accent;
        _openGame.UseVisualStyleBackColor = false;
        _openGame.FlatAppearance.BorderSize = 0;
        _openGame.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _openGame.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _openGame.Dock = DockStyle.Top;
        _openGame.Height = 26;
        _openGame.Margin = new Padding(0, 4, 0, 8);
        _openGame.Click += (_, _) => OpenGameRequested?.Invoke(this, EventArgs.Empty);

        _archives.BorderStyle = BorderStyle.None;
        _archives.BackColor = Theme.Surface;
        _archives.ForeColor = Theme.Text;
        _archives.DrawMode = DrawMode.OwnerDrawFixed;
        _archives.ItemHeight = 22;
        _archives.IntegralHeight = false;
        _archives.HorizontalScrollbar = true;
        _archives.Dock = DockStyle.Fill;
        _archives.DrawItem += OnArchiveDraw;
        _archives.SelectedIndexChanged += OnArchiveSelected;
        _archives.MouseMove += OnArchiveMouseMove;

        _types.AutoScroll = true;
        _types.FlowDirection = FlowDirection.TopDown;
        _types.WrapContents = false;
        _types.Dock = DockStyle.Fill;
        _types.BackColor = Theme.Surface;
        _types.SizeChanged += (_, _) => ResizeTypeButtons();

        _legacy.Text = "Open legacy browser";
        _legacy.FlatStyle = FlatStyle.Flat;
        _legacy.BackColor = Theme.Surface;
        _legacy.ForeColor = Theme.Muted;
        _legacy.UseVisualStyleBackColor = false;
        _legacy.FlatAppearance.BorderSize = 0;
        _legacy.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _legacy.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _legacy.Dock = DockStyle.Bottom;
        _legacy.Height = 26;
        _legacy.Margin = new Padding(0, 8, 0, 0);
        _legacy.Click += (_, _) => LegacyRequested?.Invoke(this, EventArgs.Empty);

        TableLayoutPanel gameBlock = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
        };
        gameBlock.Controls.Add(_openGame, 0, 0);
        gameBlock.Controls.Add(_game, 0, 1);
        gameBlock.Controls.Add(_gameSub, 0, 2);
        gameBlock.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Theme.Surface }, 0, 3);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.Controls.Add(gameBlock, 0, 0);
        root.Controls.Add(_archives, 0, 1);
        root.Controls.Add(_types, 0, 2);
        root.Controls.Add(_legacy, 0, 3);
        Controls.Add(root);
    }

    public event Action<string>? ArchiveSelected;

    public event Action<string>? TypeSelected;

    public event EventHandler? OpenGameRequested;

    public event EventHandler? LegacyRequested;

    public void SetGame(string name, string version)
    {
        _game.Text = name;
        _gameSub.Text = version;
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
                    break;
                }
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    public void SetTypes(IReadOnlyList<TypeChoice> types, string active)
    {
        _types.SuspendLayout();
        try
        {
            _types.Controls.Clear();
            _typeButtons.Clear();
            foreach (TypeChoice type in types)
            {
                Button button = new()
                {
                    Text = type.Display,
                    Tag = type.Name,
                    Width = Math.Max(80, _types.ClientSize.Width - 4),
                    Height = 26,
                    Margin = new Padding(0, 0, 0, 2),
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(7, 0, 0, 0),
                    UseVisualStyleBackColor = false,
                };
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
                button.FlatAppearance.MouseDownBackColor = Theme.Selection;
                button.Click += (_, _) => TypeSelected?.Invoke((string)button.Tag);
                _types.Controls.Add(button);
                _typeButtons[type.Name] = button;
            }

            SetActiveType(active);
            ResizeTypeButtons();
        }
        finally
        {
            _types.ResumeLayout();
        }
    }

    public void SetActiveType(string active)
    {
        foreach ((string name, Button button) in _typeButtons)
        {
            bool selected = name.Equals(active, StringComparison.Ordinal);
            button.BackColor = selected ? Theme.Selection : Theme.Surface;
            button.ForeColor = selected ? Theme.Text : Theme.Muted;
        }
    }

    private void ResizeTypeButtons()
    {
        int width = Math.Max(
            80,
            _types.ClientSize.Width - 4
                - (_types.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
        foreach (Button button in _typeButtons.Values)
        {
            button.Width = width;
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
        if (index >= 0 && index < _archives.Items.Count
            && _archives.Items[index] is ArchiveChoice choice)
        {
            _tip.SetToolTip(_archives, choice.Name);
        }
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
    {        if (e.Index < 0 || e.Index >= _archives.Items.Count
            || _archives.Items[e.Index] is not ArchiveChoice choice)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) != 0;
        using SolidBrush background = new(selected ? Theme.Selection : Theme.Surface);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            choice.Name,
            Font,
            new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 70, e.Bounds.Height),
            selected ? Theme.Text : Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (choice.Count > 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                choice.Count.ToString("N0"),
                Font,
                new Rectangle(e.Bounds.Right - 64, e.Bounds.Y, 60, e.Bounds.Height),
                selected ? Theme.Text : Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}
