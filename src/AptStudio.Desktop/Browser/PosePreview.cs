using System.Drawing;
using AptStudio.Core.Animations;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;

namespace AptStudio.Desktop.Browser;

internal sealed class PosePreview : Panel
{
    private readonly MeshView _view = new();
    private readonly LoadGeneration _generation = new();
    private PoseScene? _scene;
    private AnimationClipBinding? _binding;
    private int _frame;

    public PosePreview()
    {
        BackColor = Theme.Background;
        _view.Dock = DockStyle.Fill;
        Controls.Add(_view);
        ShowMessage("Select an animation clip.", false);
    }

    public bool HasPose => _scene is not null && _binding is not null;

    public int Frame => _frame;

    public int FrameCount => _binding?.FrameCount ?? 0;

    public void ShowMessage(string text, bool error) =>
        _view.ShowMessage(text, error);

    public async Task LoadAsync(
        GameProfile profile,
        IReadOnlyList<AssetEntry> assets,
        AssetEntry skin,
        ulong skeletonRoot,
        AssetEntry animation,
        CancellationToken cancellationToken)
    {
        int token = _generation.Begin();
        ShowMessage("Loading clip...", false);
        try
        {
            (PoseScene Scene, AnimationClip Clip) result = await Task.Run(() =>
            {
                PoseScene scene = PoseScene.Load(
                    profile, assets, skin, skeletonRoot, cancellationToken);
                using FileStream stream = new(
                    animation.BodyArchivePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                AnimationBody body = AnimationDecoder.ReadBody(
                    stream, animation.BodyArchivePath, animation.Row);
                AnimationClip clip = AnimationClipReader.Read(body.Data, body.Body);
                scene.BindAnimation(clip);
                return (scene, clip);
            }, cancellationToken);

            if (IsDisposed || !_generation.IsCurrent(token))
            {
                return;
            }

            _scene = result.Scene;
            _binding = _scene.Binding;
            _view.SetMesh(_scene.BindPose);
            SetFrame(0);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            if (!IsDisposed && _generation.IsCurrent(token))
            {
                ShowMessage(ex.Message, true);
            }
        }
    }

    public void SetFrame(int frame)
    {
        if (_scene is null || _binding is null)
        {
            return;
        }

        _frame = Math.Clamp(frame, 0, Math.Max(0, _binding.FrameCount - 1));
        _view.SetPositions(_scene.Deform(_frame).Positions);
    }

    public void SetWireframe(bool wireframe) => _view.SetWireframe(wireframe);

    public void Fit() => _view.Fit();

    public void Reset()
    {
        if (_scene is null)
        {
            return;
        }

        _view.SetMesh(_scene.BindPose);
        _frame = 0;
    }
}
