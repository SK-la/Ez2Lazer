// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.BMS.Objects.Drawables;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// BMS 内部使用的 note lock / 判定优先级策略。
    /// 逻辑与 mania OrderedHitPolicy 一致，但不修改其他规则集代码。
    /// </summary>
    public class BMSOrderedHitPolicy
    {
        private readonly HitObjectContainer hitObjectContainer;
        private readonly OrderedHitPolicyHelper helper;

        public BMSOrderedHitPolicy(HitObjectContainer hitObjectContainer)
        {
            this.hitObjectContainer = hitObjectContainer;
            helper = new OrderedHitPolicyHelper(hitObjectContainer);
        }

        public bool IsHittable(DrawableHitObject hitObject, double time)
        {
            if (GlobalConfigStore.EzConfig.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence) != EzEnumJudgePrecedence.Earliest)
                return helper.IsHittableWithPrecedence(hitObject, time);

            var nextObject = hitObjectContainer.AliveObjects.GetNext(hitObject);
            return nextObject == null || time < nextObject.HitObject.StartTime;
        }

        public void HandleHit(DrawableHitObject hitObject)
        {
            foreach (var obj in enumerateHitObjectsUpTo(hitObject.HitObject.StartTime))
            {
                if (obj.Judged)
                    continue;

                (obj as DrawableBMSHitObject)?.MissForcefully();
            }
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
    }
}
