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
    public readonly record struct EzScoreBonusResult(int JudgeBonus, int ErrorMalus, int CountedNotes)
    {
        public int Total => JudgeBonus + ErrorMalus;
    }

    /// <summary>
    /// 附加分：每颗 Note 的 offset 价值再乘 KPS 权重。
    /// 44ms 及以内算判定加成，超过 44ms 和 Miss 算 Error Malus。
    /// 两边都除以总 Note 数，失误再乘 <see cref="EzScoreBonusFormula.ERROR_INFLUENCE"/>。
    /// </summary>
    public static class EzScoreBonusCalculator
    {
        public static EzScoreBonusSet Calculate(IBeatmap playableBeatmap, IReadOnlyList<HitEvent> hitEvents, double rate, IEzKpsSectionLookup? kps = null,
                                                IReadOnlyList<double>? cachedKps = null)
        {
            if (rate <= 0 || double.IsNaN(rate))
                rate = 1;

            kps ??= resolveKps(playableBeatmap, rate, cachedKps);

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
                bool error = isMiss || errorMs > EzScoreBonusFormula.OFFSET_CROSS_MS;

                cotangent.Add(weight, EzScoreBonusFormula.OffsetQuality(errorMs, cotangent: true, missBoundary), error);
                inverseCotangent.Add(weight, EzScoreBonusFormula.OffsetQuality(errorMs, missBoundaryMs: missBoundary), error);
            }

            if (counted == 0)
                return new EzScoreBonusSet(default, default);

            return new EzScoreBonusSet(cotangent.ToResult(counted), inverseCotangent.ToResult(counted));
        }

        private struct Accumulator
        {
            private double judgeSum;
            private double errorSum;

            public void Add(double weight, double quality, bool error)
            {
                if (error)
                    errorSum += weight * quality;
                else
                    judgeSum += weight * quality;
            }

            public readonly EzScoreBonusResult ToResult(int counted)
            {
                int max = EzScoreBonusFormula.JUDGE_BONUS_MAX;
                int judge = counted == 0 ? 0 : (int)Math.Round(max * judgeSum / counted);
                int error = counted == 0 ? 0 : (int)Math.Round(max * errorSum / counted * EzScoreBonusFormula.ERROR_INFLUENCE);

                return new EzScoreBonusResult(
                    Math.Clamp(judge, 0, max),
                    Math.Clamp(error, -max, 0),
                    counted);
            }
        }

        /// <summary>
        /// 由 <see cref="ScoreInfo.HitEvents"/> 计算并写入临时字段 <see cref="ScoreInfo.EzBonus"/>；无 HitEvents 时置空。
        /// </summary>
        /// <param name="playableBeatmap">与 <see cref="ScoreInfo.HitEvents"/> 同一次运行的可玩谱面。</param>
        public static void Apply(ScoreInfo score, IBeatmap playableBeatmap, IReadOnlyList<double>? cachedKps = null)
        {
            if (score.HitEvents.Count == 0 || playableBeatmap.HitObjects.Count == 0)
            {
                score.EzBonus = null;
                return;
            }

            score.EzBonus = Calculate(playableBeatmap, score.HitEvents, ModUtils.CalculateRateWithMods(score.Mods), cachedKps: cachedKps);
        }

        private static IEzKpsSectionLookup resolveKps(IBeatmap playableBeatmap, double rate, IReadOnlyList<double>? cachedKps)
        {
            // 与 KPS 图同一份已换算列表。有这份列表就按歌曲进度取样，不再现场重算、也不再乘一次速率。
            if (cachedKps != null && cachedKps.Count > 0)
            {
                double songEnd = playableBeatmap.HitObjects.Count == 0 ? 0 : playableBeatmap.HitObjects[^1].StartTime;
                return EzKpsListLookup.FromAnalysis(cachedKps, songEnd);
            }

            return EzKpsListLookup.FromBeatmap(playableBeatmap, rate);
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
