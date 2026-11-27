using System.Drawing;
using System.Globalization;
using AptStudio.Core.Animations;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;

namespace AptStudio.Desktop.Browser;

internal sealed class AnimationInspector : Panel
{
    private readonly Label _type = new();
    private readonly Label _hash = new();
    private readonly Label _subtitle = new();
    private readonly Label _candidate = new();
    private readonly PosePreview _preview = new();
    private readonly Button _play = new();
    private readonly Button _large = new();
    private readonly TrackBar _scrub = new();
    private readonly Label _frame = new();
    private readonly ComboBox _skin = new();
    private readonly Label _skinLabel = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };
    private readonly LoadGeneration _generation = new();
    private GameProfile? _profile;
    private IReadOnlyList<AssetEntry> _assets = Array.Empty<AssetEntry>();
    private AssetEntry? _animation;
    private IReadOnlyList<AnimationSkinCandidate> _candidates = Array.Empty<AnimationSkinCandidate>();
    private bool _playing;
    private bool _suppressScrub;
    private bool _loading;
    private float _duration;
    private int _boneCount;

    public AnimationInspector()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(12, 10, 12, 10);

        _type.Text = "ANIMATION_Z · CLIP";
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

        _candidate.ForeColor = Theme.Muted;
        _candidate.Dock = DockStyle.Top;
        _candidate.Height = 20;
        _candidate.AutoEllipsis = true;

        _preview.Dock = DockStyle.Fill;

        _play.Text = "Play";
        _play.FlatStyle = FlatStyle.Flat;
        _play.BackColor = Theme.Accent;
        _play.ForeColor = Color.FromArgb(6, 32, 43);
        _play.Font = new Font("Segoe UI Semibold", 9f);
        _play.AutoSize = true;
        _play.Padding = new Padding(10, 3, 10, 3);
        _play.UseVisualStyleBackColor = false;
        _play.FlatAppearance.BorderSize = 0;
        _play.Enabled = false;
        _play.Click += (_, _) => TogglePlay();

        _scrub.Minimum = 0;
        _scrub.Maximum = 0;
        _scrub.TickStyle = TickStyle.None;
        _scrub.AutoSize = false;
        _scrub.Height = 26;
        _scrub.Width = 160;
        _scrub.Enabled = false;
        _scrub.ValueChanged += (_, _) =>
        {
            if (_suppressScrub || _loading)
            {
                return;
            }

            if (_playing)
            {
                _playing = false;
                _timer.Stop();
                _play.Text = "Play";
            }

            _preview.SetFrame(_scrub.Value);
            UpdateFrameLabel();
        };

        _frame.Text = "frame -";
        _frame.ForeColor = Theme.Muted;
        _frame.AutoSize = true;
        _frame.Margin = new Padding(4, 7, 4, 0);

        _skinLabel.Text = "Preview skin";
        _skinLabel.ForeColor = Theme.Muted;
        _skinLabel.AutoSize = true;
        _skinLabel.Margin = new Padding(10, 7, 4, 0);

        _skin.DropDownStyle = ComboBoxStyle.DropDownList;
        _skin.Width = 210;
        _skin.BackColor = Theme.Background;
        _skin.ForeColor = Theme.Text;
        _skin.FlatStyle = FlatStyle.Flat;
        _skin.Enabled = false;
        _skin.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _skin.SelectedIndex < 0 || _skin.SelectedIndex >= _candidates.Count)
            {
                return;
            }

            UpdateCandidateLabel(_candidates[_skin.SelectedIndex]);
            _ = LoadSkinAsync(_candidates[_skin.SelectedIndex]);
        };

        _large.Text = "Large preview";
        _large.FlatStyle = FlatStyle.Flat;
        _large.BackColor = Theme.Surface;
        _large.ForeColor = Theme.Muted;
        _large.UseVisualStyleBackColor = false;
        _large.FlatAppearance.BorderSize = 1;
        _large.FlatAppearance.BorderColor = Theme.Border;
        _large.AutoSize = true;
        _large.Padding = new Padding(8, 3, 8, 3);
        _large.Enabled = false;
        _large.Click += (_, _) => OpenLargePreview();

        FlowLayoutPanel playRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = Theme.Surface,
        };
        playRow.Controls.Add(_play);
        playRow.Controls.Add(_scrub);
        playRow.Controls.Add(_frame);

        FlowLayoutPanel skinRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 3, 0, 0),
        };
        skinRow.Controls.Add(_skinLabel);
        skinRow.Controls.Add(_skin);

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 5, 0, 0),
        };
        actions.Controls.Add(_large);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Theme.Surface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
        header.Controls.Add(_candidate, 0, 3);
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_preview, 0, 1);
        layout.Controls.Add(playRow, 0, 2);
        layout.Controls.Add(skinRow, 0, 3);
        layout.Controls.Add(actions, 0, 4);
        Controls.Add(layout);

        _timer.Tick += (_, _) =>
        {
            if (!_playing || _preview.FrameCount <= 1)
            {
                return;
            }

            int next = (_preview.Frame + 1) % _preview.FrameCount;
            _preview.SetFrame(next);
            UpdateFrameLabel();
        };
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        _generation.Begin();
        _loading = false;
        _playing = false;
        _timer.Stop();
        _animation = null;
        _candidates = Array.Empty<AnimationSkinCandidate>();
        _play.Enabled = false;
        _scrub.Enabled = false;
        _skin.Enabled = false;
        _large.Enabled = false;
        _hash.Text = "";
        _subtitle.Text = "Select a clip.";
        _candidate.Text = "";
        _frame.Text = "frame -";
        _preview.ShowMessage("Select an animation clip.", false);
    }

    public void SetClip(
        AssetEntry animation,
        IReadOnlyList<AssetEntry> assets,
        GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(animation);
        if (_animation is not null
            && _animation.NameHash == animation.NameHash
            && _animation.BodyArchivePath.Equals(
                animation.BodyArchivePath, StringComparison.OrdinalIgnoreCase)
            && _animation.Row.BodyOffset == animation.Row.BodyOffset
            && _candidates.Count > 0)
        {
            return;
        }

        _animation = animation;
        _assets = assets;
        _profile = profile;
        _hash.Text = $"0x{animation.NameHash:X16}";
        _subtitle.Text = $"{Path.GetFileName(animation.ArchivePath)} · reading clip...";
        _candidate.Text = "Finding compatible skins...";
        _play.Enabled = false;
        _scrub.Enabled = false;
        _skin.Enabled = false;
        _large.Enabled = false;
        _preview.ShowMessage("Loading animation...", false);
        _ = LoadAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task LoadAsync()
    {
        if (_animation is null || _profile is null)
        {
            return;
        }

        int token = _generation.Begin();
        _loading = true;
        try
        {
            IReadOnlyList<AnimationSkinCandidate> candidates = await Task.Run(
                () => AnimationSkinCandidates.Load(_profile, _assets, _animation),
                CancellationToken.None);
            if (IsDisposed || !_generation.IsCurrent(token))
            {
                return;
            }

            _candidates = candidates;
            _skin.Items.Clear();
            foreach (AnimationSkinCandidate candidate in candidates)
            {
                _skin.Items.Add(
                    $"0x{candidate.SkinHash:X16} · {candidate.Matched}/{candidate.AnimationBones} · candidate");
            }

            AnimationData data = ReadData();
            _duration = data.Duration;
            _boneCount = data.Bones.Count;
            _subtitle.Text =
                $"{Path.GetFileName(_animation.ArchivePath)} · {data.Duration:F3} s · " +
                $"{data.Bones.Count} bones";
            if (candidates.Count == 0)
            {
                _candidate.Text = "No compatible skin.";
                _preview.ShowMessage("No compatible skin found for this clip.", true);
                return;
            }

            _skin.Enabled = true;
            _skin.SelectedIndex = 0;
            UpdateCandidateLabel(candidates[0]);
            await LoadSkinAsync(candidates[0], token);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && _generation.IsCurrent(token))
            {
                _candidate.Text = ex.Message;
                _preview.ShowMessage(ex.Message, true);
            }
        }
        finally
        {
            if (_generation.IsCurrent(token))
            {
                _loading = false;
            }
        }
    }

    private async Task LoadSkinAsync(AnimationSkinCandidate candidate, int? parentToken = null)
    {
        if (_animation is null || _profile is null)
        {
            return;
        }

        int token = parentToken ?? _generation.Begin();
        if (parentToken is null && !_generation.IsCurrent(token))
        {
            return;
        }

        _loading = true;
        _playing = false;
        _timer.Stop();
        _play.Text = "Play";
        _play.Enabled = false;
        _scrub.Enabled = false;
        _large.Enabled = false;
        _preview.ShowMessage("Loading preview...", false);
        try
        {
            await _preview.LoadAsync(
                _profile,
                _assets,
                candidate.Skin,
                candidate.SkeletonRoot,
                _animation,
                CancellationToken.None);
            if (IsDisposed || !_generation.IsCurrent(token))
            {
                return;
            }

            _scrub.Maximum = Math.Max(0, _preview.FrameCount - 1);
            _scrub.Value = 0;
            _scrub.Enabled = _preview.FrameCount > 1;
            _play.Enabled = _preview.FrameCount > 1;
            _large.Enabled = true;
            _subtitle.Text =
                $"{Path.GetFileName(_animation.ArchivePath)} · {_duration:F3} s · " +
                $"{_preview.FrameCount} frames · {_boneCount} bones";
            _preview.Fit();
            UpdateFrameLabel();
        }
        finally
        {
            if (_generation.IsCurrent(token))
            {
                _loading = false;
            }
        }
    }

    private void TogglePlay()
    {
        if (!_preview.HasPose || _preview.FrameCount <= 1)
        {
            return;
        }

        _playing = !_playing;
        _play.Text = _playing ? "Stop" : "Play";
        if (_playing)
        {
            if (_preview.Frame >= _preview.FrameCount - 1)
            {
                _preview.SetFrame(0);
                UpdateFrameLabel();
            }

            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void UpdateFrameLabel()
    {
        _frame.Text = _preview.FrameCount == 0
            ? "frame -"
            : $"frame {_preview.Frame} / {_preview.FrameCount - 1}";
        _suppressScrub = true;
        try
        {
            _scrub.Value = _preview.Frame;
        }
        finally
        {
            _suppressScrub = false;
        }
    }

    private void UpdateCandidateLabel(AnimationSkinCandidate candidate)
    {
        _candidate.Text =
            $"Preview skin: 0x{candidate.SkinHash:X16} · candidate · " +
            $"{candidate.Matched}/{candidate.AnimationBones} bones";
    }

    private void OpenLargePreview()
    {
        if (_animation is null || _profile is null || _skin.SelectedIndex < 0
            || _skin.SelectedIndex >= _candidates.Count)
        {
            return;
        }

        AnimationSkinCandidate candidate = _candidates[_skin.SelectedIndex];
        using PoseForm form = new(
            _profile,
            _assets,
            candidate.Skin,
            candidate.SkeletonRoot,
            _animation);
        form.Show(this);
    }

    private AnimationData ReadData()
    {
        if (_animation is null)
        {
            return null!;
        }

        using FileStream stream = new(
            _animation.BodyArchivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        return AnimationDecoder.ReadBody(
            stream, _animation.BodyArchivePath, _animation.Row).Data;
    }
}
