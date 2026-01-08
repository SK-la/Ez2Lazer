// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.LAsEzExtensions.Background;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class ManiaHealthProcessor : LegacyDrainingHealthProcessor
    {
        private HitResult lastResult = HitResult.None;
        private int streak;

        public ManiaHealthProcessor(double drainStartTime)
            : base(drainStartTime)
        {
        }

        protected override double ComputeDrainRate()
        {
            // Base call is run only to compute HP recovery (namely, `HpMultiplierNormal`).
            // This closely mirrors (broken) behaviour of stable and as such is preserved unchanged.
            base.ComputeDrainRate();

            return 0;
        }

        protected override IEnumerable<HitObject> EnumerateTopLevelHitObjects() => Beatmap.HitObjects;

        protected override IEnumerable<HitObject> EnumerateNestedHitObjects(HitObject hitObject) => hitObject.NestedHitObjects;

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
            int modeIndex = 0;

            if (GlobalConfigStore.EzConfig != null)
            {
                var healthMode = GlobalConfigStore.EzConfig.Get<EnumHealthMode>(Ez2Setting.CustomHealthMode);
                modeIndex = (int)healthMode;
            }

            if (modeIndex == 0) // Lazer
            {
                switch (result)
                {
                    case HitResult.Pool:
                        double poolIncrease = 0.075 + Math.Min(streak - 1, 4) * 0.0125;
                        return -poolIncrease;

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
                        increase = 0.0051 - Beatmap.Difficulty.DrainRate * 0.0005;
                        break;

                    case HitResult.Perfect:
                        increase = 0.0053 - Beatmap.Difficulty.DrainRate * 0.0005;
                        break;
                }

                if (increase > 0)
                    increase *= streak;

                return HpMultiplierNormal * increase;
            }

            switch (result)
            {
                case HitResult.Pool:
                    increase = difficultySettings[modeIndex][6];
                    break;

                case HitResult.Miss:
                    increase = difficultySettings[modeIndex][5];
                    break;

                case HitResult.Meh:
                    increase = difficultySettings[modeIndex][4];
                    break;

                case HitResult.Ok:
                    increase = difficultySettings[modeIndex][3];
                    break;

                case HitResult.Good:
                    increase = difficultySettings[modeIndex][2];
                    break;

                case HitResult.Great:
                    increase = difficultySettings[modeIndex][1];
                    break;

                case HitResult.Perfect:
                    increase = difficultySettings[modeIndex][0];
                    break;
            }

            if (increase > 0)
                increase *= streak;

            return increase / 1000.0;
        }

        private readonly int[][] difficultySettings =
        {
            //      Perfect, Great, Good, Ok, Meh, Miss, Pool
            new[] { 4, 3, 1, 0, -10, -40, -40 }, // Lazer
            new[] { 3, 0, 2, 0, -10, -50, -60 }, // O2 Easy
            new[] { 2, 0, 1, 0, -7, -40, -50 }, // O2 Normal
            new[] { 1, 0, 0, 0, -5, -30, -40 }, // O2 Hard
            new[] { 4, 3, 1, 0, -10, -40, -60 }, // Ez2Ac
            new[] { 4, 3, 1, 0, -10, -40, -40 }, // IIDX
        };
    }
}
