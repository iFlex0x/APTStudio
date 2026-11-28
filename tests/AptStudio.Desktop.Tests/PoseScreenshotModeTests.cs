// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;

namespace AptStudio.Desktop.Tests;

public sealed class PoseScreenshotModeTests
{
    private static string ExePath =>
        Path.Combine(AppContext.BaseDirectory, "AptStudio.Desktop.exe");

    [Fact]
    public void FlagAfterPoseshotIsNotTheOutputPath()
    {
        WithFolder(folder =>
        {
            (int code, _) = Run(folder, "--poseshot", "--game", folder, "--skin", "1");

            Assert.Equal(0, code);
            Assert.False(File.Exists(Path.Combine(folder, "--game")));
            Assert.False(File.Exists(Path.Combine(folder, "--game.log")));
            Assert.Contains(
                "Resonance.exe",
                File.ReadAllText(Path.Combine(folder, "pose.png.log")));
        });
    }

    [Fact]
    public void ExistingImageSurvivesAFailedRun()
    {
        WithFolder(folder =>
        {
            string image = Path.Combine(folder, "keep.png");
            byte[] original = [0x89, 0x50, 0x4E, 0x47, 0x01];
            File.WriteAllBytes(image, original);

            (int code, _) = Run(
                folder, "--poseshot", "keep.png", "--game", folder, "--skin", "1");

            Assert.Equal(0, code);
            Assert.Equal(original, File.ReadAllBytes(image));
            Assert.True(File.Exists(image + ".log"));
        });
    }

    [Fact]
    public void RefusesOutputInsideTheGameFolderBeforeWritingAnything()
    {
        WithFolder(fakeGame =>
        {
            File.WriteAllText(Path.Combine(fakeGame, "Resonance.exe"), "");
            foreach (string name in new[] { "DATAS", "FONT", "LEVELS" })
            {
                Directory.CreateDirectory(Path.Combine(fakeGame, name));
            }

            WithFolder(work =>
            {
                string output = Path.Combine(fakeGame, "capture", "pose.png");
                (int code, string error) = Run(
                    work, "--poseshot", output, "--game", fakeGame, "--skin", "1");

                Assert.Equal(0, code);
                Assert.Contains("inside the game folder", error);
                Assert.False(File.Exists(output));
                Assert.False(File.Exists(output + ".log"));
                Assert.False(Directory.Exists(Path.Combine(fakeGame, "capture")));
                Assert.Empty(Directory.GetFiles(work));
            });
        });
    }

    private static (int Code, string Error) Run(string workingDirectory, params string[] args)
    {
        Assert.True(File.Exists(ExePath), $"{ExePath} was not built.");
        ProcessStartInfo start = new(ExePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The screenshot process did not start.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("The screenshot process did not exit.");
        }

        Task.WaitAll(error, output);
        return (process.ExitCode, error.GetAwaiter().GetResult());
    }

    private static void WithFolder(Action<string> action)
    {
        string folder = Path.Combine(
            Path.GetTempPath(), "aptstudio-poseshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            action(folder);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
