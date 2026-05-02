// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class ManiaHealthProcessor : LegacyDrainingHealthProcessor
    {
        private static EzEnumHealthMode mode = EzEnumHealthMode.Lazer;
        private static int row;

        public ManiaHealthProcessor(double drainStartTime)
            : base(drainStartTime)
        {
            try
            {
                mode = GlobalConfigStore.EzConfig.Get<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            }
            catch { }

            row = switchHealthMode(mode);
        }

        protected override double ComputeDrainRate()
        {
            // Base call is run only to compute HP recovery (namely, `HpMultiplierNormal`).
            // This closely mirrors (broken) behaviour of stable and as such is preserved unchanged.
            // 只有Lazer模式下，会调用此方法。从基类中计算HP作用。非Lazer模式禁止使用，否则会出现无限计算。
            if (mode == EzEnumHealthMode.Lazer)
                base.ComputeDrainRate();

            return 0;
        }

        protected override IEnumerable<HitObject> EnumerateTopLevelHitObjects() => Beatmap.HitObjects;

        protected override IEnumerable<HitObject> EnumerateNestedHitObjects(HitObject hitObject) => hitObject.NestedHitObjects;

        // 特别强调：血量机制异常时会导致无法进入Gameplay，无限加载。
        protected override double GetHealthIncreaseFor(HitObject hitObject, HitResult result)
        {
            double increase = 0;

            if (mode == EzEnumHealthMode.Lazer)
            {
                switch (result)
                {
                    case HitResult.Miss:
                        switch (hitObject)
                        {
                            case HeadNote:
                            case TailNote:
                                return -(Beatmap.Difficulty.DrainRate + 1) * 0.00375;

                            default:
                                return -(Beatmap.Difficulty.DrainRate + 1) * 0.0075;
                        }

                    case HitResult.Meh:
                        return -(Beatmap.Difficulty.DrainRate + 1) * 0.0016;

                    case HitResult.Ok:
                        return 0;

                    case HitResult.Good:
                        increase = 0.004 - Beatmap.Difficulty.DrainRate * 0.0004;
                        break;

                    case HitResult.Great:
                        increase = 0.005 - Beatmap.Difficulty.DrainRate * 0.0005;
                        break;

                    case HitResult.Perfect:
                        increase = 0.0055 - Beatmap.Difficulty.DrainRate * 0.0005;
                        break;
                }

                return HpMultiplierNormal * increase;
            }

            if (mode == EzEnumHealthMode.O2JamHard || mode == EzEnumHealthMode.O2JamEasy || mode == EzEnumHealthMode.O2JamNormal)
            {
                switch (hitObject)
                {
                    case HoldNoteBody:
                        return 0;
                }
            }

            switch (result)
            {
                case HitResult.Poor:
                    increase = HealthModeHelper.HEALTH_MODE_MAP[row, 6];
                    break;

                case HitResult.Miss:
                    increase = HealthModeHelper.HEALTH_MODE_MAP[row, 5];
                    break;

                case HitResult.Meh:
                    increase = HealthModeHelper.HEALTH_MODE_MAP[row, 4];
                    break;

                case HitResult.Ok:
                    increase = HealthModeHelper.HEALTH_MODE_MAP[row, 3];
                    break;

                case HitResult.Good:
                    increase = HealthModeHelper.HEALTH_MODE_MAP[row, 2];
                    break;

                case HitResult.Great:
                    increase = HealthModeHelper.HEALTH_MODE_MAP[row, 1];
                    break;

                case HitResult.Perfect:
                    increase = HealthModeHelper.HEALTH_MODE_MAP[row, 0];
                    break;
            }

            // 低血量时的特殊扣血处理
            if (increase < 0 && Health.Value <= 0.5)
            {
                if (mode == EzEnumHealthMode.IIDX_HD)
                {
                    // IIDX模式：血量≤30%时扣血减半
                    if (Health.Value <= 0.3)
                    {
                        increase *= 0.5;
                    }
                }
                else if (mode == EzEnumHealthMode.LR2_HD)
                {
                    // LR2 Hard模式：血量≤30%时扣血×0.6（60%折扣）
                    if (Health.Value <= 0.3)
                    {
                        increase *= 0.6;
                    }
                }
                else if (mode == EzEnumHealthMode.Raja_HD)
                {
                    // raja Hard模式：50%-30%之间线性插值递减
                    if (Health.Value <= 0.3)
                    {
                        // ≤30%：使用最低扣血（60%折扣）
                        increase *= 0.6;
                    }
                    else if (Health.Value < 0.5)
                    {
                        // 30%-50%之间：线性插值
                        // t = (health - 0.3) / (0.5 - 0.3)，范围从0到1
                        double t = (Health.Value - 0.3) / (0.5 - 0.3);

                        // 折扣系数从0.6（30%时）线性增加到1.0（50%时）
                        double discount = 0.6 + t * 0.4;
                        increase *= discount;
                    }
                }
            }

            // 约束血量变化幅度
            double scaled = Math.Clamp(increase, -0.2, 0.2);

            const double epsilon = 1e-6;

            if (Math.Abs(scaled) < epsilon)
            {
                scaled = 0;
            }
            else
            {
                // #if DEBUG
                // Logger.Log($"ManiaHealthProcessor: raw health change {scaled} for mode {mode}");
                // #endif
            }

            return scaled;
        }

        private int switchHealthMode(EzEnumHealthMode mode)
        {
            int idx = (int)mode;

            if (idx < 0 || idx >= HealthModeHelper.HEALTH_MODE_MAP.GetLength(0))
                idx = 0;

            return idx;
        }
    }
}
