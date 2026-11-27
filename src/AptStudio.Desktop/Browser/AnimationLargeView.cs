using System.Drawing;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;

namespace AptStudio.Desktop.Browser;

internal sealed class AnimationLargeView : Panel
{
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _candidate = new();
    private readonly Label _hint = new();
    private readonly PosePreview _preview = new();
    private readonly Button _play = new();
    private readonly Button _bind = new();
    private readonly Button _back = new();
    private readonly TrackBar _scrub = new();
    private readonly Label _frame = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };
    private bool _playing;
    private bool _suppress;
    private bool _loading;

    public AnimationLargeView()
    {
        BackColor = Theme.Background;
        Padding = new Padding(14, 12, 14, 10);

        _title.Font = BrowserVisual.MonoStrong;
        _title.ForeColor = Theme.Text;
        _title.Dock = DockStyle.Top;
        _title.Height = 24;
        _title.AutoEllipsis = true;

        _subtitle.Font = BrowserVisual.UiSmall;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 18;
        _subtitle.AutoEllipsis = true;

        _candidate.Font = BrowserVisual.UiSmall;
        _candidate.ForeColor = Theme.Muted;
        _candidate.Dock = DockStyle.Top;
        _candidate.Height = 18;
        _candidate.AutoEllipsis = true;

        _hint.Font = BrowserVisual.RowSmall;
        _hint.ForeColor = BrowserVisual.Faint;
        _hint.Dock = DockStyle.Top;
        _hint.Height = 16;
        _hint.AutoEllipsis = true;

        Panel header = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Theme.Background,
        };
        header.Controls.Add(_hint);
        header.Controls.Add(_candidate);
        header.Controls.Add(_subtitle);
        header.Controls.Add(_title);

        _preview.Dock = DockStyle.Fill;
        BrowserVisual.Frame(_preview);

        BrowserButtons.Style(_play, BrowserButtonRole.Primary, Theme.Background);
        _play.Text = "Play";
        _play.AutoSize = true;
        _play.Enabled = false;
        _play.Click += (_, _) => TogglePlay();

        _scrub.Minimum = 0;
        _scrub.Maximum = 0;
        _scrub.TickStyle = TickStyle.None;
        _scrub.AutoSize = false;
        _scrub.Height = 28;
        _scrub.Dock = DockStyle.Fill;
        _scrub.Margin = new Padding(8, 0, 8, 0);
        _scrub.Enabled = false;
        _scrub.ValueChanged += (_, _) =>
        {
            if (_suppress || _loading)
            {
                return;
            }

            Pause();
            _preview.SetFrame(_scrub.Value);
            UpdateFrameLabel();
        };

        _frame.Text = "frame -";
        _frame.Font = BrowserVisual.RowSmall;
        _frame.ForeColor = Theme.Muted;
        _frame.AutoSize = true;
        _frame.Anchor = AnchorStyles.Right;
        _frame.Margin = new Padding(0, 8, 10, 0);

        BrowserButtons.Style(_bind, BrowserButtonRole.Ordinary, Theme.Background);
        _bind.Text = "Bind pose";
        _bind.AutoSize = true;
        _bind.Enabled = false;
        _bind.Click += (_, _) => ShowBind();

        BrowserButtons.Style(_back, BrowserButtonRole.Ordinary, Theme.Background);
        _back.Text = "Back to clip";
        _back.AutoSize = true;
        _back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);

        TableLayoutPanel controls = new()
        {
            Dock = DockStyle.Top,
            Height = 38,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Theme.Background,
            Padding = new Padding(0, 5, 0, 0),
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        controls.Controls.Add(_play, 0, 0);
        controls.Controls.Add(_scrub, 1, 0);
        controls.Controls.Add(_frame, 2, 0);
        controls.Controls.Add(_bind, 3, 0);
        controls.Controls.Add(_back, 4, 0);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_preview, 0, 1);
        root.Controls.Add(controls, 0, 2);
        Controls.Add(root);

        _timer.Tick += (_, _) =>
        {
            if (!_playing || _preview.FrameCount <= 1)
            {
                return;
            }

            _preview.SetFrame((_preview.Frame + 1) % _preview.FrameCount);
            UpdateFrameLabel();
        };
    }

    public event EventHandler? BackRequested;

    public void ShowState(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        AssetEntry clip,
        AnimationLargeState state)
    {
        _title.Text = $"Animation_Z 0x{clip.NameHash:X16}";
        _subtitle.Text =
            $"{AnimationText.SourceLabel(clip)} · {state.Duration:F3} s · " +
            $"{state.BoneCount} bones";
        _candidate.Text =
            $"Bone names: {state.Matched}/{state.AnimationBones} · " +
            $"preview skin 0x{state.SkinHash:X16}";
        _hint.Text =
            $"Rig fit not verified · skeleton 0x{state.SkeletonRoot:X16} · " +
            $"{state.SkeletonBones} bones";
        _ = LoadAsync(profile, assets, skin, clip, state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task LoadAsync(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        AssetEntry clip,
        AnimationLargeState state)
    {
        _loading = true;
        _play.Enabled = false;
        _scrub.Enabled = false;
        _bind.Enabled = false;
        try
        {
            bool loaded = await _preview.LoadAsync(
                profile,
                assets,
                skin,
                state.SkeletonRoot,
                clip,
                CancellationToken.None,
                state.Frame,
                state.Applied);
            if (IsDisposed)
            {
                return;
            }

            if (!loaded)
            {
                UpdateFrameLabel();
                return;
            }

            _scrub.Maximum = Math.Max(0, _preview.FrameCount - 1);
            _scrub.Enabled = _preview.FrameCount > 1;
            _play.Enabled = _preview.FrameCount > 1;
            _bind.Enabled = _preview.HasPose;
            _preview.Fit();
            UpdateFrameLabel();
        }
        finally
        {
            _loading = false;
        }
    }

    private void TogglePlay()
    {
        if (!_preview.HasPose || _preview.FrameCount <= 1)
        {
            return;
        }

        if (!_preview.IsApplied)
        {
            _preview.SetFrame(0);
            UpdateFrameLabel();
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

    private void Pause()
    {
        if (!_playing)
        {
            return;
        }

        _playing = false;
        _timer.Stop();
        _play.Text = "Play";
    }

    private void ShowBind()
    {
        Pause();
        _preview.ShowBind();
        UpdateFrameLabel();
    }

    private void UpdateFrameLabel()
    {
        _frame.Text = _preview.FrameCount == 0
            ? "frame -"
            : _preview.IsApplied
                ? $"frame {_preview.Frame} / {_preview.FrameCount - 1}"
                : "bind pose";
        _suppress = true;
        try
        {
            _scrub.Value = _preview.Frame;
        }
        finally
        {
            _suppress = false;
        }
    }
}
