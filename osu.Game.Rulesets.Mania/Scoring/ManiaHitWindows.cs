// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public readonly record struct ManiaModifyHitRange(double Perfect, double Great, double Good, double Ok, double Meh, double Miss);

    /// <summary>
    /// 提供给Mod，实现自定义判定区间后刷新显示
    /// but does not go through <c>IApplicableToDifficulty</c>.
    /// </summary>
    public interface IManiaHitRangeProvider
    {
        ManiaModifyHitRange? GetDisplayHitRange(IBeatmapInfo beatmapInfo);
    }

    public class ManiaHitWindows : HitWindows
    {
        public static readonly DifficultyRange PERFECT_WINDOW_RANGE = new DifficultyRange(22.4D, 19.4D, 13.9D);
        private static readonly DifficultyRange great_window_range = new DifficultyRange(64, 49, 34);
        private static readonly DifficultyRange good_window_range = new DifficultyRange(97, 82, 67);
        private static readonly DifficultyRange ok_window_range = new DifficultyRange(127, 112, 97);
        private static readonly DifficultyRange meh_window_range = new DifficultyRange(151, 136, 121);
        private static readonly DifficultyRange miss_window_range = new DifficultyRange(188, 173, 158);

        private double speedMultiplier = 1;

        private double perfectRange;
        private double greatRange;
        private double goodRange;
        private double okRange;
        private double mehRange;
        private double missRange;

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

        public bool HasReset { get; private set; }

        /// <summary>
        /// 用于静态Mod覆写，设置后切换自定义判定区间
        /// </summary>
        private static ManiaModifyHitRange? modOverride;

        public static void SetModOverride(ManiaModifyHitRange range) => modOverride = range;
        public static void ClearModOverride() => modOverride = null;

        private double perfect;
        private double great;
        private double good;
        private double ok;
        private double meh;
        private double miss;

        private double bpm;

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

        public bool AllowPoorEnabled => GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.BmsPoorHitResultEnable);

        /// <summary>
        /// 当前 Mania 判定模式（统计与窗口逻辑会依赖此值）。
        /// </summary>
        public EzEnumHitMode ActiveHitMode { get; private set; }

        private readonly HitModeHelper helper = new HitModeHelper();

        public ManiaHitWindows(EzEnumHitMode? hitModeOverride = null)
        {
            ActiveHitMode = hitModeOverride ?? GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            helper.HitMode = ActiveHitMode;
            updateWindows();
        }

        public override bool IsHitResultAllowed(HitResult result)
        {
            // 先检查基础判定是否允许
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                case HitResult.Good:
                case HitResult.Ok:
                case HitResult.Meh:
                case HitResult.Miss:
                    break;

                case HitResult.Poor:
                    return AllowPoorEnabled;

                default:
                    return false;
            }

            // 使用当前实例的 HitMode 判定列表，只允许有判定区间的结果。
            var validResults = HitModeHelper.GetHitModeValidHitResults(ActiveHitMode);
            return validResults.Contains(result);
        }

        public override void SetDifficulty(double difficulty)
        {
            overallDifficulty = difficulty;
            updateWindows();
        }

        private void modifyManiaHitRange(double[] difficultyRangeArray)
        {
            perfectRange = difficultyRangeArray[0];
            greatRange = difficultyRangeArray[1];
            goodRange = difficultyRangeArray[2];
            okRange = difficultyRangeArray[3];
            mehRange = difficultyRangeArray[4];
            missRange = difficultyRangeArray[5];
        }

        public void ResetRange()
        {
            HasReset = true;
            updateWindows();
        }

        private bool setHitMode()
        {
            // 需要先更新属性，保证数值同步
            helper.HitMode = ActiveHitMode;
            helper.OverallDifficulty = overallDifficulty;
            helper.TotalMultiplier = totalMultiplier;

            if (ActiveHitMode == EzEnumHitMode.O2Jam)
                helper.BPM = BPM;

            if (ActiveHitMode == EzEnumHitMode.Lazer)
                return false;

            modifyManiaHitRange(helper.GetHitRangeList);

            return true;
        }

        private void updateWindows()
        {
            if (setHitMode() && !HasReset)
            {
                perfect = perfectRange;
                great = greatRange;
                good = goodRange;
                ok = okRange;
                meh = mehRange;
                miss = missRange;
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
        }

        public override double WindowFor(HitResult result)
        {
            if (modOverride is { } mo)
            {
                return result switch
                {
                    HitResult.Perfect => mo.Perfect,
                    HitResult.Great => mo.Great,
                    HitResult.Good => mo.Good,
                    HitResult.Ok => mo.Ok,
                    HitResult.Meh => mo.Meh,
                    HitResult.Miss => mo.Miss,
                    HitResult.Poor => helper.WindowFor(HitResult.Poor),
                    _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
                };
            }

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

                case HitResult.Miss:
                    return miss;

                case HitResult.Poor:
                    return helper.WindowFor(HitResult.Poor);

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        /// <summary>
        /// Get window for a specific result and direction (early/late) for asymmetric windows
        /// </summary>
        public double WindowFor(HitResult result, bool isEarly)
        {
            return helper.WindowFor(result, isEarly);
        }

        public HitResult ResultFor(double timeOffset, bool? useHelper = null)
        {
            bool shouldUseHelper = useHelper ?? (ActiveHitMode != EzEnumHitMode.Lazer);
            return shouldUseHelper ? helper.ResultFor(timeOffset) : base.ResultFor(timeOffset);
        }

        public void SetHitMode(EzEnumHitMode mode)
        {
            if (ActiveHitMode == mode)
                return;

            ActiveHitMode = mode;
            updateWindows();
        }

        public void UpdateO2JamBpmFromTime(double time)
        {
            if (ActiveHitMode != EzEnumHitMode.O2Jam)
                return;

            BPM = O2HitModeExtension.GetBPMAtTime(time);
        }
    }
}
