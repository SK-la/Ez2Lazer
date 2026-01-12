// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.LAsEZMania.Helper;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Analysis
{
    /// <summary>
    /// Mania-specific implementation of score graph that extends BaseEzScoreGraph.
    /// Provides LN (Long Note) aware scoring calculation for Classic mode.
    /// </summary>
    public partial class EzManiaScoreGraph : BaseEzScoreGraph
    {
        private readonly ManiaHitWindows maniaHitWindows = new ManiaHitWindows();

        public EzManiaScoreGraph(ScoreInfo score, IBeatmap beatmap)
            : base(score, beatmap, new ManiaHitWindows())
        {
            maniaHitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);
        }

        protected override double UpdateBoundary(HitResult result)
        {
            return maniaHitWindows.WindowFor(result);
        }

        /// <summary>
        /// Override to get Mania-specific hit events for V1 calculation.
        /// </summary>
        protected override IEnumerable<HitEvent> GetApplicableHitEvents()
        {
            return Score.HitEvents.Where(e => e.Result.IsBasic());
        }

        private readonly CustomHitWindowsHelper hitWindows1 = new CustomHitWindowsHelper(EzMUGHitMode.Classic)
        {
            OverallDifficulty = OD
        };

        private readonly CustomHitWindowsHelper hitWindows2 = new CustomHitWindowsHelper
        {
            OverallDifficulty = OD
        };

        protected override HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            return hitWindows1.ResultFor(hitEvent.TimeOffset);
        }

        protected override HitResult RecalculateV2Result(HitEvent hitEvent)
        {
            return hitWindows2.ResultFor(hitEvent.TimeOffset);
        }
    }
}

        // protected override void CalculateV1Accuracy()
        // {
        //     base.CalculateV1Accuracy();
        //
        //     var maniaHitWindows = new ManiaHitWindows
        //     {
        //         CustomHitWindows = false,
        //         ClassicModActive = true,
        //         IsConvert = false,
        //         ScoreV2Active = false,
        //         SpeedMultiplier = (HitWindows as ManiaHitWindows)?.SpeedMultiplier ?? 1.0,
        //         DifficultyMultiplier = (HitWindows as ManiaHitWindows)?.DifficultyMultiplier ?? 1.0
        //     };
        //
        //     maniaHitWindows.ResetRange();
        //     maniaHitWindows.SetDifficulty(OD);
        //
        //     double PerfectRange = maniaHitWindows.WindowFor(HitResult.Perfect);
        //     double GreatRange = maniaHitWindows.WindowFor(HitResult.Great);
        //     double GoodRange = maniaHitWindows.WindowFor(HitResult.Good);
        //     double OkRange = maniaHitWindows.WindowFor(HitResult.Ok);
        //     double MehRange = maniaHitWindows.WindowFor(HitResult.Meh);
        //     double MissRange = maniaHitWindows.WindowFor(HitResult.Miss);
        //
        //     Logger.Log($"[EzManiaScoreGraph] V1 HitWindows: P{PerfectRange} G{GreatRange} Go{GoodRange} O{OkRange} M{MehRange} Mi{MissRange}");
        //
        //     double[] HeadOffsets = new double[18];
        //     double MaxPoints = 0;
        //     double TotalPoints = 0;
        //
        //     double getLNScore(double head, double tail)
        //     {
        //         double combined = head + tail;
        //
        //         (double range, double headFactor, double combinedFactor, double scoreVal)[] rules = new[]
        //         {
        //             (PerfectRange, 1.2, 2.4, 300.0),
        //             (GreatRange, 1.1, 2.2, 300.0),
        //             (GoodRange, 1.0, 2.0, 200.0),
        //             (OkRange, 1.0, 2.0, 100.0),
        //             (MehRange, 1.0, 2.0, 50.0),
        //         };
        //
        //         foreach (var (range, headFactor, combinedFactor, scoreValue) in rules)
        //         {
        //             if (head < range * headFactor && combined < range * combinedFactor)
        //             {
        //                 return scoreValue;
        //             }
        //         }
        //
        //         return 0;
        //     }
        //
        //     Dictionary<HitResult, int> v1Counts = new Dictionary<HitResult, int>();
        //
        //     foreach (var hit in Score.HitEvents.Where(e => e.Result.IsBasic()))
        //     {
        //         double offset = Math.Abs(hit.TimeOffset);
        //         HitResult result = maniaHitWindows.ResultFor(offset);
        //         v1Counts[result] = v1Counts.GetValueOrDefault(result, 0) + 1;
        //
        //         var hitObject = (ManiaHitObject)hit.HitObject;
        //
        //         if (hitObject is HeadNote)
        //         {
        //             HeadOffsets[hitObject.Column] = offset;
        //         }
        //         else if (hitObject is TailNote)
        //         {
        //             MaxPoints += 300;
        //             TotalPoints += getLNScore(HeadOffsets[hitObject.Column], offset);
        //             HeadOffsets[hitObject.Column] = 0;
        //         }
        //         else if (hitObject is Note)
        //         {
        //             MaxPoints += 300;
        //             TotalPoints += result switch
        //             {
        //                 HitResult.Perfect => 300,
        //                 HitResult.Great => 300,
        //                 HitResult.Good => 200,
        //                 HitResult.Ok => 100,
        //                 HitResult.Meh => 50,
        //                 _ => 0
        //             };
        //         }
        //     }
        //
        //     double accuracy = MaxPoints > 0 ? TotalPoints / MaxPoints : 0;
        //     V1Accuracy = accuracy;
        //     V1Counts = v1Counts;
        //     // V1Score = TotalPoints;
        // }


