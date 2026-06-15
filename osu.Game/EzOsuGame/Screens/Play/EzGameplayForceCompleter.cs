// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using osu.Game.Beatmaps;
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
            for (int pass = 0; pass < max_drawable_passes; pass++)
            {
                int judgedBefore = scoreProcessor.JudgedHits;
                forceMissAllDrawables(ruleset);
                if (scoreProcessor.JudgedHits == judgedBefore)
                    break;
            }

            if (scoreProcessor.JudgedHits < scoreProcessor.MaximumJudgements)
                scoreProcessor.ApplyRemainingForcedMisses(healthProcessor);

            if (!scoreProcessor.HasCompleted.Value && scoreProcessor.JudgedHits >= scoreProcessor.MaximumJudgements)
            {
                double seekTarget = Math.Max(beatmap.GetLastObjectTime(), 0) + 100;
                clock.Seek(seekTarget);
            }
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
