// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// PPP skill engine for osu, taiko, catch, and mania.
    /// Chart keys are <c>beatmap_ppp.{metric}</c>.
    /// </summary>
    public interface IEzPPPEngine
    {
        /// <summary>Chart skills for an already playable beatmap (mods and clock rate applied).</summary>
        IReadOnlyDictionary<string, double> CalculateChart(IBeatmap beatmap, IReadOnlyList<Mod> mods);

        /// <summary>
        /// Per-play skills. The default curve scales chart values by accuracy and combo and can be replaced per mode.
        /// </summary>
        IReadOnlyDictionary<string, double> CalculatePlay(IReadOnlyDictionary<string, double> chartSkills, ScoreInfo score);
    }
}
