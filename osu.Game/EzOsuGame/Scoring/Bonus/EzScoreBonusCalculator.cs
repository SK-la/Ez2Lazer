// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <param name="MissRate">Miss 个数 / 谱面总 Note 数（普通 Note 各 1，LN 的头和尾各 1）。</param>
    public readonly record struct EzScoreBonusResult(int JudgeBonus, int MissPenalty, int CountedNotes, double MissRate = 0)
    {
        public int Total => JudgeBonus + MissPenalty;
    }

    /// <summary>
    /// Mania 附加分：每颗 Note 的 offset 价值（默认类反余切，另一条是类余切）再乘 KPS 权重。
    /// Miss 按 <see cref="EzScoreBonusFormula.OFFSET_MISS_MS"/> 计入同一条轴。
    /// 两种曲线一次算出。
    /// </summary>
    /// <remarks>
    /// 头和尾都进入价值。Miss 按当前模式判定窗口的 Miss 区间计，没有窗口时用 <see cref="EzScoreBonusFormula.OFFSET_MISS_MS"/>。
    /// <see cref="HitResult.Poor"/>（BMS 空 POOR）不参与。
    /// </remarks>
    public static class EzScoreBonusCalculator
    {
        public static EzScoreBonusSet Calculate(IBeatmap playableBeatmap, IReadOnlyList<HitEvent> hitEvents, double rate, IEzKpsSectionLookup? kps = null)
        {
            if (rate <= 0 || double.IsNaN(rate))
                rate = 1;

            kps ??= EzKpsListLookup.FromBeatmap(playableBeatmap, rate);

            var pending = new HashSet<(int column, double time)>();
            var releaseNotes = new HashSet<(int column, double time)>();

            foreach (var hitObject in playableBeatmap.HitObjects)
            {
                var leaves = judgedLeaves(hitObject);

                if (leaves.Count == 0)
                    continue;

                foreach (var leaf in leaves)
                {
                    if (leaf.StartTime == hitObject.StartTime)
                        pending.Add(keyOf(leaf));
                    else
                        releaseNotes.Add(keyOf(leaf));
                }
            }

            // 两种价值曲线一次算出。MissToJudge 槽位是默认的类反余切，JudgeToMiss 槽位是类余切。
            var cotangent = new Accumulator();
            var inverseCotangent = new Accumulator();
            int counted = 0;

            foreach (var e in hitEvents)
            {
                if (!e.Result.IsBasic() || e.Result == HitResult.Poor || e.HitObject == null)
                    continue;

                var key = keyOf(e.HitObject);

                if (!pending.Remove(key) && !releaseNotes.Remove(key))
                    continue;

                counted++;

                double sectionKps = kps.KpsAt(e.HitObject.StartTime);
                double weight = EzScoreBonusFormula.JudgeWeight(sectionKps, favourHighKps: true);
                double gameplayRate = e.GameplayRate is > 0 ? e.GameplayRate.Value : rate;
                bool isMiss = e.Result == HitResult.Miss;
                double missBoundary = resolveMissBoundary(e.HitObject, gameplayRate);
                double errorMs = isMiss ? missBoundary : Math.Abs(e.TimeOffset) / gameplayRate;

                cotangent.Add(weight, EzScoreBonusFormula.OffsetQuality(errorMs, cotangent: true, missBoundary));
                inverseCotangent.Add(weight, EzScoreBonusFormula.OffsetQuality(errorMs, missBoundaryMs: missBoundary));
            }

            if (counted == 0)
                return new EzScoreBonusSet(default, default);

            return new EzScoreBonusSet(cotangent.ToResult(counted), inverseCotangent.ToResult(counted));
        }

        private struct Accumulator
        {
            private double positive;
            private double negative;

            public void Add(double weight, double quality)
            {
                double weighted = weight * quality;

                if (weighted >= 0)
                    positive += weighted;
                else
                    negative += weighted;
            }

            public readonly EzScoreBonusResult ToResult(int counted)
            {
                if (counted == 0)
                    return default;

                int judgeBonus = (int)Math.Round(EzScoreBonusFormula.JUDGE_BONUS_MAX * positive / counted);
                int missPenalty = (int)Math.Round(EzScoreBonusFormula.JUDGE_BONUS_MAX * negative / counted);

                return new EzScoreBonusResult(
                    Math.Clamp(judgeBonus, 0, EzScoreBonusFormula.JUDGE_BONUS_MAX),
                    Math.Clamp(missPenalty, -EzScoreBonusFormula.JUDGE_BONUS_MAX, 0),
                    counted);
            }
        }

        /// <summary>
        /// 由 <see cref="ScoreInfo.HitEvents"/> 计算并写入临时字段 <see cref="ScoreInfo.EzBonus"/>；无 HitEvents 时置空。
        /// </summary>
        /// <param name="playableBeatmap">与 <see cref="ScoreInfo.HitEvents"/> 同一次运行的可玩谱面。</param>
        public static void Apply(ScoreInfo score, IBeatmap playableBeatmap)
        {
            if (score.HitEvents.Count == 0 || playableBeatmap.HitObjects.Count == 0)
            {
                score.EzBonus = null;
                return;
            }

            score.EzBonus = Calculate(playableBeatmap, score.HitEvents, ModUtils.CalculateRateWithMods(score.Mods));
        }

        private static (int column, double time) keyOf(HitObject hitObject)
            => (hitObject is IHasColumn c ? c.Column : -1, hitObject.StartTime);

        /// <summary>
        /// 物件上的 Miss 窗口是游玩时钟；价值轴用真实时间，所以再除以速率。没有窗口时用常数。
        /// </summary>
        private static double resolveMissBoundary(HitObject hitObject, double rate)
        {
            double window = findMissWindow(hitObject);

            if (window <= 0)
                return EzScoreBonusFormula.OFFSET_MISS_MS;

            return window / rate;
        }

        private static double findMissWindow(HitObject hitObject)
        {
            var windows = hitObject.HitWindows;

            if (windows != null && !ReferenceEquals(windows, HitWindows.Empty) && windows.IsHitResultAllowed(HitResult.Miss))
            {
                double window = windows.WindowFor(HitResult.Miss);

                if (window > 0 && double.IsFinite(window))
                    return window;
            }

            foreach (var nested in hitObject.NestedHitObjects)
            {
                double window = findMissWindow(nested);

                if (window > 0)
                    return window;
            }

            return 0;
        }

        private static List<HitObject> judgedLeaves(HitObject hitObject)
        {
            var leaves = new List<HitObject>();
            collectJudgedLeaves(hitObject, leaves);
            return leaves;
        }

        private static void collectJudgedLeaves(HitObject hitObject, List<HitObject> leaves)
        {
            if (hitObject.NestedHitObjects.Count > 0)
            {
                foreach (var nested in hitObject.NestedHitObjects)
                    collectJudgedLeaves(nested, leaves);

                return;
            }

            if (hitObject is IHasDuration || hitObject.CreateJudgement() is IgnoreJudgement)
                return;

            leaves.Add(hitObject);
        }
    }
}
