using System.Drawing;

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

    public void SetListIndex(int index)
    {
        State = State with { ListIndex = index };
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
