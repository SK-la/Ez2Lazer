// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Detects mods that change playable beatmap layout, difficulty, or rate — anything that
    /// invalidates NoMod SQLite analysis / raw BeatmapInfo fields for local profile stats.
    /// </summary>
    internal static class EzLocalProfileModAffects
    {
        public static bool AffectsPlayableAnalysis(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                return false;

            return mods.Any(modAffectsPlayableAnalysis);
        }

        public static bool AffectsPlayableAnalysis(Mod mod) => modAffectsPlayableAnalysis(mod);

        private static bool modAffectsPlayableAnalysis(Mod mod)
            => mod is IApplicableToRate
                      or IApplicableToBeatmapConverter
                      or IApplicableAfterBeatmapConversion
                      or IApplicableToDifficulty
                      or IApplicableToBeatmapProcessor
                      or IApplicableToHitObject
                      or IApplicableToBeatmap;
    }
}
