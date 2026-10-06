// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Race / TimelineBuilder 取「可喂 Session」的 playable：经 <see cref="EzPlayableBeatmapCache.GetBound"/>，
    /// 禁止 <see cref="EzPlayableBeatmapCache.GetShared"/>（Session 会就地 ApplyDefaults）。
    /// </summary>
    public static class EzScoreRacePlayableResolver
    {
        /// <summary>
        /// 避开 Mania hitmode+1（通常为小数）占用带；同一 working+mods 的 Session 就绪谱共享此 scope。
        /// </summary>
        public const int SESSION_READY_SCOPE = 1000;

        public static IBeatmap GetSessionReady(IWorkingBeatmap working, IRulesetInfo ruleset, IReadOnlyList<Mod>? mods = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(working);
            ArgumentNullException.ThrowIfNull(ruleset);

            Mod[] stripped = mods == null ? Array.Empty<Mod>() : EzModCompatibility.StripUnknown(mods);
            return EzPlayableBeatmapCache.GetBound(working, ruleset, stripped, SESSION_READY_SCOPE,
                beatmap => prepareSessionReady(beatmap, ruleset),
                cancellationToken == CancellationToken.None ? null : cancellationToken);
        }

        private static void prepareSessionReady(IBeatmap beatmap, IRulesetInfo rulesetInfo)
        {
            var ruleset = rulesetInfo.CreateInstance();
            var processor = ruleset.CreateBeatmapProcessor(beatmap);
            processor?.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

            processor?.PostProcess();
        }
    }
}
