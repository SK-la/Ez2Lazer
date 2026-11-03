// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// Tiny invisible component whose only job is to grab the live <see cref="AudioManager"/> from
    /// gameplay-side DI and stash it in <see cref="BmsRuntimeAudioContext"/> so that non-Drawable BMS
    /// helpers (skin transformer, sample skin, ...) can resolve external keysound files without
    /// needing osu.Game-side hooks.
    /// </summary>
    internal partial class BmsRuntimeAudioRegistrar : Drawable
    {
        [BackgroundDependencyLoader]
        private void load(AudioManager audioManager)
        {
            BmsRuntimeAudioContext.Register(audioManager);
        }
    }
}
