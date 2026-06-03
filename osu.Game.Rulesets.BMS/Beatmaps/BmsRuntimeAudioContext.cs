// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Game.Rulesets.BMS.Audio;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Runtime context for non-Drawable BMS components during gameplay.
    /// </summary>
    public static class BmsRuntimeAudioContext
    {
        public static AudioManager? Audio { get; private set; }

        public static BmsKeysoundManager? KeysoundManager { get; private set; }

        internal static void Register(AudioManager? audioManager)
        {
            if (audioManager != null)
                Audio = audioManager;
        }

        public static void RegisterKeysoundManager(BmsKeysoundManager? manager)
        {
            KeysoundManager = manager;
        }

        public static void Clear()
        {
            KeysoundManager = null;
        }
    }
}
