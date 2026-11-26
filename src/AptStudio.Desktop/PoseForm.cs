using System.Globalization;
using System.Numerics;
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
    private readonly TaskCompletionSource<bool> _ready = new();
    private SkinDeformer? _deformer;
    private SkeletonData? _skeleton;
    private CancellationTokenSource? _cancel;
    private int _selectedBone = -1;
    private bool _suppress;
    private bool _loaded;
    private bool _loading;

    public PoseForm(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        ulong skeletonRoot)
    {
        _profile = profile;
        _assets = assets;
        _skin = skin;
        _skeletonRoot = skeletonRoot;

        Text = $"Pose 0x{skin.NameHash:X16}";
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
            RowCount = 3,
            BackColor = Theme.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.Controls.Add(CreateBar(), 0, 0);
        root.Controls.Add(content, 0, 1);
        root.Controls.Add(_status, 0, 2);
        Controls.Add(root);

        _status.Text = "select a bone, set X/Y/Z | drag to orbit, right-drag to pan, wheel to zoom";
        _status.AutoEllipsis = true;
        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.Height = 24;
        _status.ForeColor = Theme.Muted;
        _status.Padding = new Padding(8, 4, 8, 6);

        Shown += async (_, _) => await LoadAsync();
        FormClosed += (_, _) =>
        {
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
        _reset.AutoSize = true;
        _reset.FlatStyle = FlatStyle.Flat;
        _reset.BackColor = Theme.Surface;
        _reset.ForeColor = Theme.Text;
        _reset.Padding = new Padding(10, 0, 10, 0);
        _reset.MinimumSize = new Size(0, 24);
        _reset.UseVisualStyleBackColor = false;
        _reset.FlatAppearance.BorderSize = 0;
        _reset.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        _reset.FlatAppearance.MouseDownBackColor = Theme.Selection;
        _reset.Margin = new Padding(0, 3, 10, 3);
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
