// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Diagnostics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Screens.Play
{
    /// <summary>
    /// Resolves stuck gameplay by force-applying miss judgements and advancing the gameplay clock when required.
    /// </summary>
    public static class EzGameplayForceCompleter
    {
        private const int max_drawable_passes = 10;

        public static void CompleteRemainingJudgements(
            DrawableRuleset ruleset,
            ScoreProcessor scoreProcessor,
            HealthProcessor healthProcessor,
            GameplayClockContainer clock,
            IBeatmap beatmap)
        {
            EzUnjudgedDiagnostics.Capture("before", ruleset, scoreProcessor, beatmap);

            for (int pass = 0; pass < max_drawable_passes; pass++)
            {
                int judgedBefore = scoreProcessor.JudgedHits;
                forceMissAllDrawables(ruleset);
                if (scoreProcessor.JudgedHits == judgedBefore)
                    break;
            }

            if (scoreProcessor.JudgedHits < scoreProcessor.MaximumJudgements)
                scoreProcessor.ApplyRemainingForcedMisses(healthProcessor);

            EzUnjudgedDiagnostics.Capture("after", ruleset, scoreProcessor, beatmap);

            if (scoreProcessor.HasCompleted.Value && !scoreProcessor.HasCompleted.Value)
            {
                // 计数已补齐，只差时钟推过最后一个判定时刻。
                double seekTarget = getLatestJudgementTime(beatmap);

                // 目标必定在当前位置之后；加一道护栏以免空谱面（MaxHits=0）时把时钟 seek 回退、
                // 反而撤掉刚补上的判定。
                if (clock.CurrentTime < seekTarget)
                    clock.Seek(seekTarget);
            }
        }

        /// <summary>
        /// 最后一个物件「最终判定可能落在」的最晚时刻，末后留 <paramref name="margin"/> ms。
        /// </summary>
        /// <remarks>
        /// 不能直接用 <c>GetLastObjectTime()</c> 再加一个经验常量：
        /// <c>JudgementProcessor.HasCompleted</c> 比较的是已应用结果的 <c>TimeAbsolute</c>，
        /// 而 <c>TimeAbsolute</c> 被钳在 <c>GetEndTime() + MaximumJudgementOffset</c>；
        /// Mania 的 LN 尾判还会在 <c>MaximumJudgementOffset</c> 上再乘 release lenience（1.5×），
        /// OD8 下约 260ms，远大于原先写死的 100ms。取太晚的结果会让时钟永远追不上，
        /// 「最后一条 LN 尾判得偏晚」的局就卡在差这一截上。
        /// </remarks>
        private static double getLatestJudgementTime(IBeatmap beatmap, double margin = 100)
        {
            double latest = 0;

            foreach (var hitObject in beatmap.HitObjects)
                latest = Math.Max(latest, hitObject.GetEndTime() + hitObject.MaximumJudgementOffset);

            return latest + margin;
        }

        private static void forceMissAllDrawables(DrawableRuleset ruleset)
        {
            foreach (var hitObject in ruleset.Playfield.AllHitObjects.ToArray())
                forceMissDrawable(hitObject);
        }

        private static void forceMissDrawable(DrawableHitObject hitObject)
        {
            if (hitObject.AllJudged)
                return;

            foreach (var nested in hitObject.NestedHitObjects.ToArray())
                forceMissDrawable(nested);

            if (!hitObject.AllJudged)
            {
                try
                {
                    if (!tryInvokeRulesetMissForcefully(hitObject))
                        return;
                }
                catch (InvalidOperationException)
                {
                    // Some objects may already have a result by the time nested misses propagate.
                }
                catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
                {
                    // Reflection wrapper around the same already-judged case.
                }
            }
        }

        /// <summary>
        /// Invokes a ruleset-defined <c>MissForcefully()</c> when present, without adding a base virtual on <see cref="DrawableHitObject"/>.
        /// Taiko/Catch and other rulesets without this method rely on <see cref="ScoreProcessor.ApplyRemainingForcedMisses"/>.
        /// </summary>
        private static bool tryInvokeRulesetMissForcefully(DrawableHitObject hitObject)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            for (var type = hitObject.GetType(); type != null && type != typeof(DrawableHitObject); type = type.BaseType)
            {
                var method = type.GetMethod("MissForcefully", flags, null, Type.EmptyTypes, null);
                if (method == null)
                    continue;

                method.Invoke(hitObject, null);
                return true;
            }

            return false;
        }
    }
}
