// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using Ww2Ogg.Core;

namespace AptStudio.Core.Audio;

public static class WemConverter
{
    /// <summary>
    /// Converts a packed Wwise Vorbis WEM to an Ogg stream. The AoTuV codebook
    /// covers the shipped game; the default table is the fallback for other
    /// Wwise builds.
    /// </summary>
    public static byte[] ToOgg(byte[] wem)
    {
        ArgumentNullException.ThrowIfNull(wem);
        if (wem.Length == 0)
        {
            throw new ArgumentException("WEM is empty.", nameof(wem));
        }

        try
        {
            return Convert(wem, CodebookLibrary.AoTuV);
        }
        catch (WemException)
        {
            try
            {
                return Convert(wem, CodebookLibrary.Default);
            }
            catch (WemException ex)
            {
                throw new InvalidDataException($"WEM conversion failed: {ex.Message}", ex);
            }
        }
    }

    private static byte[] Convert(byte[] wem, CodebookLibrary codebooks)
    {
        using MemoryStream input = new(wem, writable: false);
        using MemoryStream output = new();
        new WwiseRiffVorbis(input, codebooks).GenerateOgg(output);
        return output.ToArray();
    }
}
