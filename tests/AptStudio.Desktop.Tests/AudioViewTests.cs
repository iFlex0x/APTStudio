using System.Reflection;
using AptStudio.Core.Audio;

namespace AptStudio.Desktop.Tests;

public sealed class AudioViewTests
{
    private static readonly AudioEntry Entry = new(
        "C:\\game\\SOUNDBANKS\\MUS_MEDIA.PC.PCK",
        "MUS_MEDIA.PC.PCK",
        0x676418D8,
        0x27F2B,
        74_880,
        2_831_292,
        0,
        "sfx",
        new WemInfo(0xFFFF, 2, 44_100, 4_456_421, "mus_test"),
        "mus_test");

    [Fact]
    public void SetAudioShowsEntryAndEnablesButtons()
    {
        using AudioView view = new();
        view.CreateControl();
        view.SetAudio(Entry);

        Assert.Same(Entry, view.Entry);
        Assert.True(FindButton(view, "Play").Enabled);
        Assert.True(FindButton(view, "Export Ogg").Enabled);
    }

    [Fact]
    public void ResetClearsEntry()
    {
        using AudioView view = new();
        view.CreateControl();
        view.SetAudio(Entry);
        view.Reset();

        Assert.Null(view.Entry);
        Assert.False(FindButton(view, "Play").Enabled);
        Assert.False(FindButton(view, "Export Ogg").Enabled);
    }

    [Fact]
    public void PlayAndExportRaiseRequests()
    {
        using AudioView view = new();
        view.CreateControl();
        view.SetAudio(Entry);
        int plays = 0;
        int exports = 0;
        view.PlayRequested += () => plays++;
        view.ExportRequested += () => exports++;

        FindButton(view, "Play").PerformClick();
        FindButton(view, "Export Ogg").PerformClick();

        Assert.Equal(1, plays);
        Assert.Equal(1, exports);
    }

    [Fact]
    public void ExportStatusRestoresButtons()
    {
        using AudioView view = new();
        view.CreateControl();
        view.SetAudio(Entry);
        view.SetBusy("Exporting...");
        Assert.False(FindButton(view, "Play").Enabled);
        Assert.False(FindButton(view, "Export Ogg").Enabled);

        view.SetStatus("Exported file.ogg");

        Assert.True(FindButton(view, "Play").Enabled);
        Assert.True(FindButton(view, "Export Ogg").Enabled);
    }

    [Fact]
    public void ScalesWithoutThrowing()
    {
        using AudioView view = new();
        view.CreateControl();
        view.SetDpiScale(1.5f);
        view.SetAudio(Entry);
        view.SetDpiScale(2f);
    }

    [Fact]
    public void ExportFollowsTheSharedWriteGateAcrossSelectionAndBusyStates()
    {
        using AudioView view = new();
        view.CreateControl();
        Button export = FindButton(view, "Export Ogg");
        Button play = FindButton(view, "Play");

        view.SetExportBlocked(true);
        view.SetAudio(Entry);

        Assert.False(export.Enabled);
        Assert.True(play.Enabled);

        view.SetBusy("Converting...");
        view.SetExportBlocked(false);

        Assert.False(export.Enabled);
        Assert.False(play.Enabled);

        view.SetStatus("Ready");

        Assert.True(export.Enabled);
        Assert.True(play.Enabled);

        view.SetExportBlocked(true);
        view.SetAudio(Entry);

        Assert.False(export.Enabled);

        view.SetExportBlocked(false);
        view.SetStatus("Ready");

        Assert.True(export.Enabled);
    }

    [Fact]
    public void PlaybackCompletionKeepsExportBlocked()
    {
        using AudioView view = new();
        view.CreateControl();
        view.SetAudio(Entry);
        Button export = FindButton(view, "Export Ogg");
        view.SetExportBlocked(true);

        System.Windows.Forms.Timer timer =
            (System.Windows.Forms.Timer)typeof(AudioView)
                .GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(view)!;
        MethodInfo tick = timer.GetType().GetMethod(
            "OnTick", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("OnTick not found.");
        tick.Invoke(timer, [EventArgs.Empty]);

        Assert.False(export.Enabled);

        view.SetExportBlocked(false);

        Assert.True(export.Enabled);
    }

    private static Button FindButton(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            Button? found = FindButtonOrNull(child, text);
            if (found is not null)
            {
                return found;
            }
        }

        throw new InvalidOperationException($"No button '{text}'.");
    }

    private static Button? FindButtonOrNull(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            Button? found = FindButtonOrNull(child, text);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
