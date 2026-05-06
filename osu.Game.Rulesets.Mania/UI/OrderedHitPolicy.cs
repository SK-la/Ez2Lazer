// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// Ensures that only the most recent <see cref="HitObject"/> is hittable, affectionately known as "note lock".
    /// </summary>
    public class OrderedHitPolicy
    {
        private readonly HitObjectContainer hitObjectContainer;
        private readonly OrderedHitPolicyHelper helper;
        private const string log_prefix = "[JudgeDiag][OrderedHitPolicy]";

        public OrderedHitPolicy(HitObjectContainer hitObjectContainer)
        {
            this.hitObjectContainer = hitObjectContainer;
            helper = new OrderedHitPolicyHelper(hitObjectContainer);
        }

        /// <summary>
        /// Determines whether a <see cref="DrawableHitObject"/> can be hit at a point in time.
        /// </summary>
        /// <remarks>
        /// Only the most recent <see cref="DrawableHitObject"/> can be hit, a previous hitobject's window cannot extend past the next one.
        /// </remarks>
        /// <param name="hitObject">The <see cref="DrawableHitObject"/> to check.</param>
        /// <param name="time">The time to check.</param>
        /// <returns>Whether <paramref name="hitObject"/> can be hit at the given <paramref name="time"/>.</returns>
        public bool IsHittable(DrawableHitObject hitObject, double time)
        {
            // 非Earliest模式下，允许独立的优先级算法。
            if (GlobalConfigStore.EzConfig.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence) != EzEnumJudgePrecedence.Earliest)
            {
                bool result = helper.IsHittableWithPrecedence(hitObject, time);
                logDiag($"isHittable-precedence t={time:F3} target={describe(hitObject)} result={result}");
                return result;
            }

            var nextObject = hitObjectContainer.AliveObjects.GetNext(hitObject);
            bool isHittable = nextObject == null || time < nextObject.HitObject.StartTime;
            logDiag($"isHittable-earliest t={time:F3} target={describe(hitObject)} next={describe(nextObject)} result={isHittable}");
            return isHittable;
        }

        /// <summary>
        /// Handles a <see cref="HitObject"/> being hit to potentially miss all earlier <see cref="HitObject"/>s.
        /// </summary>
        /// <param name="hitObject">The <see cref="HitObject"/> that was hit.</param>
        public void HandleHit(DrawableHitObject hitObject)
        {
            double judgementTime = hitObject.Result.TimeAbsolute;
            var forcedMisses = new List<string>();
            var preserved = new List<string>();

            foreach (var obj in enumerateHitObjectsUpTo(hitObject.HitObject.StartTime))
            {
                if (obj.Judged)
                    continue;

                if (OrderedHitPolicyHelper.IsUserTriggerJudgeableNow(obj, judgementTime))
                {
                    preserved.Add(describe(obj));
                    continue;
                }

                ((DrawableManiaHitObject)obj).MissForcefully();
                forcedMisses.Add(describe(obj));
            }

            logDiag(
                $"handleHit source={describe(hitObject)} t={judgementTime:F3} " +
                $"forcedMisses=[{string.Join(", ", forcedMisses)}] preserved=[{string.Join(", ", preserved)}]");
        }

        private IEnumerable<DrawableHitObject> enumerateHitObjectsUpTo(double targetTime)
        {
            foreach (var obj in hitObjectContainer.AliveObjects)
            {
                if (obj.HitObject.GetEndTime() >= targetTime)
                    yield break;

                yield return obj;

                foreach (var nestedObj in obj.NestedHitObjects)
                {
                    if (nestedObj.HitObject.GetEndTime() >= targetTime)
                        break;

                    yield return nestedObj;
                }
            }
        }

        private static string describe(DrawableHitObject? obj)
        {
            if (obj == null)
                return "null";

            return $"{obj.GetType().Name}@{obj.HitObject.StartTime:F3} judged={obj.Judged}";
        }

        private static void logDiag(string message)
        {
            if (!GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.EzJudgmentDiagEnabled))
                return;

            Logger.Log($"{log_prefix} {message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }
    }
}
