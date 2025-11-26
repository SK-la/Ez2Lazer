// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.LAsEzExtensions;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class ManiaScoreProcessor : ScoreProcessor
    {
        public List<EzManiaHitTimingInfo> HitTimings { get; private set; } = new List<EzManiaHitTimingInfo>();

        public void AddHitTiming(double hitTime, HitResult result)
        {
            HitTimings.Add(new EzManiaHitTimingInfo(hitTime, result));
        }

        public int PoolCount { get; private set; }

        public void ApplyPoolResult(double time, int column)
        {
            PoolCount++;
            // Create a dummy JudgementResult for IgnoreHit
            var dummyHitObject = new PoolHitObject(time, column);
            var judgement = new ManiaJudgement();
            var result = new JudgementResult(dummyHitObject, judgement);
            result.Type = HitResult.Pool;
            // Set RawTime and GameplayRate using reflection since they are internal
            typeof(JudgementResult).GetProperty("RawTime")?.SetValue(result, time);
            typeof(JudgementResult).GetProperty("GameplayRate")?.SetValue(result, Clock.Rate);
            ApplyResult(result);
        }

        public double CalculateScoreWithParameters(double comboProgress, double accuracyProgress, double bonusPortion, Dictionary<HitResult, int> customHitProportionScore)
        {
            double totalScore = 0;

            foreach (var hitTiming in HitTimings)
            {
                if (customHitProportionScore.TryGetValue(hitTiming.Result, out int score))
                {
                    totalScore += score;
                }
            }

            totalScore += 150000 * comboProgress
                          + 850000 * Math.Pow(accuracyProgress, 2 + 2 * accuracyProgress) * accuracyProgress
                          + bonusPortion;

            return totalScore;
        }

        private const double combo_base = 4;

        public (int Perfect, int Great, int Good, int Ok, int Meh, int Miss) HitProportionScore = (305, 300, 200, 100, 50, 0);

        public ManiaScoreProcessor()
            : base(new ManiaRuleset())
        {
            if (ManiaBeatmapConverter.CurrentHitMode == EzMUGHitMode.O2Jam)
            {
                // O2JAM hit windows are set in ManiaRuleset.CreateBeatmapConverter
            }
        }

        protected override IEnumerable<HitObject> EnumerateHitObjects(IBeatmap beatmap)
            => base.EnumerateHitObjects(beatmap).Order(JudgementOrderComparer.DEFAULT);

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            return 150000 * comboProgress
                   + 850000 * Math.Pow(Accuracy.Value, 2 + 2 * Accuracy.Value) * accuracyProgress
                   + bonusPortion;
        }

        protected override double GetComboScoreChange(JudgementResult result)
        {
            return getBaseComboScoreForResult(result.Type) * Math.Min(Math.Max(0.5, Math.Log(result.ComboAfterJudgement, combo_base)), Math.Log(400, combo_base));
        }

        public override int GetBaseScoreForResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return HitProportionScore.Perfect;

                case HitResult.Great:
                    return HitProportionScore.Great;

                case HitResult.Good:
                    return HitProportionScore.Good;

                case HitResult.Ok:
                    return HitProportionScore.Ok;

                case HitResult.Meh:
                    return HitProportionScore.Meh;

                case HitResult.Miss:
                    return HitProportionScore.Miss;

                case HitResult.Pool:
                    return 0;
            }

            return base.GetBaseScoreForResult(result);
        }

        private int getBaseComboScoreForResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return 300;
            }

            return GetBaseScoreForResult(result);
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            ScoreRank rank = base.RankFromScore(accuracy, results);

            if (rank != ScoreRank.S)
                return rank;

            // SS is expected as long as all hitobjects have been hit with either a GREAT or PERFECT result.

            bool anyImperfect =
                results.GetValueOrDefault(HitResult.Good) > 0
                || results.GetValueOrDefault(HitResult.Ok) > 0
                || results.GetValueOrDefault(HitResult.Meh) > 0
                || results.GetValueOrDefault(HitResult.Pool) > 0
                || results.GetValueOrDefault(HitResult.Miss) > 0;

            return anyImperfect ? rank : ScoreRank.X;
        }

        private class JudgementOrderComparer : IComparer<HitObject>
        {
            public static readonly JudgementOrderComparer DEFAULT = new JudgementOrderComparer();

            public int Compare(HitObject? x, HitObject? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (ReferenceEquals(x, null)) return -1;
                if (ReferenceEquals(y, null)) return 1;

                int result = x.GetEndTime().CompareTo(y.GetEndTime());
                if (result != 0)
                    return result;

                // due to the way input is handled in mania, notes take precedence over ticks in judging order.
                if (x is Note && y is not Note) return -1;
                if (x is not Note && y is Note) return 1;

                return x is ManiaHitObject maniaX && y is ManiaHitObject maniaY
                    ? maniaX.Column.CompareTo(maniaY.Column)
                    : 0;
            }
        }
    }
}
