// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Games;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class PosePreviewTests
{
    [Fact]
    public async Task FailedLoadClearsPreviousAvailabilityAndReportsFalse()
    {
        using TemporaryGame game = new();
        using PosePreview preview = new();
        FieldInfo frame = typeof(PosePreview).GetField(
            "_frame", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo applied = typeof(PosePreview).GetField(
            "_applied", BindingFlags.Instance | BindingFlags.NonPublic)!;
        frame.SetValue(preview, 7);
        applied.SetValue(preview, true);

        bool loaded = await preview.LoadAsync(
            game.Profile,
            Array.Empty<AssetEntry>(),
            Entry(),
            0x1234,
            Entry(),
            CancellationToken.None);

        Assert.False(loaded);
        Assert.False(preview.HasPose);
        Assert.False(preview.IsApplied);
        Assert.Equal(0, preview.Frame);
        Assert.Equal(0, preview.FrameCount);
    }

    [Fact]
    public async Task DisposedLoadCompletesWithoutThrowing()
    {
        using TemporaryGame game = new();
        PosePreview preview = new();

        Task<bool> load = preview.LoadAsync(
            game.Profile,
            Array.Empty<AssetEntry>(),
            Entry(),
            0x1234,
            Entry(),
            CancellationToken.None);
        preview.Dispose();

        Assert.False(await load);
    }

    private static AssetEntry Entry() => new(
        @"C:\game\DATAS\A.DPC",
        @"C:\game\DATAS\A.DPC",
        false,
        new DpcEntry(1, AssetClass.Skin, 0, 0, 0, 0, 0, 0, 0),
        null,
        null);

    private sealed class TemporaryGame : IDisposable
    {
        public TemporaryGame()
        {
            Root = Directory.CreateTempSubdirectory("aptstudio-pose-").FullName;
            File.WriteAllBytes(Path.Combine(Root, "Resonance.exe"), []);
            foreach (string folder in new[] { "DATAS", "FONT", "LEVELS" })
            {
                Directory.CreateDirectory(Path.Combine(Root, folder));
            }

            if (!GameProfile.TryDetect(Root, out GameProfile? profile, out string? error))
            {
                throw new InvalidOperationException(error ?? "profile not detected");
            }

            Profile = profile!;
        }

        public string Root { get; }

        public GameProfile Profile { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
