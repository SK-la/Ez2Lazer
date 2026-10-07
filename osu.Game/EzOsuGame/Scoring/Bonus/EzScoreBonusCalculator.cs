// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <param name="MissRate">Miss 个数 / 谱面总 Note 数（普通 Note + LN）。</param>
    public readonly record struct EzScoreBonusResult(int JudgeBonus, int MissPenalty, int CountedNotes, double MissRate = 0)
    {
        public int Total => JudgeBonus + MissPenalty;
    }

    /// <summary>
    /// Mania 附加分计算：判定加成（KPS 加权 offset 精度 × 覆盖率）与 Miss 罚分（Miss 率映射到罚分曲线，再乘 Miss 所在区间的平均 KPS 权重）。
    /// 两种 <see cref="EzScoreBonusTendency"/> 一次算出。
    /// </summary>
    /// <remarks>
    /// V1 只统计顶层 Note 与 LN 头（按列 + 起始时间匹配），LN 尾 / body / tick 不参与。
    /// <see cref="HitResult.Poor"/>（BMS 空 POOR）不参与。
    /// </remarks>
    public static class EzScoreBonusCalculator
    {
        public const int MANIA_RULESET_ID = 3;

        public static EzScoreBonusSet Calculate(IBeatmap playableBeatmap, IReadOnlyList<HitEvent> hitEvents, double rate, IEzKpsSectionLookup? kps = null)
        {
            if (rate <= 0 || double.IsNaN(rate))
                rate = 1;

            kps ??= EzKpsListLookup.FromBeatmap(playableBeatmap, rate);

            var pending = new HashSet<(int column, double time)>();

            foreach (var hitObject in playableBeatmap.HitObjects)
                pending.Add(keyOf(hitObject));

            // JudgeToMiss：判定看重低 KPS、Miss 看重高 KPS；MissToJudge 相反。
            var judgeToMiss = new Accumulator(judgeFavoursHighKps: false);
            var missToJudge = new Accumulator(judgeFavoursHighKps: true);
            int counted = 0;

            foreach (var e in hitEvents)
            {
                if (!e.Result.IsBasic() || e.Result == HitResult.Poor || e.HitObject == null)
                    continue;

                if (!pending.Remove(keyOf(e.HitObject)))
                    continue;

                counted++;

                double sectionKps = kps.KpsAt(e.HitObject.StartTime);
                bool isMiss = e.Result == HitResult.Miss;
                double quality = 0;

                if (!isMiss)
                {
                    double eventRate = e.GameplayRate is > 0 ? e.GameplayRate.Value : rate;
                    quality = EzScoreBonusFormula.OffsetQuality(e.TimeOffset / eventRate);
                }

                judgeToMiss.Add(sectionKps, isMiss, quality);
                missToJudge.Add(sectionKps, isMiss, quality);
            }

            if (counted == 0)
                return new EzScoreBonusSet(default, default);

            // 顶层物件数 = 普通 Note + LN（每个 LN 计一次）。
            int totalNotes = playableBeatmap.HitObjects.Count;

            return new EzScoreBonusSet(judgeToMiss.ToResult(counted, totalNotes), missToJudge.ToResult(counted, totalNotes));
        }

        private struct Accumulator
        {
            private readonly bool judgeFavoursHighKps;

            private double judgeQualitySum;
            private double judgeWeightSum;
            private double missWeightSum;
            private int missCount;

            public Accumulator(bool judgeFavoursHighKps)
            {
                this.judgeFavoursHighKps = judgeFavoursHighKps;
                judgeQualitySum = judgeWeightSum = missWeightSum = 0;
                missCount = 0;
            }

            public void Add(double kps, bool isMiss, double quality)
            {
                double judgeWeight = EzScoreBonusFormula.JudgeWeight(kps, judgeFavoursHighKps);

                judgeWeightSum += judgeWeight;
                judgeQualitySum += judgeWeight * quality;

                if (isMiss)
                {
                    missCount++;
                    missWeightSum += EzScoreBonusFormula.MissWeight(kps, !judgeFavoursHighKps);
                }
            }

            public readonly EzScoreBonusResult ToResult(int counted, int totalNotes)
            {
                int judgeBonus = Math.Clamp((int)Math.Round(EzScoreBonusFormula.JUDGE_BONUS_MAX * judgeQualitySum / counted), 0, EzScoreBonusFormula.JUDGE_BONUS_MAX);

                double missRate = (double)missCount / Math.Max(1, totalNotes);
                double missWeight = missCount > 0 ? missWeightSum / missCount : 0;

                return new EzScoreBonusResult(judgeBonus, EzScoreBonusFormula.MissPenalty(missRate, judgeWeightSum / counted, missWeight), counted, missRate);
            }
        }

        /// <summary>
        /// 由 <see cref="ScoreInfo.HitEvents"/> 计算并写入临时字段 <see cref="ScoreInfo.EzBonus"/>；非 mania 或无 HitEvents 时置空。
        /// </summary>
        /// <param name="playableBeatmap">与 <see cref="ScoreInfo.HitEvents"/> 同一次运行的可玩谱面。</param>
        public static void Apply(ScoreInfo score, IBeatmap playableBeatmap)
        {
            if (score.Ruleset.OnlineID != MANIA_RULESET_ID || score.HitEvents.Count == 0 || playableBeatmap.HitObjects.Count == 0)
            {
                score.EzBonus = null;
                return;
            }

            score.EzBonus = Calculate(playableBeatmap, score.HitEvents, ModUtils.CalculateRateWithMods(score.Mods));
        }

        private static (int column, double time) keyOf(HitObject hitObject)
            => (hitObject is IHasColumn c ? c.Column : -1, hitObject.StartTime);
    }
}
