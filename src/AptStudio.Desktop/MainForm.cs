using AptStudio.Core.Games;

namespace AptStudio.Desktop;

internal sealed class MainForm : Form
{
    private readonly Label _pathLabel = new();
    private readonly Label _treeLabel = new();
    private readonly Label _previewLabel = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly SplitContainer _split = new();

    public MainForm()
    {
        Text = "APTStudio";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(800, 500);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        KeyPreview = true;
        KeyDown += OnKeyDown;

        Controls.Add(BuildContent());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatus());
    }

    private Control BuildToolbar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 8, 12, 8),
        };

        var open = new Button
        {
            Text = "Open Game Folder",
            AutoSize = true,
            Dock = DockStyle.Left,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Accent,
            Padding = new Padding(8, 0, 8, 0),
        };
        open.FlatAppearance.BorderSize = 0;
        open.Click += (_, _) => OpenGameFolder();

        _pathLabel.Dock = DockStyle.Fill;
        _pathLabel.TextAlign = ContentAlignment.MiddleRight;
        _pathLabel.ForeColor = Theme.Muted;
        _pathLabel.Text = "No game folder selected";

        bar.Controls.Add(_pathLabel);
        bar.Controls.Add(open);
        return bar;
    }

    private Control BuildContent()
    {
        _split.Dock = DockStyle.Fill;
        _split.SplitterWidth = 4;
        _split.BackColor = Theme.Surface;

        _treeLabel.Dock = DockStyle.Fill;
        _treeLabel.TextAlign = ContentAlignment.MiddleCenter;
        _treeLabel.ForeColor = Theme.Muted;
        _treeLabel.Text = "Open the game folder to begin.";

        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.TextAlign = ContentAlignment.MiddleCenter;
        _previewLabel.ForeColor = Theme.Muted;
        _previewLabel.Text = "Preview";

        _split.Panel1.BackColor = Theme.Background;
        _split.Panel2.BackColor = Theme.Background;
        _split.Panel1.Controls.Add(_treeLabel);
        _split.Panel2.Controls.Add(_previewLabel);
        return _split;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _split.SplitterDistance = 300;
        _split.Panel1MinSize = 220;
        _split.Panel2MinSize = 400;
    }

    private Control BuildStatus()
    {
        _statusLabel.Text = "No game folder selected.";
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Theme.Muted;
        _statusLabel.BackColor = Theme.Surface;

        var status = new StatusStrip
        {
            BackColor = Theme.Surface,
            SizingGrip = false,
        };
        status.Items.Add(_statusLabel);
        return status;
    }

    private void OpenGameFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Resonance game folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!GameProfile.TryDetect(dialog.SelectedPath, out GameProfile? profile, out string? error) || profile is null)
        {
            _statusLabel.Text = error ?? "That folder is not a supported game.";
            MessageBox.Show(this, _statusLabel.Text, "APTStudio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _pathLabel.Text = profile.Root;
        _statusLabel.Text = $"{profile.Kind} | {profile.ExecutableName}";
        _treeLabel.Text = string.Join(Environment.NewLine, profile.DataRoots);
        _treeLabel.ForeColor = Theme.Text;
        Text = $"APTStudio - {profile.Kind}";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.O)
        {
            OpenGameFolder();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }
}
