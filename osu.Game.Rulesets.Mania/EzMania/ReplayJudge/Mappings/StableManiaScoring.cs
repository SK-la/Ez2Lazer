// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// osu!stable mania classic（非 ScoreV2）总分累加器，复刻 <c>ComboAddition</c> 状态机。
    /// 仅供 StableClassic 判定档使用。
    /// </summary>
    /// <remarks>
    /// 总分不再是 combo/acc 曲线，而是逐物件累加的 <c>TotalScoreDouble</c>；结束时长条取整（999999 抬到 1000000）。
    /// stable 的每物件增量：<c>sqrt(ComboAddition)*num4*SM/320 + base*SM/320</c>，SM 已包含 500000/物件数 的归一化。
    /// </remarks>
    public sealed class StableManiaScoring
    {
        public const int ScoreMax = 500000;
        public const int Score300g = 320;
        public const int ComboMulti = 100;
        public const int ComboBonus = 384;

        private readonly List<(double comboAddition, double delta)> undoStates = new List<(double, double)>();

        private double comboAddition = ComboMulti;
        private double totalScoreDouble;

        /// <summary>归一化 + mod 乘数（stable 的 <c>Ruleset.ScoreMultiplier</c>）。</summary>
        public double ScoreMultiplier { get; private set; } = 1;

        public double TotalScoreDouble => totalScoreDouble;

        public void Reset(double scoreMultiplier)
        {
            comboAddition = ComboMulti;
            totalScoreDouble = 0;
            undoStates.Clear();
            ScoreMultiplier = scoreMultiplier;
        }

        /// <param name="comboAtJudgement">本物件判定前的 combo（stable 的 <c>CurrentCombo</c>）。</param>
        public void Apply(StableManiaJudgement judgement, int comboAtJudgement)
        {
            double previous = comboAddition;
            int num4;
            int baseScore;

            switch (judgement)
            {
                default:
                case StableManiaJudgement.Miss:
                    comboAddition = -56;
                    num4 = 0;
                    baseScore = 0;
                    break;

                case StableManiaJudgement.Meh50:
                    comboAddition -= 44;
                    num4 = 4;
                    baseScore = 50;
                    break;

                case StableManiaJudgement.Ok100:
                    comboAddition -= 24;
                    num4 = 8;
                    baseScore = 100;
                    break;

                case StableManiaJudgement.Good200:
                    comboAddition -= 8;
                    num4 = 16;
                    baseScore = 200;
                    break;

                case StableManiaJudgement.Great300:
                    comboAddition += 1;
                    num4 = 32;
                    baseScore = 300;
                    break;

                case StableManiaJudgement.Perfect300g:
                    comboAddition += 2;
                    num4 = 32;
                    baseScore = Score300g;
                    break;
            }

            if (comboAtJudgement != 0 && comboAtJudgement % ComboBonus == 0)
                comboAddition = ComboMulti;

            comboAddition = Math.Max(0, Math.Min(comboAddition, ComboMulti));

            double num2 = Math.Sqrt(comboAddition) * num4 * ScoreMultiplier / Score300g;
            double delta = num2 + baseScore * ScoreMultiplier / Score300g;

            totalScoreDouble += delta;
            undoStates.Add((previous, delta));
        }

        /// <summary>回退最近一次 <see cref="Apply"/>（rewind / revert）。</summary>
        public void Revert()
        {
            if (undoStates.Count == 0)
                return;

            (double comboAddition, double delta) state = undoStates[^1];
            undoStates.RemoveAt(undoStates.Count - 1);

            comboAddition = state.comboAddition;
            totalScoreDouble -= state.delta;
        }

        /// <summary>
        /// 按 replay 帧头重建状态（跳帧 / 后退）。分数直接取帧头值（与取整后总分只差 &lt;=0.5）。
        /// </summary>
        /// <remarks>
        /// <c>ComboAddition</c> 是 0..<see cref="ComboMulti"/> 的有界状态，只由「最近若干次判定」决定，
        /// 帧头（计数 + 总分 + 连击）反推不出，故取 <see cref="ComboMulti"/>：
        /// 即「最近连段全是 300g」的不变量，对通常的回退点是精确值，其余情形误差也被上限夹住。
        /// </remarks>
        public void RestoreFrom(double totalScore)
        {
            undoStates.Clear();

            totalScoreDouble = totalScore;
            comboAddition = ComboMulti;
        }

        /// <summary>结束分：四舍五入，999999 抬到 1000000（stable <c>OnCompletion</c>）。</summary>
        public static long Finalize(double totalScoreDouble)
        {
            long value = (long)Math.Round(totalScoreDouble);

            return value == 999999 ? 1000000 : value;
        }

        public long Total => Finalize(totalScoreDouble);

        /// <summary>stable <c>ModManager.ScoreMultiplier</c>（mania）：EZ/NF/HT 减半，SpunOut/Relax 归零；HD/HR/DT/FL/FI 均为 1。</summary>
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

        /// <summary>stable 的乘窗系数 = 播放速度 / 难度系数（DT·HT 与 HR·EZ 的合成）。</summary>
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
    }
}
