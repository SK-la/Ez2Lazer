// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.LAsEzExtensions.Background;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class ManiaHealthProcessor : LegacyDrainingHealthProcessor
    {
        private static readonly double[,] health_settings =
        {
            //  305   300    200     100     50     Miss    Poor
            { 0.004, 0.003, 0.001, 0.000, -0.010, -0.065, -0.000 }, // Lazer
            { 0.003, 0.000, 0.002, 0.000, -0.010, -0.050, -0.060 }, // O2 Easy
            { 0.002, 0.000, 0.001, 0.000, -0.070, -0.040, -0.050 }, // O2 Normal
            { 0.001, 0.000, 0.000, 0.000, -0.050, -0.030, -0.040 }, // O2 Hard
            { 0.004, 0.003, 0.001, 0.000, -0.010, -0.050, -0.050 }, // Ez2Ac
            { 0.016, 0.016, 0.000, 0.000, -0.050, -0.090, -0.050 }, // IIDX Hard
            { 0.010, 0.010, 0.005, 0.000, -0.060, -0.100, -0.020 }, // LR2 Hard
            { 0.012, 0.012, 0.006, 0.000, -0.030, -0.060, -0.020 }, // raja normal
        };

        private static EnumHealthMode mode = EnumHealthMode.Lazer;
        private static int row;

        private HitResult lastResult = HitResult.None;
        private int streak;

        public ManiaHealthProcessor(double drainStartTime)
            : base(drainStartTime)
        {
            if (GlobalConfigStore.EzConfig != null)
                mode = GlobalConfigStore.EzConfig.Get<EnumHealthMode>(Ez2Setting.CustomHealthMode);

            row = switchHealthMode(mode);
        }

        protected override double ComputeDrainRate()
        {
            // Base call is run only to compute HP recovery (namely, `HpMultiplierNormal`).
            // This closely mirrors (broken) behaviour of stable and as such is preserved unchanged.
            // 只有Lazer模式下，会调用此方法。从基类中计算HP作用。非Lazer模式禁止使用，否则会出现无限计算。
            if (mode == EnumHealthMode.Lazer)
                base.ComputeDrainRate();

            return 0;
        }

        protected override IEnumerable<HitObject> EnumerateTopLevelHitObjects() => Beatmap.HitObjects;

        protected override IEnumerable<HitObject> EnumerateNestedHitObjects(HitObject hitObject) => hitObject.NestedHitObjects;

        // 特别强调：血量机制异常时会导致无法进入Gameplay，无限加载。
        protected override double GetHealthIncreaseFor(HitObject hitObject, HitResult result)
        {
            if (result == lastResult)
                streak++;
            else
            {
                streak = 1;
                lastResult = result;
            }

            double increase = 0;

            if (mode == EnumHealthMode.Lazer)
            {
                switch (result)
                {
                    case HitResult.Pool:
                        return -getPoorHealthDelta(streak);

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

                if (increase > 0)
                    increase *= streak;
                // Logger.Log($"ManiaHealthProcessor: raw health change {HpMultiplierNormal * increase} for mode {mode}");
                return HpMultiplierNormal * increase;
            }

            switch (result)
            {
                case HitResult.Pool:
                    return -getPoorHealthDelta(streak);

                case HitResult.Miss:
                    switch (hitObject)
                    {
                        case HeadNote:
                        case TailNote:
                            return health_settings[row, 5] / 5;

                        default:
                            return health_settings[row, 5];
                    }

                case HitResult.Meh:
                    return health_settings[row, 4];

                case HitResult.Ok:
                    return health_settings[row, 3];

                case HitResult.Good:
                    increase = health_settings[row, 2];
                    break;

                case HitResult.Great:
                    increase = health_settings[row, 1];
                    break;

                case HitResult.Perfect:
                    increase = health_settings[row, 0];
                    break;
            }

            // Non-default health modes use integer table values where the final value
            double scaled = Math.Clamp(increase, -0.00001, 0.01);

            // Suppress extremely small floating-point changes which are noise
            // and can cause issues (e.g. infinite loading) when treated as non-zero.
            const double EPSILON = 1e-6;

            if (Math.Abs(scaled) < EPSILON)
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

        private int switchHealthMode(EnumHealthMode mode)
        {
            int idx = (int)mode;

            if (idx < 0 || idx >= health_settings.GetLength(0))
                idx = 0;

            return idx;
        }

        private static double getPoorHealthDelta(int streak)
        {
            return 0.075 + Math.Min(streak - 1, 4) * 0.0125;
        }
    }
}
