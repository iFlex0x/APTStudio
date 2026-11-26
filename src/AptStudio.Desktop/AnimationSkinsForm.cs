using System.Diagnostics;
using System.Globalization;
using AptStudio.Core.Animations;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Links;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop;

internal sealed record AnimationSkinRow(
    ulong SkeletonRoot,
    int Matched,
    int AnimationBones,
    int SkeletonBones,
    AssetEntry Skin)
{
    public string ArchiveName => Path.GetFileName(Skin.ArchivePath);
}

internal sealed class AnimationSkinsForm : Form
{
    private readonly GameProfile _profile;
    private readonly IReadOnlyList<AssetEntry> _assets;
    private readonly AssetEntry _animation;
    private readonly Label _title = new();
    private readonly Label _status = new();
    private readonly DataGridView _grid = new();
    private readonly Button _pose = new();
    private readonly TaskCompletionSource<bool> _ready = new();
    private CancellationTokenSource? _cancel;
    private IReadOnlyList<AnimationSkinRow> _rows = Array.Empty<AnimationSkinRow>();
    private bool _loaded;

    public AnimationSkinsForm(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry animation)
    {
        _profile = profile;
        _assets = assets;
        _animation = animation;

        Text = $"Animation skins - 0x{animation.NameHash:X16}";
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 420);
        Size = new Size(980, 620);
        ShowInTaskbar = false;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        FormClosing += (_, _) => _cancel?.Cancel();

        _title.Text = $"Animation_Z 0x{animation.NameHash:X16}";
        _title.Font = new Font("Segoe UI Semibold", 9.5f);
        _title.ForeColor = Theme.Text;
        _title.AutoEllipsis = true;
        _title.Dock = DockStyle.Top;
        _title.Height = 24;
        _title.Padding = new Padding(8, 2, 8, 0);

        _status.AutoEllipsis = true;
        _status.ForeColor = Theme.Muted;
        _status.Dock = DockStyle.Top;
        _status.Height = 22;
        _status.Padding = new Padding(8, 2, 8, 0);
        _status.Text = "not loaded";

        _pose.Text = "Open Pose";
        _pose.Enabled = false;
        StyleButton(_pose);
        _pose.Click += (_, _) => OpenSelected();

        Button close = new()
        {
            Text = "Close",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Padding = new Padding(10, 0, 10, 0),
            MinimumSize = new Size(0, 24),
            UseVisualStyleBackColor = false,
            Margin = new Padding(0, 3, 0, 3),
        };
        close.FlatAppearance.BorderSize = 0;
        close.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        close.FlatAppearance.MouseDownBackColor = Theme.Selection;
        close.Click += (_, _) => Close();

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Theme.Surface,
            Padding = new Padding(8, 4, 8, 4),
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(_pose);

        ConfigureGrid();
        _grid.SelectionChanged += (_, _) => _pose.Enabled = _grid.CurrentRow?.Tag is AnimationSkinRow;
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                OpenSelected();
            }
        };

        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(_status);
        Controls.Add(_title);
        Shown += async (_, _) => await LoadAsync();
    }

    public event Action<AssetEntry, ulong, ulong>? PoseRequested;

    internal Task LoadForShotAsync() => _ready.Task;

    internal void ScaleForShot(float scale)
    {
        if (float.IsFinite(scale) && scale > 0f)
        {
            Scale(new SizeF(scale, scale));
        }
    }

    internal void CaptureShot(string path)
    {
        using Bitmap bitmap = new(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        _cancel?.Cancel();
        _cancel?.Dispose();
        CancellationTokenSource cancel = new();
        _cancel = cancel;
        Stopwatch watch = Stopwatch.StartNew();
        IProgress<CatalogProgress> progress = new Progress<CatalogProgress>(item =>
        {
            if (!IsDisposed && cancel == _cancel)
            {
                _status.Text =
                    $"{Path.GetFileName(item.ArchivePath)} {item.Index}/{item.Total}";
            }
        });

        try
        {
            IReadOnlyList<AnimationSkinRow> rows = await Task.Run(
                () => BuildRows(_profile, _assets, _animation, progress, cancel.Token),
                cancel.Token);
            if (IsDisposed || cancel != _cancel)
            {
                return;
            }

            watch.Stop();
            _rows = rows;
            FillGrid();
            _loaded = true;
            string unmatched = rows.Count == 0
                ? "no bound skin shares a bone with this animation"
                : $"{rows.Count:N0} skins | {rows.Max(row => row.Matched):N0} bones matched at best";
            _status.Text = $"{unmatched} | {watch.ElapsedMilliseconds} ms";
            _ready.TrySetResult(rows.Count > 0);
        }
        catch (OperationCanceledException)
        {
            _ready.TrySetResult(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && cancel == _cancel)
            {
                _status.Text = ex.Message;
                _ready.TrySetResult(false);
            }
        }
        finally
        {
            if (cancel == _cancel)
            {
                _cancel = null;
            }

            cancel.Dispose();
        }
    }

    private static List<AnimationSkinRow> BuildRows(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry animation,
        IProgress<CatalogProgress> progress,
        CancellationToken cancellationToken)
    {
        AnimationBody body;
        using (FileStream stream = new(
            animation.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            body = AnimationDecoder.ReadBody(stream, animation.BodyArchivePath, animation.Row);
        }

        AnimationData data = body.Data;
        if (data.Encoding != AnimationEncoding.Packed || data.Bones.Count == 0)
        {
            return new List<AnimationSkinRow>();
        }

        ulong[] animationBones = data.Bones.Select(bone => bone.NameHash).ToArray();
        SkinSkeletonIndex skins = SkinSkeletonIndex.Load(assets, progress, cancellationToken);
        SkeletonCatalog catalog = SkeletonCatalog.Load(
            profile, false, progress, cancellationToken);
        Dictionary<ulong, SkeletonEntry> entries = new();
        foreach (SkeletonEntry entry in catalog.Entries)
        {
            entries.TryAdd(entry.NameHash, entry);
        }

        List<AnimationSkinRow> rows = new();
        foreach ((ulong skeletonRoot, IReadOnlyList<SkinSkeletonUse> uses) in skins.BySkeleton)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entries.TryGetValue(skeletonRoot, out SkeletonEntry? entry))
            {
                continue;
            }

            SkeletonData skeleton;
            using (FileStream stream = new(
                entry.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                skeleton = SkeletonDecoder.Read(stream, entry.ArchivePath, entry.Hit);
            }

            HashSet<ulong> skeletonBones =
                skeleton.Bones.Select(bone => bone.NameHash).ToHashSet();
            int matched = AnimationSkeletonMatch.Count(animationBones, skeletonBones);
            if (matched == 0)
            {
                continue;
            }

            foreach (SkinSkeletonUse use in uses)
            {
                rows.Add(new AnimationSkinRow(
                    skeletonRoot,
                    matched,
                    animationBones.Length,
                    skeleton.Bones.Count,
                    use.Row));
            }
        }

        rows.Sort((left, right) =>
        {
            int order = right.Matched.CompareTo(left.Matched);
            if (order != 0)
            {
                return order;
            }

            order = left.SkeletonBones.CompareTo(right.SkeletonBones);
            if (order != 0)
            {
                return order;
            }

            order = left.Skin.NameHash.CompareTo(right.Skin.NameHash);
            return order != 0
                ? order
                : string.Compare(
                    left.Skin.ArchivePath,
                    right.Skin.ArchivePath,
                    StringComparison.OrdinalIgnoreCase);
        });
        return rows;
    }

    private void FillGrid()
    {
        _grid.Rows.Clear();
        foreach (AnimationSkinRow row in _rows)
        {
            int index = _grid.Rows.Add(
                $"0x{row.SkeletonRoot:X16}",
                $"{row.Matched}/{row.AnimationBones}",
                row.SkeletonBones.ToString(CultureInfo.InvariantCulture),
                $"0x{row.Skin.NameHash:X16}",
                row.ArchiveName);
            _grid.Rows[index].Tag = row;
        }

        if (_grid.Rows.Count > 0)
        {
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
            _pose.Enabled = _grid.CurrentRow?.Tag is AnimationSkinRow;
        }
    }

    private void OpenSelected()
    {
        if (_grid.CurrentRow?.Tag is not AnimationSkinRow row)
        {
            return;
        }

        PoseRequested?.Invoke(row.Skin, row.SkeletonRoot, _animation.NameHash);
        Close();
    }

    private static void StyleButton(Button button)
    {
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Text;
        button.Padding = new Padding(10, 0, 10, 0);
        button.MinimumSize = new Size(0, 24);
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SelectionInactive;
        button.FlatAppearance.MouseDownBackColor = Theme.Selection;
        button.Margin = new Padding(0, 3, 8, 3);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Theme.Background;
        _grid.GridColor = Theme.Border;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 24;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.RowTemplate.Height = 21;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridHeader,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.GridHeader,
            SelectionForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Padding = new Padding(4, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
            Padding = new Padding(4, 0, 4, 0),
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.GridAlt,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Selection,
            SelectionForeColor = Color.White,
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Skeleton",
            Width = 200,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Matched",
            Width = 110,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Bones",
            Width = 70,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Skin",
            Width = 200,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Archive",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
    }
}
