// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Audio
{
    /// <summary>
    /// Ez2Lazer defaults for ASIO behaviour. Edit <see cref="VirtualHostWarmUpNamePatterns"/> to match your virtual ASIO drivers.
    /// </summary>
    public static class EzAsioAudioDefaults
    {
        /// <summary>
        /// Case-insensitive substrings: if an ASIO device name contains any entry, host audio warm-up runs before ASIO init.
        /// Add your bridge drivers here (e.g. "reaper", "jack", "pulse").
        /// </summary>
        public static IReadOnlyList<string> VirtualHostWarmUpNamePatterns { get; set; } = new[]
        {
            "asio4all",
            "voicemeeter",
            "vb-audio",
            "vb audio",
            "flexasio",
            "generic low latency",
        };
    }
}
