using System.Drawing;
using AptStudio.Core.Audio;

namespace AptStudio.Desktop.Browser;

internal sealed class BrowserNavigation
{
    private readonly Stack<BrowserState> _back = new();
    private readonly Stack<BrowserState> _forward = new();
    private BrowserState? _chooserSource;

    public BrowserNavigation(BrowserState initial)
    {
        State = initial ?? throw new ArgumentNullException(nameof(initial));
    }

    public BrowserState State { get; private set; }

    public bool IsChooserOpen => _chooserSource is not null;

    public bool CanBack => IsChooserOpen || _back.Count > 0;

    public bool CanForward => !IsChooserOpen && _forward.Count > 0;

    public void SetQuery(string query)
    {
        State = State with { Query = query ?? "" };
    }

    public void SetFilter(TextureFilter filter)
    {
        State = State with { Filter = filter };
    }

    public void SelectTexture(TextureOccurrence texture, int listIndex)
    {
        ArgumentNullException.ThrowIfNull(texture);
        State = State with
        {
            Texture = texture,
            ListIndex = listIndex,
            Mip = 0,
            Fit = true,
            Zoom = 1f,
            Pan = PointF.Empty,
        };
    }

    public void SetTextureCopy(TextureOccurrence texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        State = State with { Texture = texture };
    }

    public void SetLinkedTextureCopy(TextureOccurrence texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        State = State with { LinkedTexture = texture };
    }

    public void SetAnimation(AnimationOccurrence animation, int listIndex)
    {
        ArgumentNullException.ThrowIfNull(animation);
        State = State with
        {
            Animation = animation,
            ListIndex = listIndex,
        };
    }

    public void SelectMesh(MeshOccurrence mesh, int listIndex)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        State = State with
        {
            View = BrowserView.Meshes,
            Mesh = mesh,
            MeshCamera = null,
            MeshWireframe = false,
            Skin = null,
            SkinPart = 0,
            Skeleton = null,
            ListIndex = listIndex,
        };
    }

    public void OpenMeshFromSkin(MeshOccurrence mesh, int listIndex)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _back.Push(State);
        _forward.Clear();
        State = State with
        {
            View = BrowserView.Meshes,
            ArchivePath = mesh.ArchivePath,
            Query = "",
            Mesh = mesh,
            MeshCamera = null,
            MeshWireframe = false,
            Skin = null,
            SkinPart = 0,
            Skeleton = null,
            ListIndex = listIndex,
        };
    }

    public void SetMeshView(MeshCamera camera, bool wireframe)
    {
        State = State with { MeshCamera = camera, MeshWireframe = wireframe };
    }

    public void OpenSkin(SkinOccurrence skin, int part)
    {
        ArgumentNullException.ThrowIfNull(skin);
        _back.Push(State);
        _forward.Clear();
        State = State with
        {
            View = BrowserView.Skin,
            Skin = skin,
            SkinPart = part,
            Skeleton = null,
        };
    }

    public void SetSkinPart(int part)
    {
        State = State with { SkinPart = part };
    }

    public void SetSkinListSelection(SkinOccurrence skin, int listIndex)
    {
        ArgumentNullException.ThrowIfNull(skin);
        State = State with
        {
            View = BrowserView.Skins,
            Skin = skin,
            SkinPart = 0,
            Skeleton = null,
            ListIndex = listIndex,
        };
    }

    public void SetSkeletonListSelection(SkeletonOccurrence skeleton, int listIndex)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        State = State with
        {
            View = BrowserView.Skeletons,
            Skin = null,
            Skeleton = skeleton,
            ListIndex = listIndex,
        };
    }

    public void OpenSkeleton(SkeletonOccurrence skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        _back.Push(State);
        _forward.Clear();
        State = State with
        {
            View = BrowserView.Skeleton,
            Skeleton = skeleton,
        };
    }

    public void SelectAudio(AudioEntry audio, int listIndex)
    {
        ArgumentNullException.ThrowIfNull(audio);
        State = State with
        {
            View = BrowserView.Audio,
            Audio = audio,
            ListIndex = listIndex,
        };
    }

    public void ClearAudio()
    {
        State = State with { Audio = null };
    }

    public void SetAudioFilter(string? pack, string? language)
    {
        State = State with { AudioPack = pack, AudioLanguage = language };
    }

    public void SetAudioSort(AudioSort sort, bool descending)
    {
        State = State with { AudioSort = sort, AudioSortDescending = descending };
    }

    public void SetSort(TextureSort sort, bool descending)
    {
        State = State with { Sort = sort, SortDescending = descending };
    }

    public void SetListIndex(int index)
    {
        State = State with { ListIndex = index };
    }

    public void SetListScroll(int scroll)
    {
        State = State with { ListScroll = Math.Max(0, scroll) };
    }

    public void SetMip(int mip)
    {
        State = State with { Mip = mip };
    }

    public void SetChannel(ChannelView channel)
    {
        State = State with { Channel = channel };
    }

    public void SetFit(bool fit)
    {
        State = State with
        {
            Fit = fit,
            Zoom = fit ? 1f : State.Zoom,
            Pan = fit ? PointF.Empty : State.Pan,
        };
    }

    public void SetView(float zoom, PointF pan)
    {
        State = State with { Fit = false, Zoom = zoom, Pan = pan };
    }

    public void OpenChooser()
    {
        _chooserSource = State;
        _forward.Clear();
    }

    public void DismissChooser()
    {
        _chooserSource = null;
    }

    public void OpenMaterial(MaterialOccurrence material, int slot)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (_chooserSource is null)
        {
            throw new InvalidOperationException("Open a material chooser first.");
        }

        _back.Push(_chooserSource);
        _forward.Clear();
        _chooserSource = null;
        State = State with
        {
            View = BrowserView.Material,
            Material = material,
            MaterialSlot = slot,
            MaterialFromList = false,
            MaterialError = null,
            LinkedTexture = null,
        };
    }

    public void SetMaterialListSelection(
        MaterialOccurrence material, int listIndex, int listScroll = 0)
    {
        ArgumentNullException.ThrowIfNull(material);
        State = State with
        {
            View = BrowserView.Materials,
            Material = material,
            MaterialSlot = -1,
            MaterialFromList = true,
            MaterialError = null,
            ListIndex = listIndex,
            ListScroll = Math.Max(0, listScroll),
        };
    }

    public void OpenMaterialFromList(MaterialOccurrence material, int slot, string? error = null)
    {
        ArgumentNullException.ThrowIfNull(material);
        _back.Push(State);
        _forward.Clear();
        _chooserSource = null;
        State = State with
        {
            View = BrowserView.Material,
            Material = material,
            MaterialSlot = slot,
            MaterialFromList = true,
            MaterialError = error,
            LinkedTexture = null,
        };
    }

    public void SelectSlot(int slot)
    {
        State = State with { MaterialSlot = slot };
    }

    public void OpenLinkedTexture(TextureOccurrence texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        _back.Push(State);
        _forward.Clear();
        State = State with
        {
            View = BrowserView.LinkedTexture,
            LinkedTexture = texture,
            Mip = 0,
            Fit = true,
            Zoom = 1f,
            Pan = PointF.Empty,
        };
    }

    public void RevealLinkedTexture()
    {
        TextureOccurrence? linked = State.LinkedTexture;
        if (linked is null)
        {
            return;
        }

        _back.Push(State);
        _forward.Clear();
        State = State with
        {
            View = BrowserView.Textures,
            ArchivePath = linked.ArchivePath,
            Query = "",
            Filter = TextureFilter.All,
            Texture = linked,
            LinkedTexture = null,
            ListIndex = 0,
            Mip = 0,
            Fit = true,
            Zoom = 1f,
            Pan = PointF.Empty,
        };
    }

    public void SwitchScope(string archivePath, BrowserView view, string placeholderName = "")
    {
        _back.Clear();
        _forward.Clear();
        _chooserSource = null;
        State = new BrowserState(
            view,
            archivePath,
            "",
            TextureFilter.All,
            null,
            0,
            0,
            ChannelView.Rgb,
            true,
            1f,
            PointF.Empty,
            null,
            0,
            null,
            null,
            placeholderName);
    }

    public void Back()
    {
        if (IsChooserOpen)
        {
            DismissChooser();
            return;
        }

        if (_back.Count == 0)
        {
            return;
        }

        _forward.Push(State);
        State = _back.Pop();
    }

    public void Forward()
    {
        if (IsChooserOpen || _forward.Count == 0)
        {
            return;
        }

        _back.Push(State);
        State = _forward.Pop();
    }

    public void Reset(BrowserState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _back.Clear();
        _forward.Clear();
        _chooserSource = null;
        State = state;
    }
}
