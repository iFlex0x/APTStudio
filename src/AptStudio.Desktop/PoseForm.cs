using System.Globalization;
using System.Numerics;
using AptStudio.Core.Animations;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Meshes;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Skins;

namespace AptStudio.Desktop;

internal sealed class PoseForm : Form
{
    private readonly GameProfile _profile;
    private readonly IReadOnlyList<AssetEntry> _assets;
    private readonly AssetEntry _skin;
    private readonly ulong _skeletonRoot;
    private readonly ulong _animationHash;
    private readonly MeshView _view = new();
    private readonly DataGridView _bones = new();
    private readonly Dictionary<ulong, int> _boneRows = new();
    private readonly Dictionary<int, (float X, float Y, float Z)> _angles = new();
    private readonly NumericUpDown _x = CreateAngle();
    private readonly NumericUpDown _y = CreateAngle();
    private readonly NumericUpDown _z = CreateAngle();
    private readonly CheckBox _wire = new();
    private readonly Button _reset = new();
    private readonly Label _status = new();
    private readonly TextBox _animationBox = new();
    private readonly Button _animationUse = new();
    private readonly Button _animationPlay = new();
    private readonly Label _frameLabel = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly TaskCompletionSource<bool> _ready = new();
    private SkinDeformer? _deformer;
    private SkeletonData? _skeleton;
    private AnimationClipBinding? _binding;
    private CancellationTokenSource? _cancel;
    private int _selectedBone = -1;
    private int _frame;
    private bool _suppress;
    private bool _loaded;
    private bool _loading;
    private bool _animating;

    public PoseForm(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        ulong skeletonRoot,
        ulong animationHash = 0)
    {
        _profile = profile;
        _assets = assets;
        _skin = skin;
        _skeletonRoot = skeletonRoot;
        _animationHash = animationHash;

        Text = $"Pose 0x{skin.NameHash:X16}";
        Icon = Theme.AppIcon;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 560);
        Size = new Size(1120, 720);
        ShowInTaskbar = false;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        ConfigureToolbar();
        ConfigureBones();
        _view.Dock = DockStyle.Fill;

        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(_bones, 0, 0);
        content.Controls.Add(_view, 1, 0);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.Controls.Add(CreateBar(), 0, 0);
        root.Controls.Add(CreateAnimationBar(), 0, 1);
        root.Controls.Add(content, 0, 2);
        root.Controls.Add(_status, 0, 3);
        Controls.Add(root);

        _status.Text = "select a bone, set X/Y/Z | drag to orbit, right-drag to pan, wheel to zoom";
        _status.AutoEllipsis = true;
        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.Height = 24;
        _status.ForeColor = Theme.Muted;
        _status.Padding = new Padding(8, 4, 8, 6);

        _timer.Interval = 33;
        _timer.Tick += (_, _) => AdvanceFrame();
        Shown += async (_, _) =>
        {
            await LoadAsync();
            if (_animationHash != 0 && _skeleton is not null && !IsDisposed)
            {
                _animationBox.Text = _animationHash.ToString("X16", CultureInfo.InvariantCulture);
                await UseAnimationAsync();
            }
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
            _cancel?.Cancel();
            _cancel?.Dispose();
        };
    }

    internal async Task LoadForShotAsync() => await _ready.Task;

    internal bool PoseForShot(ulong boneHash, float x, float y, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            return false;
        }

        if (!_boneRows.TryGetValue(boneHash, out int row))
        {
            return false;
        }

        _bones.CurrentCell = _bones.Rows[row].Cells[0];
        _bones.Rows[row].Selected = true;
        SelectBone(row);
        _suppress = true;
        try
        {
            _x.Value = (decimal)x;
            _y.Value = (decimal)y;
            _z.Value = (decimal)z;
        }
        finally
        {
            _suppress = false;
        }

        ApplyAngles();
        return true;
    }

    internal void CaptureShot(string path)
    {
        using Bitmap bitmap = new(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    internal void ScaleForShot(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            return;
        }

        Scale(new SizeF(scale, scale));
        SetDpiScale(scale);
    }

    internal void SetDpiScale(float scale)
    {
        if (scale <= 0f)
        {
            return;
        }

        _bones.RowTemplate.Height = (int)Math.Round(20 * scale);
        _bones.ColumnHeadersHeight = (int)Math.Round(22 * scale);
        _bones.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI Semibold", 8.5f * scale);
        foreach (DataGridViewColumn column in _bones.Columns)
        {
            if (column.Tag is int width)
            {
                column.Width = (int)Math.Round(width * scale);
            }
        }

        _status.Font = new Font("Segoe UI", 8.5f * scale);
    }

    private static NumericUpDown CreateAngle()
    {
        NumericUpDown number = new()
        {
            Minimum = -180,
            Maximum = 180,
            DecimalPlaces = 0,
            Increment = 5,
            Width = 62,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(2, 3, 8, 3),
        };
        return number;
    }

    private void ConfigureToolbar()
    {
        _reset.Text = "Reset";
        StyleButton(_reset, new Padding(10, 0, 10, 0));
        _reset.Click += (_, _) => ResetPose();

        _wire.Text = "Wireframe";
        _wire.AutoSize = true;
        _wire.ForeColor = Theme.Text;
        _wire.FlatStyle = FlatStyle.Flat;
        _wire.Margin = new Padding(0, 6, 14, 3);
        _wire.CheckedChanged += (_, _) => _view.SetWireframe(_wire.Checked);

        _x.ValueChanged += (_, _) => ApplyAngles();
        _y.ValueChanged += (_, _) => ApplyAngles();
        _z.ValueChanged += (_, _) => ApplyAngles();
    }

    private Control CreateBar()
    {
        FlowLayoutPanel bar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Theme.Background,
            Padding = new Padding(6, 2, 6, 2),
            WrapContents = false,
        };
        bar.Controls.Add(_reset);
        bar.Controls.Add(_wire);
        bar.Controls.Add(AngleLabel("X"));
        bar.Controls.Add(_x);
        bar.Controls.Add(AngleLabel("Y"));
        bar.Controls.Add(_y);
        bar.Controls.Add(AngleLabel("Z"));
        bar.Controls.Add(_z);
        return bar;
    }

    private Control CreateAnimationBar()
    {
        Label label = new()
        {
            Text = "Anim",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 7, 4, 3),
        };
        _animationBox.Width = 150;
        _animationBox.MaxLength = 18;
        _animationBox.CharacterCasing = CharacterCasing.Upper;
        _animationBox.BackColor = Theme.Surface;
        _animationBox.ForeColor = Theme.Text;
        _animationBox.BorderStyle = BorderStyle.FixedSingle;
        _animationBox.Margin = new Padding(2, 4, 4, 3);
        _animationBox.PlaceholderText = "Animation_Z hash";

        _animationUse.Text = "Load";
        StyleButton(_animationUse, new Padding(8, 0, 8, 0));
        _animationUse.Click += async (_, _) => await UseAnimationAsync();

        _animationPlay.Text = "Play";
        _animationPlay.Enabled = false;
        StyleButton(_animationPlay, new Padding(8, 0, 8, 0));
        _animationPlay.Click += (_, _) => TogglePlay();

        _frameLabel.Text = "frame -";
        _frameLabel.AutoSize = true;
        _frameLabel.ForeColor = Theme.Muted;
        _frameLabel.Margin = new Padding(4, 7, 4, 3);

        FlowLayoutPanel bar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Theme.Background,
            Padding = new Padding(6, 2, 6, 2),
            WrapContents = false,
        };
        bar.Controls.Add(label);
        bar.Controls.Add(_animationBox);
        bar.Controls.Add(_animationUse);
        bar.Controls.Add(_animationPlay);
        bar.Controls.Add(_frameLabel);
        return bar;
    }

    private static void StyleButton(Button button, Padding padding)
    {
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Text;
        button.Padding = padding;
        button.MinimumSize = new Size(0, 24);
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        button.FlatAppearance.MouseDownBackColor = Theme.Selection;
        button.Margin = new Padding(0, 3, 10, 3);
    }

    private async Task UseAnimationAsync()
    {
        if (_skeleton is null)
        {
            return;
        }

        if (!TryHash(_animationBox.Text, out ulong hash))
        {
            _status.Text = "animation hash must be 16 hex digits.";
            return;
        }

        _animationUse.Enabled = false;
        try
        {
            AnimationClip clip = await Task.Run(() => LoadClip(hash));
            BindAnimation(clip);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            _animationUse.Enabled = true;
        }
    }

    private AnimationClip LoadClip(ulong hash)
    {
        AssetEntry? entry = _assets
            .Where(row => row.ClassHash == AssetClass.Animation && row.NameHash == hash)
            .GroupBy(row => (row.BodyArchivePath, row.Row.BodyOffset))
            .Select(group => group
                .OrderBy(row => row.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .First())
            .FirstOrDefault()
            ?? throw new InvalidDataException($"no Animation_Z row named 0x{hash:X16}.");
        using FileStream stream = new(
            entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        AnimationBody body = AnimationDecoder.ReadBody(stream, entry.BodyArchivePath, entry.Row);
        return AnimationClipReader.Read(body.Data, body.Body);
    }

    private void BindAnimation(AnimationClip clip)
    {
        if (_skeleton is null)
        {
            return;
        }

        _binding = AnimationClipBinding.Create(clip, _skeleton);
        _frame = 0;
        _animationPlay.Enabled = clip.FrameCount > 1;
        ApplyAnimation();
    }

    private void ApplyAnimation()
    {
        if (_deformer is null || _binding is null)
        {
            return;
        }

        _frame = Math.Clamp(_frame, 0, _binding.FrameCount - 1);
        MeshGeometry mesh = _deformer.DeformAbsolute(_binding.Sample(_frame));
        _view.SetPositions(mesh.Positions);
        _frameLabel.Text = $"frame {_frame}/{_binding.FrameCount - 1}";
        _status.Text =
            $"{_deformer.Parts.Count:N0} parts | {_deformer.Bones.Count:N0} bones | " +
            $"{_binding.MappedBones:N0} animated | frame {_frame} of {_binding.FrameCount - 1}";
    }

    private void AdvanceFrame()
    {
        if (!_animating || _binding is null)
        {
            return;
        }

        _frame = (_frame + 1) % _binding.FrameCount;
        ApplyAnimation();
    }

    private void TogglePlay()
    {
        if (_binding is null)
        {
            return;
        }

        _animating = !_animating;
        _animationPlay.Text = _animating ? "Stop" : "Play";
        if (_animating)
        {
            if (_frame >= _binding.FrameCount - 1)
            {
                _frame = 0;
            }

            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void ClearAnimation()
    {
        _animating = false;
        _timer.Stop();
        _animationPlay.Text = "Play";
        _animationPlay.Enabled = false;
        _binding = null;
        _frame = 0;
        _frameLabel.Text = "frame -";
    }

    internal bool AnimationForShot(ulong hash, int frame)
    {
        if (_skeleton is null || _deformer is null)
        {
            return false;
        }

        try
        {
            AnimationClip clip = LoadClip(hash);
            BindAnimation(clip);
            if (_binding is null)
            {
                return false;
            }

            _frame = Math.Clamp(frame, 0, _binding.FrameCount - 1);
            ApplyAnimation();
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryHash(string text, out ulong hash) =>
        ulong.TryParse(
            text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out hash);

    private static Label AngleLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.Muted,
        Margin = new Padding(0, 7, 2, 3),
    };

    private void ConfigureBones()
    {
        _bones.Dock = DockStyle.Fill;
        _bones.BackgroundColor = Theme.Background;
        _bones.GridColor = Theme.Border;
        _bones.BorderStyle = BorderStyle.None;
        _bones.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _bones.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _bones.EnableHeadersVisualStyles = false;
        _bones.ColumnHeadersHeight = 22;
        _bones.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _bones.RowHeadersVisible = false;
        _bones.AllowUserToAddRows = false;
        _bones.AllowUserToDeleteRows = false;
        _bones.AllowUserToResizeRows = false;
        _bones.ReadOnly = true;
        _bones.MultiSelect = false;
        _bones.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _bones.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _bones.RowTemplate.Height = 20;
        _bones.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Padding = new Padding(4, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _bones.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(4, 0, 4, 0),
        };
        _bones.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        DataGridViewTextBoxColumn name = new()
        {
            HeaderText = "Bone",
            Width = 160,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Tag = 160,
            DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Consolas", 8.5f) },
        };
        DataGridViewTextBoxColumn index = new()
        {
            HeaderText = "Index",
            Width = 50,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Tag = 50,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
            },
        };
        DataGridViewTextBoxColumn parent = new()
        {
            HeaderText = "Parent",
            Width = 60,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Tag = 60,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
            },
        };
        _bones.Columns.Add(name);
        _bones.Columns.Add(index);
        _bones.Columns.Add(parent);
        _bones.CurrentCellChanged += (_, _) => SelectBone(_bones.CurrentCell?.RowIndex ?? -1);
    }

    private async Task LoadAsync()
    {
        if (_loaded || _loading)
        {
            return;
        }

        _loading = true;
        _cancel = new CancellationTokenSource();
        _status.Text = "loading skin...";
        try
        {
            PoseLoad result = await Task.Run(() => LoadPose(_cancel.Token), _cancel.Token);
            if (IsDisposed)
            {
                _ready.TrySetResult(false);
                return;
            }

            _deformer = result.Deformer;
            _skeleton = result.Skeleton;
            _view.SetMesh(result.Bind);
            FillBones();
            _loaded = true;
            _status.Text =
                $"{result.Deformer.Parts.Count:N0} parts | {result.Deformer.Bones.Count:N0} bones | bind pose";
            _ready.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
            _ready.TrySetResult(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            _view.ShowMessage(ex.Message, true);
            _status.Text = ex.Message;
            _ready.TrySetResult(false);
        }
        finally
        {
            _loading = false;
        }
    }

    private PoseLoad LoadPose(CancellationToken cancellationToken)
    {
        SkeletonCatalog catalog = SkeletonCatalog.Load(
            _profile, false, null, cancellationToken);
        SkeletonEntry skeletonEntry = catalog.Entries.FirstOrDefault(
            entry => entry.NameHash == _skeletonRoot)
            ?? throw new InvalidDataException(
                $"skeleton 0x{_skeletonRoot:X16} was not found.");
        SkeletonData skeleton;
        using (FileStream stream = new(
            skeletonEntry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            skeleton = SkeletonDecoder.Read(
                stream, skeletonEntry.ArchivePath, skeletonEntry.Hit);
        }

        Dictionary<ulong, AssetEntry> meshRows = new();
        foreach (AssetEntry entry in _assets)
        {
            if (entry.ClassHash == AssetClass.Mesh)
            {
                meshRows.TryAdd(entry.NameHash, entry);
            }
        }

        Dictionary<ulong, MeshGeometry?> meshes = new();
        MeshGeometry? Geometry(ulong hash)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (meshes.TryGetValue(hash, out MeshGeometry? cached))
            {
                return cached;
            }

            if (!meshRows.TryGetValue(hash, out AssetEntry? row))
            {
                return null;
            }

            using FileStream stream = new(
                row.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            MeshData data = MeshDecoder.Read(stream, row.BodyArchivePath, row.Row);
            MeshGeometry mesh = data.Streams.Count == 0
                ? new MeshGeometry([], [], Vector3.Zero, Vector3.Zero)
                : data.ToGeometry();
            meshes.Add(hash, mesh);
            return mesh;
        }

        int? Verts(ulong hash) => Geometry(hash)?.Positions.Length;

        SkinData skin;
        using (FileStream stream = new(
            _skin.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            skin = SkinDecoder.Read(stream, _skin.BodyArchivePath, _skin.Row, Verts);
        }

        SkinDeformer deformer = SkinDeformer.Create(skin, skeleton, Geometry);
        return new PoseLoad(deformer, skeleton, deformer.Deform(null));
    }

    private void FillBones()
    {
        if (_deformer is null || _skeleton is null)
        {
            return;
        }

        _bones.Rows.Clear();
        _boneRows.Clear();
        foreach (SkinPaletteBone bone in _deformer.Bones)
        {
            int parent = _skeleton.Bones[bone.SkeletonBone].ParentIndex;
            int row = _bones.Rows.Add(
                $"0x{bone.NameHash:X16}",
                bone.SkeletonBone.ToString(CultureInfo.InvariantCulture),
                parent.ToString(CultureInfo.InvariantCulture));
            _bones.Rows[row].Tag = bone.SkeletonBone;
            _boneRows[bone.NameHash] = row;
        }

        if (_bones.Rows.Count > 0)
        {
            _bones.CurrentCell = _bones.Rows[0].Cells[0];
            _bones.Rows[0].Selected = true;
            SelectBone(0);
        }
    }

    private void SelectBone(int row)
    {
        if (row < 0 || row >= _bones.Rows.Count || _bones.Rows[row].Tag is not int bone)
        {
            return;
        }

        _selectedBone = bone;
        (float x, float y, float z) = _angles.GetValueOrDefault(bone);
        _suppress = true;
        try
        {
            _x.Value = (decimal)x;
            _y.Value = (decimal)y;
            _z.Value = (decimal)z;
        }
        finally
        {
            _suppress = false;
        }
    }

    private void ApplyAngles()
    {
        if (_suppress || _deformer is null || _selectedBone < 0)
        {
            return;
        }

        (float x, float y, float z) = ((float)_x.Value, (float)_y.Value, (float)_z.Value);
        if (x == 0 && y == 0 && z == 0)
        {
            _angles.Remove(_selectedBone);
        }
        else
        {
            _angles[_selectedBone] = (x, y, z);
        }

        ApplyPose();
    }

    private void ApplyPose()
    {
        if (_deformer is null)
        {
            return;
        }

        if (_binding is not null)
        {
            ApplyAnimation();
            return;
        }

        Dictionary<int, Quaternion> rotations = new();
        foreach ((int bone, (float x, float y, float z)) in _angles)
        {
            if (x == 0 && y == 0 && z == 0)
            {
                continue;
            }

            rotations[bone] = Quaternion.CreateFromYawPitchRoll(
                y * MathF.PI / 180f,
                x * MathF.PI / 180f,
                z * MathF.PI / 180f);
        }

        MeshGeometry mesh = _deformer.Deform(rotations);
        _view.SetPositions(mesh.Positions);
        _status.Text = rotations.Count == 0
            ? $"{_deformer.Parts.Count:N0} parts | {_deformer.Bones.Count:N0} bones | bind pose"
            : $"{_deformer.Parts.Count:N0} parts | {_deformer.Bones.Count:N0} bones | {rotations.Count:N0} posed";
    }

    private void ResetPose()
    {
        _angles.Clear();
        ClearAnimation();
        _suppress = true;
        try
        {
            _x.Value = 0;
            _y.Value = 0;
            _z.Value = 0;
        }
        finally
        {
            _suppress = false;
        }

        ApplyPose();
    }

    private sealed record PoseLoad(
        SkinDeformer Deformer,
        SkeletonData Skeleton,
        MeshGeometry Bind);
}
