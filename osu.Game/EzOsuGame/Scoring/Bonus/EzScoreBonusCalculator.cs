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
    public readonly record struct EzScoreBonusResult(int JudgeBonus, int MissPenalty, int CountedNotes)
    {
        public int Total => JudgeBonus + MissPenalty;
    }

    /// <summary>
    /// Mania 附加分计算：判定加成（KPS 加权 offset 精度 × 覆盖率）与 Miss 罚分（按次扣分、按所在区间 KPS 加权，有上限）。
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

            double judgeHighSum = 0;
            double judgeLowSum = 0;
            double missHighSum = 0;
            double missLowSum = 0;
            int counted = 0;

            foreach (var e in hitEvents)
            {
                if (!e.Result.IsBasic() || e.Result == HitResult.Poor || e.HitObject == null)
                    continue;

                if (!pending.Remove(keyOf(e.HitObject)))
                    continue;

                counted++;
                double sectionKps = kps.KpsAt(e.HitObject.StartTime);

                if (e.Result == HitResult.Miss)
                {
                    missHighSum += EzScoreBonusFormula.MissWeight(sectionKps, favourHighKps: true);
                    missLowSum += EzScoreBonusFormula.MissWeight(sectionKps, favourHighKps: false);
                    continue;
                }

                double eventRate = e.GameplayRate is > 0 ? e.GameplayRate.Value : rate;
                double quality = EzScoreBonusFormula.OffsetQuality(e.TimeOffset / eventRate);

                judgeHighSum += EzScoreBonusFormula.JudgeWeight(sectionKps, favourHighKps: true) * quality;
                judgeLowSum += EzScoreBonusFormula.JudgeWeight(sectionKps, favourHighKps: false) * quality;
            }

            if (counted == 0)
                return new EzScoreBonusSet(default, default);

            return new EzScoreBonusSet(
                JudgeToMiss: new EzScoreBonusResult(judgeBonus(judgeLowSum, counted), EzScoreBonusFormula.MissPenalty(missHighSum), counted),
                MissToJudge: new EzScoreBonusResult(judgeBonus(judgeHighSum, counted), EzScoreBonusFormula.MissPenalty(missLowSum), counted));
        }

        private static int judgeBonus(double weightedQualitySum, int counted)
            => Math.Clamp((int)Math.Round(EzScoreBonusFormula.JUDGE_BONUS_MAX * weightedQualitySum / counted), 0, EzScoreBonusFormula.JUDGE_BONUS_MAX);

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
