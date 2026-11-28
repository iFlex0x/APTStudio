// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

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
    private readonly Label _hint = new();
    private readonly PosePreview _preview = new();
    private readonly Button _play = new();
    private readonly Button _large = new();
    private readonly Button _bind = new();
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
        Padding = new Padding(14, 13, 14, 10);

        _type.Text = "ANIMATION_Z · CLIP";
        _type.ForeColor = BrowserVisual.Faint;
        _type.Font = BrowserVisual.Section;
        _type.Dock = DockStyle.Top;
        _type.Height = 15;

        _hash.Font = BrowserVisual.MonoStrong;
        _hash.ForeColor = Theme.Text;
        _hash.Dock = DockStyle.Top;
        _hash.Height = 22;
        _hash.AutoEllipsis = true;

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

        _preview.Dock = DockStyle.Fill;
        BrowserVisual.Frame(_preview);

        BrowserButtons.Style(_play, BrowserButtonRole.Primary, Theme.Surface);
        _play.Text = "Play";
        _play.AutoSize = true;
        _play.Enabled = false;
        _play.Click += (_, _) => TogglePlay();

        _scrub.Minimum = 0;
        _scrub.Maximum = 0;
        _scrub.TickStyle = TickStyle.None;
        _scrub.AutoSize = false;
        _scrub.Height = 26;
        _scrub.Dock = DockStyle.Fill;
        _scrub.Margin = new Padding(8, 0, 8, 0);
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
        _frame.Font = BrowserVisual.RowSmall;
        _frame.ForeColor = Theme.Muted;
        _frame.AutoSize = true;
        _frame.Anchor = AnchorStyles.Right;
        _frame.Margin = new Padding(0);

        _skinLabel.Text = "Skin by name";
        _skinLabel.Font = BrowserVisual.RowSmall;
        _skinLabel.ForeColor = Theme.Muted;
        _skinLabel.AutoSize = true;
        _skinLabel.Margin = new Padding(0, 6, 6, 0);

        _skin.DropDownStyle = ComboBoxStyle.DropDownList;
        _skin.Width = 210;
        _skin.DropDownWidth = 320;
        _skin.Font = BrowserVisual.UiSmall;
        _skin.BackColor = Theme.Background;
        _skin.ForeColor = Theme.Text;
        _skin.FlatStyle = FlatStyle.Flat;
        _skin.Enabled = false;
        _skin.Dock = DockStyle.Fill;
        _skin.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _skin.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _skin.SelectedIndex < 0 || _skin.SelectedIndex >= _candidates.Count)
            {
                return;
            }

            UpdateCandidateLabel(_candidates[_skin.SelectedIndex]);
            _ = LoadSkinAsync(_candidates[_skin.SelectedIndex]);
        };

        BrowserButtons.Style(_large, BrowserButtonRole.Quiet, Theme.Surface);
        _large.Text = "Large preview";
        _large.AutoSize = true;
        _large.Enabled = false;
        _large.Click += (_, _) => OpenLargePreview();

        BrowserButtons.Style(_bind, BrowserButtonRole.Quiet, Theme.Surface);
        _bind.Text = "Bind pose";
        _bind.AutoSize = true;
        _bind.Enabled = false;
        _bind.Click += (_, _) => ShowBind();

        TableLayoutPanel playRow = new()
        {
            Dock = DockStyle.Top,
            Height = 30,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 0, 2),
        };
        playRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        playRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        playRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        playRow.Controls.Add(_play, 0, 0);
        playRow.Controls.Add(_scrub, 1, 0);
        playRow.Controls.Add(_frame, 2, 0);

        TableLayoutPanel skinRow = new()
        {
            Dock = DockStyle.Top,
            Height = 30,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
            Padding = new Padding(0, 3, 0, 0),
        };
        skinRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        skinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        skinRow.Controls.Add(_skinLabel, 0, 0);
        skinRow.Controls.Add(_skin, 1, 0);

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 8, 0, 0),
        };
        actions.Controls.Add(_large);
        actions.Controls.Add(_bind);

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
            RowCount = 5,
            BackColor = Theme.Surface,
        };
        header.Controls.Add(_type, 0, 0);
        header.Controls.Add(_hash, 0, 1);
        header.Controls.Add(_subtitle, 0, 2);
        header.Controls.Add(_candidate, 0, 3);
        header.Controls.Add(_hint, 0, 4);
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

    public event EventHandler<AnimationLargeState>? LargeRequested;

    public void ShowEmpty()
    {
        _generation.Begin();
        _loading = false;
        _playing = false;
        _timer.Stop();
        _animation = null;
        _candidates = Array.Empty<AnimationSkinCandidate>();
        _skin.Items.Clear();
        _play.Enabled = false;
        _scrub.Enabled = false;
        _skin.Enabled = false;
        _large.Enabled = false;
        _bind.Enabled = false;
        _hash.Text = "";
        _subtitle.Text = "Select a clip.";
        _candidate.Text = "";
        _hint.Text = "";
        _frame.Text = "frame -";
        _preview.Clear();
        _preview.ShowMessage("Select an animation clip.", false);
    }

    public void ResetForGame()
    {
        _assets = Array.Empty<AssetEntry>();
        _profile = null;
        ShowEmpty();
    }

    public void Pause()
    {
        if (!_playing)
        {
            return;
        }

        _playing = false;
        _timer.Stop();
        _play.Text = "Play";
    }

    public void SetClip(
        AssetEntry animation,
        IReadOnlyList<AssetEntry> assets,
        GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(animation);
        if (_animation is not null
            && _profile is not null
            && _profile.Root.Equals(profile.Root, StringComparison.OrdinalIgnoreCase)
            && _animation.NameHash == animation.NameHash
            && _animation.ArchivePath.Equals(
                animation.ArchivePath, StringComparison.OrdinalIgnoreCase)
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
        _subtitle.Text = $"{AnimationText.SourceLabel(animation)} · reading clip...";
        _candidate.Text = "Finding skins by bone name...";
        _hint.Text = "";
        _play.Enabled = false;
        _scrub.Enabled = false;
        _skin.Enabled = false;
        _large.Enabled = false;
        _bind.Enabled = false;
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
        AssetEntry? animation = _animation;
        GameProfile? profile = _profile;
        IReadOnlyList<AssetEntry> assets = _assets;
        if (animation is null || profile is null)
        {
            return;
        }

        int token = _generation.Begin();
        _loading = true;
        try
        {
            IReadOnlyList<AnimationSkinCandidate> candidates = await Task.Run(
                () => AnimationSkinCandidates.Load(profile, assets, animation),
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
                    $"0x{candidate.SkinHash:X16} · " +
                    $"{candidate.Matched}/{candidate.AnimationBones} names");
            }

            AnimationData data = ReadData(animation);
            _duration = data.Duration;
            _boneCount = data.Bones.Count;
            _subtitle.Text =
                $"{AnimationText.SourceLabel(animation)} · {data.Duration:F3} s · " +
                $"{data.Bones.Count} bones";
            if (candidates.Count == 0)
            {
                _candidate.Text = "No skin shares these bone names.";
                _preview.ShowMessage("No skin shares this clip's bone names.", true);
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
        AssetEntry? animation = _animation;
        GameProfile? profile = _profile;
        IReadOnlyList<AssetEntry> assets = _assets;
        if (animation is null || profile is null)
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
        _bind.Enabled = false;
        _preview.ShowMessage("Loading preview...", false);
        try
        {
            bool loaded = await _preview.LoadAsync(
                profile,
                assets,
                candidate.Skin,
                candidate.SkeletonRoot,
                animation,
                CancellationToken.None);
            if (IsDisposed || !_generation.IsCurrent(token))
            {
                return;
            }

            if (!loaded)
            {
                UpdateFrameLabel();
                return;
            }

            _scrub.Maximum = Math.Max(0, _preview.FrameCount - 1);
            _scrub.Value = 0;
            _scrub.Enabled = _preview.FrameCount > 1;
            _play.Enabled = _preview.FrameCount > 1;
            _large.Enabled = true;
            _bind.Enabled = true;
            _subtitle.Text =
                $"{AnimationText.SourceLabel(animation)} · {_duration:F3} s · " +
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

    private void UpdateFrameLabel()
    {
        _frame.Text = _preview.FrameCount == 0
            ? "frame -"
            : _preview.IsApplied
                ? $"frame {_preview.Frame} / {_preview.FrameCount - 1}"
                : "bind pose";
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
            $"Bone names: {candidate.Matched}/{candidate.AnimationBones} · " +
            $"preview skin 0x{candidate.SkinHash:X16}";
        _hint.Text =
            $"Rig fit not verified · skeleton 0x{candidate.SkeletonRoot:X16} · " +
            $"{candidate.SkeletonBones} bones";
    }

    private void ShowBind()
    {
        _playing = false;
        _timer.Stop();
        _play.Text = "Play";
        _preview.ShowBind();
        _suppressScrub = true;
        try
        {
            _scrub.Value = 0;
        }
        finally
        {
            _suppressScrub = false;
        }

        UpdateFrameLabel();
    }

    private void OpenLargePreview()
    {
        AssetEntry? animation = _animation;
        if (animation is null || _skin.SelectedIndex < 0
            || _skin.SelectedIndex >= _candidates.Count
            || !_preview.HasPose)
        {
            return;
        }

        AnimationSkinCandidate candidate = _candidates[_skin.SelectedIndex];
        AnimationLargeState large = new(
            candidate.SkinHash,
            candidate.Skin.ArchivePath,
            candidate.Skin.Row.BodyOffset,
            candidate.SkeletonRoot,
            _preview.IsApplied ? _preview.Frame : 0,
            _preview.IsApplied,
            candidate.Matched,
            candidate.AnimationBones,
            candidate.SkeletonBones,
            _duration,
            _boneCount);
        Pause();
        LargeRequested?.Invoke(this, large);
    }

    private AnimationData ReadData(AssetEntry animation)
    {
        using FileStream stream = new(
            animation.BodyArchivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        return AnimationDecoder.ReadBody(
            stream, animation.BodyArchivePath, animation.Row).Data;
    }
}
