// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
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
        private readonly EzEnumJudgePrecedence judgePrecedence;
        private readonly ManiaLaneController laneController;
        private DrawableHitObject? columnRoutedPressTarget;

        public OrderedHitPolicy(HitObjectContainer hitObjectContainer, EzEnumJudgePrecedence judgePrecedence, ManiaLaneController laneController)
        {
            this.hitObjectContainer = hitObjectContainer;
            this.judgePrecedence = judgePrecedence;
            this.laneController = laneController;
            helper = new OrderedHitPolicyHelper(hitObjectContainer, laneController);
        }

        internal void RegisterDrawable(DrawableHitObject drawable, bool scheduleAutoMiss) => laneController.Register(drawable, scheduleAutoMiss);

        internal void EnsureRegistered(DrawableHitObject drawable, bool scheduleAutoMiss) => laneController.RegisterIfNeeded(drawable, scheduleAutoMiss);

        internal void UnregisterDrawable(DrawableHitObject drawable) => laneController.Unregister(drawable);

        internal void UnregisterByHitObject(HitObject hitObject) => laneController.UnregisterByHitObject(hitObject);

        internal void NotifyJudged(DrawableHitObject drawable) => laneController.NotifyJudged(drawable);

        internal bool ShouldSkipDrawablePress(DrawableHitObject drawable)
            => columnRoutedPressTarget != null;

        /// <summary>
        /// 列级按键路由：选出本列唯一 press 目标（Combo / Duration / Earliest + BMS post-Bad）。
        /// </summary>
        public bool TryRoutePress(double time, EzEnumJudgePrecedence precedence, out DrawableHitObject? target)
        {
            columnRoutedPressTarget = null;
            target = null;

            var entry = laneController.SelectPressEntry(time, precedence);

            if (entry == null)
                return false;

            target = entry.RoutedObject;
            return true;
        }

        /// <summary>
        /// 对 <see cref="TryRoutePress"/> 选中的目标执行判定（保留 Ez 判定链）。
        /// </summary>
        public bool ApplyRoutedPress(DrawableHitObject target, double time, KeyBindingPressEvent<ManiaAction> e)
        {
            switch (target)
            {
                case DrawableNote note when ManiaEzDrawableJudgement.TryBmsOnPressed(note, e):
                    columnRoutedPressTarget = target;
                    return true;

                case DrawableNote note:
                    if (!note.ApplyColumnRoutedPress())
                        return false;

                    columnRoutedPressTarget = target;
                    return true;

                case DrawableHoldNote hold:
                    if (!hold.TryBeginHoldPressFromColumn(time))
                        return false;

                    laneController.SetActiveHold(hold);
                    columnRoutedPressTarget = target;
                    return true;

                default:
                    return false;
            }
        }

        public bool IsHittable(DrawableHitObject hitObject, double time, EzEnumJudgePrecedence precedence)
        {
            ManiaJudgeHotPathTrace.RecordIsHittable();

            if (hitObject is DrawableHoldNoteTail)
                return helper.IsHittableWithPrecedence(hitObject, time, precedence);

            if (precedence != EzEnumJudgePrecedence.Earliest)
                return helper.IsHittableWithPrecedence(hitObject, time, precedence);

            return laneController.IsHittableEarliest(hitObject, time);
        }

        public void HandleHit(DrawableHitObject hitObject)
        {
            double judgementTime = hitObject.Result.TimeAbsolute;

            foreach (var entry in laneController.EnumerateForceMissBefore(hitObject.HitObject.StartTime))
            {
                if (OrderedHitPolicyHelper.IsUserTriggerJudgeableNow(entry.RoutedObject, judgementTime))
                    continue;

                ((DrawableManiaHitObject)entry.RoutedObject).MissForcefully();
            }

            laneController.NotifyJudged(hitObject);
        }
    }
}
