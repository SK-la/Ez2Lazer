﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class ManiaScoreProcessor : ScoreProcessor
    {
        public List<ManiaHitTimingInfo> HitTimings { get; private set; } = new List<ManiaHitTimingInfo>();

        public void AddHitTiming(double hitTime, HitResult result)
        {
            HitTimings.Add(new ManiaHitTimingInfo(hitTime, result));
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
