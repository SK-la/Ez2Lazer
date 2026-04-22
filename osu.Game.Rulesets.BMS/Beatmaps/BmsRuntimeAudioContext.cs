// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Provides BMS-only access to the runtime <see cref="AudioManager"/>.
    ///
    /// The plain <see cref="Ruleset"/> extension points (CreateSkinTransformer, CreateBeatmapConverter, ...)
    /// are not Drawable and therefore cannot use [BackgroundDependencyLoader] to obtain the
    /// <see cref="AudioManager"/>. We use a small Drawable registrar that the BMS DrawableRuleset attaches
    /// during gameplay setup; it writes the live <see cref="AudioManager"/> into this context, so other
    /// non-Drawable BMS components (like <see cref="BMSExternalSampleSkin"/>) can lazily read it.
    ///
    /// All access is best-effort: components must tolerate <see cref="Audio"/> being null and fall back
    /// gracefully when no DI host has registered yet.
    /// </summary>
    public static class BmsRuntimeAudioContext
    {
        public static AudioManager? Audio { get; private set; }

        internal static void Register(AudioManager audioManager)
        {
            // Last writer wins; under normal flow there is a single AudioManager per game host instance.
            if (audioManager != null)
                Audio = audioManager;
        }
    }
}
