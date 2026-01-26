// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Background;
using osu.Game.Rulesets.Scoring;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.LAsEZMania.Helper;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public readonly record struct ManiaModifyHitRange(double Perfect, double Great, double Good, double Ok, double Meh, double Miss, double Poor = 0);

    public class ManiaHitWindows : HitWindows
    {
        public static readonly DifficultyRange PERFECT_WINDOW_RANGE = new DifficultyRange(22.4D, 19.4D, 13.9D);
        private static readonly DifficultyRange great_window_range = new DifficultyRange(64, 49, 34);
        private static readonly DifficultyRange good_window_range = new DifficultyRange(97, 82, 67);
        private static readonly DifficultyRange ok_window_range = new DifficultyRange(127, 112, 97);
        private static readonly DifficultyRange meh_window_range = new DifficultyRange(151, 136, 121);
        private static readonly DifficultyRange miss_window_range = new DifficultyRange(188, 173, 158);

        private double speedMultiplier = 1;

        public static double PerfectRange;
        public static double GreatRange;
        public static double GoodRange;
        public static double OkRange;
        public static double MehRange;
        public static double MissRange;
        public static double PoorRange;

        /// <summary>
        /// Multiplier used to compensate for the playback speed of the track speeding up or slowing down.
        /// The goal of this multiplier is to keep hit windows independent of track speed.
        /// <list type="bullet">
        /// <item>When the track speed is above 1, the hit window ranges are multiplied by <see cref="SpeedMultiplier"/>, because the time elapses faster.</item>
        /// <item>When the track speed is below 1, the hit window ranges are also multiplied by <see cref="SpeedMultiplier"/>, because the time elapses slower.</item>
        /// </list>
        /// </summary>
        public double SpeedMultiplier
        {
            get => speedMultiplier;
            set
            {
                speedMultiplier = value;
                updateWindows();
            }
        }

        private double difficultyMultiplier = 1;

        /// <summary>
        /// Multiplier used to make the gameplay more or less difficult.
        /// <list type="bullet">
        /// <item>When the <see cref="DifficultyMultiplier"/> is above 1, the hit windows decrease to make the gameplay harder.</item>
        /// <item>When the <see cref="DifficultyMultiplier"/> is below 1, the hit windows increase to make the gameplay easier.</item>
        /// </list>
        /// </summary>
        public double DifficultyMultiplier
        {
            get => difficultyMultiplier;
            set
            {
                difficultyMultiplier = value;
                updateWindows();
            }
        }

        private double totalMultiplier => speedMultiplier / difficultyMultiplier;

        private double overallDifficulty;

        private bool classicModActive;

        public bool ClassicModActive
        {
            get => classicModActive;
            set
            {
                classicModActive = value;
                updateWindows();
            }
        }

        private bool scoreV2Active;

        public bool ScoreV2Active
        {
            get => scoreV2Active;
            set
            {
                scoreV2Active = value;
                updateWindows();
            }
        }

        private bool isConvert;

        public bool IsConvert
        {
            get => isConvert;
            set
            {
                isConvert = value;
                updateWindows();
            }
        }

        private static bool modifyHitWindows;

        public bool ModifyHitWindows
        {
            get => modifyHitWindows;
            set => modifyHitWindows = value;
        }

        private double perfect;
        private double great;
        private double good;
        private double ok;
        private double meh;
        private double miss;
        private double pool;

        private static double bpm = 200;

        public double BPM
        {
            get => bpm;
            set
            {
                bpm = value;
                setHitMode();
                updateWindows();
            }
        }

        public bool AllowPoorEnabled => GlobalConfigStore.EzConfig?.Get<bool>(Ez2Setting.CustomPoorHitResultBool) ?? false;

        public override bool IsHitResultAllowed(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                case HitResult.Good:
                case HitResult.Ok:
                case HitResult.Meh:
                case HitResult.Miss:
                    return true;

                case HitResult.Poor:
                    return AllowPoorEnabled;

                default:
                    return false;
            }
        }

        public override void SetDifficulty(double difficulty)
        {
            overallDifficulty = difficulty;
            updateWindows();
        }

        private void modifyManiaHitRange(double[] difficultyRangeArray)
        {
            ModifyHitWindows = true;

            PerfectRange = difficultyRangeArray[0];
            GreatRange = difficultyRangeArray[1];
            GoodRange = difficultyRangeArray[2];
            OkRange = difficultyRangeArray[3];
            MehRange = difficultyRangeArray[4];
            MissRange = difficultyRangeArray[5];
            PoorRange = difficultyRangeArray[6] == 0 ? MissRange : difficultyRangeArray[6];
            updateWindows();
        }

        public void ModifyManiaHitRange(ManiaModifyHitRange range)
        {
            ModifyHitWindows = true;

            PerfectRange = range.Perfect;
            GreatRange = range.Great;
            GoodRange = range.Good;
            OkRange = range.Ok;
            MehRange = range.Meh;
            MissRange = range.Miss;
            PoorRange = range.Poor == 0 ? MissRange : range.Poor;
            updateWindows();
        }

        public void ResetRange()
        {
            ModifyHitWindows = false;
            setHitMode();
            updateWindows();
        }

        private static readonly CustomHitWindowsHelper custom_helper = new CustomHitWindowsHelper();

        private void setHitMode()
        {
            EzMUGHitMode HitMode = GlobalConfigStore.EzConfig?.Get<EzMUGHitMode>(Ez2Setting.HitMode) ?? EzMUGHitMode.Lazer;

            if (HitMode == EzMUGHitMode.Lazer)
            {
                return;
            }

            switch (HitMode)
            {
                case EzMUGHitMode.O2Jam:
                    modifyManiaHitRange(custom_helper.GetHitWindowsO2Jam(BPM));
                    break;

                case EzMUGHitMode.EZ2AC:
                    modifyManiaHitRange(custom_helper.GetHitWindowsEZ2AC());
                    break;

                case EzMUGHitMode.IIDX_HD:
                case EzMUGHitMode.LR2_HD:
                case EzMUGHitMode.Raja_NM:
                    modifyManiaHitRange(custom_helper.GetHitWindowsBMS(HitMode));
                    break;

                case EzMUGHitMode.Malody:
                    modifyManiaHitRange(custom_helper.GetHitWindowsMelody());
                    break;
            }
        }

        private void updateWindows()
        {
            if (ModifyHitWindows)
            {
                perfect = PerfectRange;
                great = GreatRange;
                good = GoodRange;
                ok = OkRange;
                meh = MehRange;
                miss = MissRange;
                pool = PoorRange;
            }
            else if (ClassicModActive && !ScoreV2Active)
            {
                if (IsConvert)
                {
                    perfect = Math.Floor(16 * totalMultiplier) + 0.5;
                    great = Math.Floor((Math.Round(overallDifficulty) > 4 ? 34 : 47) * totalMultiplier) + 0.5;
                    good = Math.Floor((Math.Round(overallDifficulty) > 4 ? 67 : 77) * totalMultiplier) + 0.5;
                    ok = Math.Floor(97 * totalMultiplier) + 0.5;
                    meh = Math.Floor(121 * totalMultiplier) + 0.5;
                    miss = Math.Floor(158 * totalMultiplier) + 0.5;
                }
                else
                {
                    double invertedOd = Math.Clamp(10 - overallDifficulty, 0, 10);

                    perfect = Math.Floor(16 * totalMultiplier) + 0.5;
                    great = Math.Floor((34 + 3 * invertedOd) * totalMultiplier) + 0.5;
                    good = Math.Floor((67 + 3 * invertedOd) * totalMultiplier) + 0.5;
                    ok = Math.Floor((97 + 3 * invertedOd) * totalMultiplier) + 0.5;
                    meh = Math.Floor((121 + 3 * invertedOd) * totalMultiplier) + 0.5;
                    miss = Math.Floor((158 + 3 * invertedOd) * totalMultiplier) + 0.5;
                }
            }
            else
            {
                perfect = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, PERFECT_WINDOW_RANGE) * totalMultiplier) + 0.5;
                great = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, great_window_range) * totalMultiplier) + 0.5;
                good = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, good_window_range) * totalMultiplier) + 0.5;
                ok = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, ok_window_range) * totalMultiplier) + 0.5;
                meh = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, meh_window_range) * totalMultiplier) + 0.5;
                miss = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, miss_window_range) * totalMultiplier) + 0.5;
            }
        }

        public override double WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Poor:
                    return pool;

                case HitResult.Perfect:
                    return perfect;

                case HitResult.Great:
                    return great;

                case HitResult.Good:
                    return good;

                case HitResult.Ok:
                    return ok;

                case HitResult.Meh:
                    return meh;

                case HitResult.Miss:
                    return miss;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}
