// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class ManiaScoreProcessor : ScoreProcessor
    {
        private const double combo_base = 4;

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
                    return 305;
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

        protected override void UpdateClassicBaseScore(JudgementResult judgement)
        {
            if (!IsLegacyScore) return;

            var hitWindows = new ManiaHitWindows
            {
                ClassicModActive = true,
                IsConvert = false,
                ScoreV2Active = false
            };

            perfectRange = hitWindows.WindowFor(HitResult.Perfect);
            greatRange = hitWindows.WindowFor(HitResult.Great);
            goodRange = hitWindows.WindowFor(HitResult.Good);
            okRange = hitWindows.WindowFor(HitResult.Ok);
            mehRange = hitWindows.WindowFor(HitResult.Meh);
            missRange = hitWindows.WindowFor(HitResult.Miss);

            double offset = Math.Abs(judgement.TimeOffset);
            var hitObject = (ManiaHitObject)judgement.HitObject;

            if (hitObject is HeadNote)
            {
                headOffsets[hitObject.Column] = offset;
            }
            else if (hitObject is TailNote)
            {
                ClassicMaxBaseScore += 300;
                ClassicBaseScore += getLNScore(headOffsets[hitObject.Column], offset);
                headOffsets[hitObject.Column] = 0;
            }
            else if (hitObject is Note)
            {
                ClassicMaxBaseScore += 300;
                HitResult legacyHitResult = offset <= perfectRange ? HitResult.Perfect :
                                            offset <= greatRange ? HitResult.Great :
                                            offset <= goodRange ? HitResult.Good :
                                            offset <= okRange ? HitResult.Ok :
                                            offset <= mehRange ? HitResult.Meh :
                                            HitResult.Miss;

                ClassicBaseScore += legacyHitResult switch
                {
                    HitResult.Perfect => 300,
                    HitResult.Great => 300,
                    HitResult.Good => 200,
                    HitResult.Ok => 100,
                    HitResult.Meh => 50,
                    _ => 0
                };
            }
        }

        private double getLNScore(double head, double tail)
        {
            double combined = head + tail;

            (double range, double headFactor, double combinedFactor, double score)[] rules = new[]
            {
                (PerfectRange: perfectRange, 1.2, 2.4, 300.0),
                (greatRange, 1.1, 2.2, 300),
                (goodRange, 1.0, 2.0, 200),
                (okRange, 1.0, 2.0, 100),
                (mehRange, 1.0, 2.0, 50),
            };

            foreach (var (range, headFactor, combinedFactor, score) in rules)
            {
                if (head <= range * headFactor && combined < range * combinedFactor)
                    return score;
            }

            return 0;
        }

        private readonly double[] headOffsets = new double[18];

        private double perfectRange;
        private double greatRange;
        private double goodRange;
        private double okRange;
        private double mehRange;
        private double missRange;

        protected override void RevertClassicBaseScore(JudgementResult judgement)
        {
            if (!IsLegacyScore) return;

            var hitWindows = new ManiaHitWindows
            {
                ClassicModActive = true,
                IsConvert = false,
                ScoreV2Active = false
            };

            perfectRange = hitWindows.WindowFor(HitResult.Perfect);
            greatRange = hitWindows.WindowFor(HitResult.Great);
            goodRange = hitWindows.WindowFor(HitResult.Good);
            okRange = hitWindows.WindowFor(HitResult.Ok);
            mehRange = hitWindows.WindowFor(HitResult.Meh);
            missRange = hitWindows.WindowFor(HitResult.Miss);

            double offset = Math.Abs(judgement.TimeOffset);
            var hitObject = (ManiaHitObject)judgement.HitObject;

            if (hitObject is HeadNote)
            {
                headOffsets[hitObject.Column] = offset;
            }
            else if (hitObject is TailNote)
            {
                ClassicMaxBaseScore -= 300;
                ClassicBaseScore -= getLNScore(headOffsets[hitObject.Column], offset);
                headOffsets[hitObject.Column] = 0;
            }
            else if (hitObject is Note)
            {
                ClassicMaxBaseScore -= 300;
                HitResult legacyHitResult = offset <= perfectRange ? HitResult.Perfect :
                    offset <= greatRange ? HitResult.Great :
                    offset <= goodRange ? HitResult.Good :
                    offset <= okRange ? HitResult.Ok :
                    offset <= mehRange ? HitResult.Meh :
                    HitResult.Miss;

                ClassicBaseScore -= legacyHitResult switch
                {
                    HitResult.Perfect => 300,
                    HitResult.Great => 300,
                    HitResult.Good => 200,
                    HitResult.Ok => 100,
                    HitResult.Meh => 50,
                    _ => 0
                };
            }
        }
    }
}
