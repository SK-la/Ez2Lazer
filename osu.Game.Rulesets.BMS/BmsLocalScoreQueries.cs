// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS
{
    /// <summary>
    /// Local score lookups for BMS beatmaps indexed in Realm (MD5 + ruleset short name "bms").
    /// </summary>
    internal static class BmsLocalScoreQueries
    {
        private const string bms_ruleset_short_name = "bms";

        public static ScoreInfo? GetBestLocalScore(RealmAccess realm, string beatmapMd5)
        {
            if (string.IsNullOrEmpty(beatmapMd5))
                return null;

            return realm.Run(r =>
            {
                var bmsRuleset = r.Find<osu.Game.Rulesets.RulesetInfo>(bms_ruleset_short_name);
                if (bmsRuleset == null)
                    return null;

                BeatmapInfo? bm = r.All<BeatmapInfo>()
                                   .FirstOrDefault(b => b.MD5Hash == beatmapMd5 && b.Ruleset == bmsRuleset);
                if (bm == null)
                    return null;

                ScoreInfo? best = bm.Scores.Where(s => !s.DeletePending).OrderByDescending(s => s.TotalScore).FirstOrDefault();
                return best?.Detach();
            });
        }

        public static List<ScoreInfo> GetTopLocalScores(RealmAccess realm, string beatmapMd5, int take = 5)
        {
            if (string.IsNullOrEmpty(beatmapMd5))
                return new List<ScoreInfo>();

            return realm.Run(r =>
            {
                var bmsRuleset = r.Find<osu.Game.Rulesets.RulesetInfo>(bms_ruleset_short_name);
                if (bmsRuleset == null)
                    return new List<ScoreInfo>();

                BeatmapInfo? bm = r.All<BeatmapInfo>()
                                   .FirstOrDefault(b => b.MD5Hash == beatmapMd5 && b.Ruleset == bmsRuleset);
                if (bm == null)
                    return new List<ScoreInfo>();

                // Realm does not support translating Take() on this collection query path.
                // Materialise first, then apply ordering/Take in-memory.
                return bm.Scores
                         .AsEnumerable()
                         .Where(s => !s.DeletePending)
                         .OrderByDescending(s => s.TotalScore)
                         .Take(take)
                         .Select(s => s.Detach())
                         .ToList();
            });
        }
    }

    /// <summary>
    /// Clear lamp colours for BMS song select; maps osu <see cref="ScoreRank"/> to lamp hues (not BMS lamp tier names).
    /// </summary>
    internal static class BmsClearLampColour
    {
        public static Color4 ForBestScore(ScoreInfo? best)
        {
            if (best == null)
                return no_play;

            return ForRank(best.Rank);
        }

        public static Color4 ForRank(ScoreRank rank)
        {
            switch (rank)
            {
                case ScoreRank.F:
                    return new Color4(0.92f, 0.22f, 0.22f, 1f);

                case ScoreRank.D:
                    return new Color4(0.55f, 0.38f, 0.28f, 1f);

                case ScoreRank.C:
                    return new Color4(0.28f, 0.48f, 0.92f, 1f);

                case ScoreRank.B:
                    return new Color4(0.32f, 0.78f, 0.42f, 1f);

                case ScoreRank.A:
                    return new Color4(0.95f, 0.86f, 0.22f, 1f);

                case ScoreRank.S:
                case ScoreRank.SH:
                    return new Color4(1f, 0.62f, 0.12f, 1f);

                case ScoreRank.X:
                case ScoreRank.XH:
                    return new Color4(0.22f, 0.92f, 0.92f, 1f);

                default:
                    return new Color4(0.5f, 0.5f, 0.5f, 1f);
            }
        }

        private static readonly Color4 no_play = new Color4(0.25f, 0.25f, 0.25f, 1f);
    }
}
