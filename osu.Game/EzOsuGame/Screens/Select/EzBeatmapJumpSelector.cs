// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Screens.Select
{
    /// <summary>
    /// Resolves the difficulty the "go to beatmap" action should present, based on the track currently playing.
    /// </summary>
    /// <remarks>
    /// A jump must stay within the track that is playing; only the difficulty (and, as a last resort, the active
    /// ruleset) may change. Any randomisation is confined to the difficulties of that single beatmap set.
    /// </remarks>
    public static class EzBeatmapJumpSelector
    {
        /// <summary>
        /// Picks a difficulty belonging to the beatmap set of <paramref name="playing"/>.
        /// </summary>
        /// <param name="playing">The working beatmap currently driving global music playback.</param>
        /// <param name="activeRuleset">The game-wide active ruleset.</param>
        /// <returns>The difficulty to present, or <c>null</c> if the jump cannot be resolved.</returns>
        public static BeatmapInfo? Select(WorkingBeatmap playing, RulesetInfo activeRuleset)
        {
            var difficulties = playing.BeatmapSetInfo?.Beatmaps;

            if (difficulties == null || difficulties.Count == 0)
                return null;

            BeatmapInfo? playingInSet = difficulties.FirstOrDefault(b => b.Equals(playing.BeatmapInfo));

            // 1. The playing difficulty itself, when it already belongs to the active ruleset.
            if (playingInSet != null && playingInSet.Ruleset.Equals(activeRuleset))
                return playingInSet;

            var activeRulesetDifficulties = difficulties.Where(b => b.Ruleset.Equals(activeRuleset)).ToList();

            // 2. Prefer another difficulty of the same song on the active ruleset, keeping the ruleset unchanged.
            if (activeRulesetDifficulties.Count > 0)
                return activeRulesetDifficulties[RNG.Next(activeRulesetDifficulties.Count)];

            // 3. The song has no difficulty for the active ruleset: follow the playing difficulty and switch ruleset.
            if (playingInSet != null)
                return playingInSet;

            // 4. Last resort: any difficulty of the same song.
            return difficulties[RNG.Next(difficulties.Count)];
        }
    }
}
