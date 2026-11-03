// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// stable mania classic 总分累加器，复刻 <c>ComboAddition</c> 状态机。
    /// 仅供 StableClassic 判定档使用。
    /// </summary>
    /// <remarks>
    /// 总分不再是 combo/acc 曲线，而是逐物件累加的 <c>TotalScoreDouble</c>；结束时长条取整（999999 抬到 1000000）。
    /// 每物件增量：<c>sqrt(ComboAddition) * 档位权重 * ScoreMultiplier / 320 + 基础分 * ScoreMultiplier / 320</c>，
    /// <c>ScoreMultiplier</c> 已包含 500000 / 物件数 的归一化。
    /// </remarks>
    public sealed class StableManiaScoring
    {
        public const int SCORE_MAX = 500000;
        public const int SCORE300G = 320;
        public const int COMBO_MULTI = 100;
        public const int COMBO_BONUS = 384;

        private readonly List<(double comboAddition, double delta)> undoStates = new List<(double, double)>();

        // 扣分项除数：只取加成类 mod 的分数系数（与 ScoreMultiplier 无关，后者已排除加成类 mod）。
        private double penaltyDivisor = 1;

        /// <summary>归一化 + mod 乘数（stable 的 <c>ScoreMultiplier</c>）。</summary>
        public double ScoreMultiplier { get; private set; } = 1;

        public double TotalScoreDouble { get; private set; }

        /// <summary>当前 <c>ComboAddition</c>（0..<see cref="COMBO_MULTI"/>），供诊断与测试读取。</summary>
        public double ComboAddition { get; private set; } = COMBO_MULTI;

        /// <param name="scoreMultiplier">归一化 + mod 乘数，用于每物件增量。</param>
        /// <param name="scoreIncreaseMultiplier">加成类 mod 的分数系数，只作为扣分项的除数。</param>
        public void Reset(double scoreMultiplier, double scoreIncreaseMultiplier = 1)
        {
            ComboAddition = COMBO_MULTI;
            TotalScoreDouble = 0;
            undoStates.Clear();
            ScoreMultiplier = scoreMultiplier;
            penaltyDivisor = scoreIncreaseMultiplier > 0 ? scoreIncreaseMultiplier : 1;
        }

        /// <param name="comboAtJudgement">本物件判定前的 combo。</param>
        public void Apply(StableManiaJudgement judgement, int comboAtJudgement)
        {
            double previous = ComboAddition;
            int comboWeight;
            int baseScore;

            switch (judgement)
            {
                default:
                case StableManiaJudgement.Miss:
                    ComboAddition = -56 / penaltyDivisor;
                    comboWeight = 0;
                    baseScore = 0;
                    break;

                case StableManiaJudgement.Meh50:
                    ComboAddition -= 44 / penaltyDivisor;
                    comboWeight = 4;
                    baseScore = 50;
                    break;

                case StableManiaJudgement.Ok100:
                    ComboAddition -= 24 / penaltyDivisor;
                    comboWeight = 8;
                    baseScore = 100;
                    break;

                case StableManiaJudgement.Good200:
                    ComboAddition -= 8 / penaltyDivisor;
                    comboWeight = 16;
                    baseScore = 200;
                    break;

                case StableManiaJudgement.Great300:
                    ComboAddition += 1;
                    comboWeight = 32;
                    baseScore = 300;
                    break;

                case StableManiaJudgement.Perfect300g:
                    ComboAddition += 2;
                    comboWeight = 32;
                    baseScore = SCORE300G;
                    break;
            }

            if (comboAtJudgement != 0 && comboAtJudgement % COMBO_BONUS == 0)
                ComboAddition = COMBO_MULTI;

            ComboAddition = Math.Max(0, Math.Min(ComboAddition, COMBO_MULTI));

            double comboScore = Math.Sqrt(ComboAddition) * comboWeight * ScoreMultiplier / SCORE300G;
            double delta = comboScore + baseScore * ScoreMultiplier / SCORE300G;

            TotalScoreDouble += delta;
            undoStates.Add((previous, delta));
        }

        /// <summary>
        /// 长条持有期的连击增量：只推进 combo，不加分、不进判定计数。
        /// </summary>
        /// <remarks>
        /// 384 重置对持有增量同样生效，且用的是<strong>自增前</strong>的 combo，
        /// 故调用方须在 <c>ScoreProcessor.Combo</c> 自增之前调用本方法。
        /// 增量不产生判定结果，故不进 undo 栈；跳帧由 <see cref="RestoreFrom"/> 整体重建。
        /// </remarks>
        /// <param name="comboAtTick">本拍增量之前的 combo。</param>
        public void ApplyHoldTick(int comboAtTick)
        {
            if (comboAtTick != 0 && comboAtTick % COMBO_BONUS == 0)
                ComboAddition = COMBO_MULTI;
        }

        /// <summary>回退最近一次 <see cref="Apply"/>（rewind / revert）。</summary>
        public void Revert()
        {
            if (undoStates.Count == 0)
                return;

            (double comboAddition, double delta) state = undoStates[^1];
            undoStates.RemoveAt(undoStates.Count - 1);

            ComboAddition = state.comboAddition;
            TotalScoreDouble -= state.delta;
        }

        /// <summary>
        /// 按 replay 帧头重建状态（跳帧 / 后退）。分数直接取帧头值（与取整后总分只差 &lt;=0.5）。
        /// </summary>
        /// <remarks>
        /// <c>ComboAddition</c> 是 0..<see cref="COMBO_MULTI"/> 的有界状态，只由「最近若干次判定」决定，
        /// 帧头（计数 + 总分 + 连击）反推不出，故取 <see cref="COMBO_MULTI"/>：
        /// 即「最近连段全是 300g」的不变量，对通常的回退点是精确值，其余情形误差也被上限夹住。
        /// </remarks>
        public void RestoreFrom(double totalScore)
        {
            undoStates.Clear();

            TotalScoreDouble = totalScore;
            ComboAddition = COMBO_MULTI;
        }

        /// <summary>结束分：四舍五入，999999 抬到 1000000（stable 的 <c>OnCompletion</c>）。</summary>
        public static long Finalize(double totalScoreDouble)
        {
            long value = (long)Math.Round(totalScoreDouble);

            return value == 999999 ? 1000000 : value;
        }

        public long Total => Finalize(TotalScoreDouble);

        /// <summary>mania 的 <c>ScoreMultiplier</c>：EZ/NF/HT 减半，Relax 归零；HD/HR/DT/FL/FI 均为 1。</summary>
        public static double ModMultiplier(IReadOnlyList<Mod> mods)
        {
            double multiplier = 1;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ManiaModNoFail:
                    case ManiaModEasy:
                    case ManiaModHalfTime:
                    case ManiaModDaycore:
                        multiplier *= 0.5;
                        break;

                    case ModRelax:
                        multiplier *= 0;
                        break;
                }
            }

            return multiplier;
        }

        /// <summary>乘窗系数 = 播放速度 / 难度系数（DT·HT 与 HR·EZ 的合成）。</summary>
        public static double WindowMultiplier(IReadOnlyList<Mod> mods)
        {
            double speed = 1;
            double difficulty = 1;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case IManiaRateAdjustmentMod rate:
                        speed = rate.SpeedChange.Value;
                        break;

                    case ManiaModHardRock:
                        difficulty = ManiaModHardRock.HIT_WINDOW_DIFFICULTY_MULTIPLIER;
                        break;

                    case ManiaModEasy:
                        difficulty = ManiaModEasy.HIT_WINDOW_DIFFICULTY_MULTIPLIER;
                        break;
                }
            }

            return speed / difficulty;
        }

        /// <summary>
        /// 只取加成类 mod 时的 <c>ScoreMultiplier</c>，作为扣分项的除数：
        /// HD / FI ×1.06、HR ×1.08、DT（含 NC）×1.1、FL ×1.06。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="ModMultiplier"/> 分开：后者已排除加成类 mod，两处混用会让每物件增量与扣分同时变化。
        /// </remarks>
        public static double ScoreIncreaseMultiplier(IReadOnlyList<Mod> mods)
        {
            double multiplier = 1;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ManiaModHidden:
                    case ManiaModFlashlight:
                        multiplier *= 1.06;
                        break;

                    case ManiaModHardRock:
                        multiplier *= 1.08;
                        break;

                    case ModDoubleTime:
                    case ModNightcore:
                        multiplier *= 1.1;
                        break;
                }
            }

            return multiplier;
        }
    }
}
