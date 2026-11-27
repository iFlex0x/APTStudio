using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop;

internal sealed class PoseForm : Form
{
    private readonly PoseView _pose;

    public PoseForm(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        ulong skeletonRoot,
        ulong animationHash = 0)
        : this(profile, assets, skin, skeletonRoot, animationHash, null)
    {
    }

    public PoseForm(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        ulong skeletonRoot,
        AssetEntry animation)
        : this(profile, assets, skin, skeletonRoot, animation.NameHash, animation)
    {
    }

    private PoseForm(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        ulong skeletonRoot,
        ulong animationHash,
        AssetEntry? animationEntry)
    {
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

        _pose = new PoseView(
            profile, assets, skin, skeletonRoot, showAnimation: true, animationHash, animationEntry);
        _pose.Dock = DockStyle.Fill;
        Controls.Add(_pose);
        Shown += async (_, _) => await _pose.LoadAsync();
    }

    internal async Task LoadForShotAsync() => await _pose.LoadForShotAsync();

    internal bool PoseForShot(ulong boneHash, float x, float y, float z) =>
        _pose.PoseForShot(boneHash, x, y, z);

    internal bool AnimationForShot(ulong hash, int frame) =>
        _pose.AnimationForShot(hash, frame);

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

    internal void SetDpiScale(float scale) => _pose.SetDpiScale(scale);
}
