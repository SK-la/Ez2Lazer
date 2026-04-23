// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    // 代码改编自YuLiangSSS提供的ManiaModO2Judgement
    public static partial class O2HitModeExtension
    {
        // 💊数量（可绑定）
        // 上限为 5，在达到一定 Cool 连击后会增加，发生较大偏移时会减少。
        public static readonly Bindable<int> PILL_COUNT = new Bindable<int>(0);

        public const double BASE_COOL = 7500.0;
        public const double BASE_GOOD = 22500.0;
        public const double BASE_BAD = 31250.0;

        // 启用 Pill 模式的特殊判定逻辑（如累积/消耗 Pill、使用 CoolCombo 逻辑等）。
        // 注意：初始值和持久化逻辑取决于外部设置/开关，这里仅作为全局运行时状态使用。
        public static bool PillActivated; // = ManiaModO2Judgement.PillMode.Value;

        // Cool 连击计数（用于追踪在 Cool 判定内的连续命中次数）
        // 语义：每次命中判断在 Cool 范围内时递增；当计数达到 15 时会重置（减去 15）并使 `Pill` 增加（最多至 5）。
        // 若在 Good 范围内则重置为 0；若落入 Bad 范围且拥有 Pill 会消耗 1 个 Pill 并替换判定为 Perfect（见使用处）。
        public static int CoolCombo;

        // 当前时间戳的控制点信息，用于动态计算 BPM 相关范围
        private static ControlPointInfo? currentControlPoints;

        // 保存原始 BPM 值
        private static double originalBPM = 120.0;
        public static bool IsPlaying = true;

        /// <summary>
        /// 设置当前谱面的控制点信息，用于动态 BPM 计算
        /// </summary>
        /// <param name="controlPoints">谱面的控制点信息</param>
        public static void SetControlPoints(ControlPointInfo? controlPoints)
        {
            currentControlPoints = controlPoints;
        }

        /// <summary>
        /// 设置原始 BPM 值
        /// </summary>
        /// <param name="bpm">原始 BPM 值</param>
        public static void SetOriginalBPM(double bpm)
        {
            originalBPM = bpm;
        }

        /// <summary>
        /// 根据当前时间获取动态 BPM
        /// </summary>
        /// <param name="time">当前时间</param>
        /// <returns>对应时间的 BPM，最低为 120</returns>
        public static double GetBPMAtTime(double time)
        {
            if (currentControlPoints != null && IsPlaying)
            {
                var timingPoint = currentControlPoints.TimingPointAt(time);
                // 确保 BPM 不低于 120
                return Math.Max(timingPoint.BPM, 75.0);
            }

            // 如果没有控制点信息，则使用原始 BPM 值，同样确保不低于 120
            return Math.Max(originalBPM, 120);
        }

        /// <summary>
        /// 根据当前时间获取 Cool 判定范围
        /// </summary>
        /// <param name="time">当前时间</param>
        /// <returns>Cool 判定范围</returns>
        public static double GetCoolRangeAtTime(double time) => BASE_COOL / GetBPMAtTime(time);

        /// <summary>
        /// 根据当前时间获取 Good 判定范围
        /// </summary>
        /// <param name="time">当前时间</param>
        /// <returns>Good 判定范围</returns>
        public static double GetGoodRangeAtTime(double time) => BASE_GOOD / GetBPMAtTime(time);

        /// <summary>
        /// 根据当前时间获取 Bad 判定范围
        /// </summary>
        /// <param name="time">当前时间</param>
        /// <returns>Bad 判定范围</returns>
        public static double GetBadRangeAtTime(double time) => BASE_BAD / GetBPMAtTime(time);

        /// <summary>
        /// 更新 CoolCombo 值，自动处理溢出逻辑
        /// </summary>
        public static void IncrementCoolCombo()
        {
            if (++CoolCombo >= 15)
            {
                CoolCombo = 0;
                // 使用 Clamp 统一约束范围，确保在 [0, 5] 范围内
                PILL_COUNT.Value = Math.Clamp(PILL_COUNT.Value + 1, 0, 5);
            }
        }

        /// <summary>
        /// 统一的 Pill 判定逻辑：将原本分散在各 Drawable 的重复实现合并到这里。
        /// 返回值：true 表示继续执行后续判定逻辑；false 表示应中断后续判定（保留以便未来扩展）。
        /// out 参数：
        /// - <paramref name="applyComboBreak"/>：当命中落入 Bad 范围且没有可用 Pill 时为 true。
        /// - <paramref name="upgradeToPerfect"/>：当命中落入 Bad 范围且消耗了 Pill 时为 true（调用者应将该次判定提升为 <see cref="HitResult.Perfect"/>）。
        /// </summary>
        /// <param name="timeOffset">时间偏移</param>
        /// <param name="currentTime">当前游戏时间</param>
        /// <param name="applyComboBreak">当命中落入 Bad 范围且没有可用 Pill 时为 true</param>
        /// <param name="upgradeToPerfect">当命中落入 Bad 范围且消耗了 Pill 时为 true</param>
        public static bool PillCheck(double timeOffset, double currentTime, out bool applyComboBreak, out bool upgradeToPerfect)
        {
            applyComboBreak = false;
            upgradeToPerfect = false;

            if (!PillActivated)
                return true;

            double absOffset = Math.Abs(timeOffset);

            // 根据当前时间获取动态范围
            double coolRange = GetCoolRangeAtTime(currentTime);
            double goodRange = GetGoodRangeAtTime(currentTime);
            double badRange = GetBadRangeAtTime(currentTime);

            // Logger.Log("[O2HitModeExtension] Ranges at time " + currentTime + ": Cool=" + coolRange + ", Good=" + goodRange + ", Bad=" + badRange);

            if (absOffset <= coolRange)
            {
                IncrementCoolCombo();
            }
            else if (absOffset <= goodRange)
            {
                CoolCombo = 0;
            }
            else if (absOffset <= badRange)
            {
                CoolCombo = 0;

                if (PILL_COUNT.Value > 0)
                {
                    // 使用 Clamp 统一约束范围，确保在 [0, 5] 范围内
                    PILL_COUNT.Value = Math.Clamp(PILL_COUNT.Value - 1, 0, 5);
                    upgradeToPerfect = true; // 升级为 Perfect 判定
                }
                else
                {
                    applyComboBreak = true; // 无法挽救，断连
                }
            }

            return true;
        }
    }
}
