// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Background;
using osu.Game.Rulesets.Scoring;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public class ManiaHitWindows : HitWindows
    {
        public static readonly DifficultyRange PERFECT_WINDOW_RANGE = new DifficultyRange(22.4D, 19.4D, 13.9D);
        private static readonly DifficultyRange great_window_range = new DifficultyRange(64, 49, 34);
        private static readonly DifficultyRange good_window_range = new DifficultyRange(97, 82, 67);
        private static readonly DifficultyRange ok_window_range = new DifficultyRange(127, 112, 97);
        private static readonly DifficultyRange meh_window_range = new DifficultyRange(151, 136, 121);
        private static readonly DifficultyRange miss_window_range = new DifficultyRange(188, 173, 158);

        private double speedMultiplier = 1;

        private bool updateCustomHitWindows;

        public bool CustomHitWindows
        {
            get => updateCustomHitWindows;
            set
            {
                updateCustomHitWindows = value;
                updateWindows();
            }
        }

        public static double PerfectRange;
        public static double GreatRange;
        public static double GoodRange;
        public static double OkRange;
        public static double MehRange;
        public static double MissRange;

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

        private double perfect;
        private double great;
        private double good;
        private double ok;
        private double meh;
        private double miss;
        private double pool;

        public override bool AllowPoolEnabled => GlobalConfigStore.EzConfig?.Get<bool>(Ez2Setting.CustomPoorHitResult) ?? false;

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

                case HitResult.Pool:
                    return AllowPoolEnabled;

                default:
                    return false;
            }
        }

        public override void SetDifficulty(double difficulty)
        {
            overallDifficulty = difficulty;
            updateWindows();
        }

        public void SetSpecialDifficultyRange(double perfect, double great, double good, double ok, double meh, double miss)
        {
            updateCustomHitWindows = true;
            PerfectRange = perfect;
            GreatRange = great;
            GoodRange = good;
            OkRange = ok;
            MehRange = meh;
            MissRange = miss;
            updateWindows();
        }

        public void SetSpecialDifficultyRange(double[] difficultyRangeArray)
        {
            updateCustomHitWindows = true;
            PerfectRange = difficultyRangeArray[0];
            GreatRange = difficultyRangeArray[1];
            GoodRange = difficultyRangeArray[2];
            OkRange = difficultyRangeArray[3];
            MehRange = difficultyRangeArray[4];
            MissRange = difficultyRangeArray[5];
            updateWindows();
        }

        public void ResetRange()
        {
            updateCustomHitWindows = false;
            updateWindows();
        }

        public void SetHitMode(IBeatmap beatmap)
        {
            if (!CustomHitWindows) return;

            EzMUGHitMode hitMode = GlobalConfigStore.EzConfig?.Get<EzMUGHitMode>(Ez2Setting.HitMode) ?? EzMUGHitMode.Lazer;

            switch (hitMode)
            {
                case EzMUGHitMode.O2Jam:
                    double bpm = beatmap.BeatmapInfo.BPM <= 0
                        ? O2HitModeExtension.DEFAULT_BPM
                        : beatmap.BeatmapInfo.BPM;

                    double coolRange = 7500.0 / bpm * totalMultiplier;
                    double goodRange = 22500.0 / bpm * totalMultiplier;
                    double badRange = 31250.0 / bpm * totalMultiplier;

                    // 注意：O2Jam的判定窗口是基于BPM变化的。Bad约等于om的Miss。
                    // 此处的作用只是为了方便显示和计算，并不会影响实际判定。
                    SetSpecialDifficultyRange(new[] { coolRange, coolRange, goodRange, goodRange, badRange, badRange });

                    // 这里是真正影响判定的BPM设定
                    O2HitModeExtension.NowBeatmapBPM = bpm;
                    O2HitModeExtension.PillCount.Value = 0;
                    O2HitModeExtension.CoolCombo = 0;
                    O2HitModeExtension.PillActivated = true;
                    break;

                case EzMUGHitMode.EZ2AC:
                    SetSpecialDifficultyRange(new[] { 18.0, 38.0, 68.0, 88.0, 88.0, 100.0 });
                    break;

                case EzMUGHitMode.IIDX:
                    SetSpecialDifficultyRange(new[] { 20.0, 40.0, 60.0, 70.0, 80.0, 100.0 });
                    break;

                case EzMUGHitMode.Melody:
                    SetSpecialDifficultyRange(new[] { 20.0, 40.0, 60.0, 80.0, 100.0, 120.0 });
                    break;

                default:
                    ResetRange();
                    break;
            }
        }

        private void updateWindows()
        {
            if (updateCustomHitWindows)
            {
                //perfect = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, PerfectRange) * totalMultiplier);
                //great = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, GreatRange) * totalMultiplier);
                //good = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, GoodRange) * totalMultiplier);
                //ok = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, OkRange) * totalMultiplier);
                //meh = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, MehRange) * totalMultiplier);
                //miss = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, MissRange) * totalMultiplier);
                perfect = PerfectRange * totalMultiplier;
                great = GreatRange * totalMultiplier;
                good = GoodRange * totalMultiplier;
                ok = OkRange * totalMultiplier;
                meh = MehRange * totalMultiplier;
                miss = MissRange * totalMultiplier;
                return;
            }

            if (ClassicModActive && !ScoreV2Active)
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

            // 这里的Pool区间只是用于显示，并不会影响实际判定；实际判定请见 HitWindows.ResultFor 方法
            pool = miss + 150;
        }

        public override double WindowFor(HitResult result)
        {
            switch (result)
            {
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

                case HitResult.Pool:
                    return pool;

                case HitResult.Miss:
                    return miss;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}
