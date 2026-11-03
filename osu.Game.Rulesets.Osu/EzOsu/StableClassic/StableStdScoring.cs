// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.StableClassic
{
    /// <summary>
    /// osu!stable std classic（非 ScoreV2）总分累加器，复刻 <c>Ruleset.IncreaseScoreHit</c>。
    /// 圆圈 / 滑条 / 转盘主体按判定给 300/100/50/0 并吃 combo 乘区
    /// <c>max(0, combo-1) * (value/25) * peppyStars * modMultiplier</c>；
    /// 滑条头 30、tick 10、repeat 30、tail 30，转盘 tick 100 / bonus 1100，均不吃乘区。
    /// </summary>
    /// <remarks>
    /// 仅供 StableClassic 路径使用。
    /// stable 的连击把滑条 tick / repeat / tail 也计入，lazer 的 <see cref="ScoreProcessor"/> 连击不计，故此处自管连击。
    /// 另：stable 漏掉滑条尾不会断连（<c>IncreaseScoreType -524288</c>），此处同样不断。
    /// </remarks>
    public sealed class StableStdScoring
    {
        private const int value_perfect = 300;
        private const int value_slider_point = 30;
        private const int value_slider_tick = 10;
        private const int value_spinner_tick = 100;
        private const int value_spinner_bonus_tick = 1100;

        /// <summary>一次基础判定的满分值，也是 stable 准确率分母的单个物件权重。</summary>
        public const int MaxBaseScore = value_perfect;

        private readonly List<UndoState> undoStates = new List<UndoState>();

        private int combo;
        private int maxCombo;
        private int accuracyScore;
        private int comboScore;
        private int bonusScore;

        private int count300;
        private int count100;
        private int count50;
        private int countMiss;

        private double peppyStars = 1;
        private double modMultiplier = 1;

        /// <summary>stable 语义下的当前连击（含滑条 tick / repeat / tail）。</summary>
        public int Combo => combo;

        public int MaxCombo => maxCombo;

        public int Count300 => count300;

        public int Count100 => count100;

        public int Count50 => count50;

        public int CountMiss => countMiss;

        public double PeppyStars => peppyStars;

        public double ModMultiplier => modMultiplier;

        /// <summary>已计分的总判定数（圆圈 / 滑条 / 转盘主体），即 stable 准确率的分母基数。</summary>
        public int JudgedCount => count300 + count100 + count50 + countMiss;

        /// <summary>准确率分子（300 / 100 / 50 加权），用于外部同步 classic 准确率显示。</summary>
        public int AccuracyPoints => count300 * 300 + count100 * 100 + count50 * 50;

        /// <summary>stable 的 TotalScore：基础分 + combo 乘区 + 转盘 bonus，不再走 combo/acc 曲线。</summary>
        public long TotalScore => accuracyScore + (long)comboScore + bonusScore;

        public void Reset(double peppyStars, double modMultiplier)
        {
            this.peppyStars = peppyStars;
            this.modMultiplier = modMultiplier;

            combo = 0;
            maxCombo = 0;
            accuracyScore = 0;
            comboScore = 0;
            bonusScore = 0;
            count300 = 0;
            count100 = 0;
            count50 = 0;
            countMiss = 0;
            undoStates.Clear();
        }

        /// <summary>
        /// 按 replay 帧头重建状态（跳帧 / 后退）。帧头只带判定计数与总分，故不做重放，
        /// 直接按 stable 的分数分解反推：准确率分只认主体判定，bonus 只认转盘 tick，余下即 combo 乘区。
        /// </summary>
        public void RestoreFrom(int restoredCombo, int restoredMaxCombo, IReadOnlyDictionary<HitResult, int> statistics, long totalScore)
        {
            undoStates.Clear();

            combo = restoredCombo;
            maxCombo = Math.Max(restoredMaxCombo, restoredCombo);

            count300 = getCount(statistics, HitResult.Great) + getCount(statistics, HitResult.Perfect);
            count100 = getCount(statistics, HitResult.Ok);
            count50 = getCount(statistics, HitResult.Meh);
            countMiss = getCount(statistics, HitResult.Miss);

            accuracyScore = AccuracyPoints;
            bonusScore = getCount(statistics, HitResult.SmallBonus) * value_spinner_tick
                         + getCount(statistics, HitResult.LargeBonus) * value_spinner_bonus_tick;
            comboScore = (int)Math.Max(0, totalScore - accuracyScore - bonusScore);
        }

        private static int getCount(IReadOnlyDictionary<HitResult, int> statistics, HitResult result)
            => statistics.TryGetValue(result, out int count) ? count : 0;

        /// <summary>
        /// 累加一次判定。<paramref name="hitObject"/> 决定计分档位，<paramref name="result"/> 决定命中 / 失手。
        /// 未参与 stable 计分的物件（Ignore 系）记为空操作，但仍压栈以便 <see cref="Revert"/> 对称。
        /// </summary>
        /// <remarks>
        /// combo 乘区必须取「本次判定入账前」的连击，与 stable <c>Ruleset.IncreaseScoreHit</c> 一致：
        /// 它先读 <c>ComboCounter.HitCombo</c> 算分，之后才把连击 +1。圆圈 / 转盘主体自己就涨连击，
        /// 若先涨再算，会比 stable 每击多算一个档位。
        /// </remarks>
        public void Apply(HitObject hitObject, HitResult result)
        {
            undoStates.Add(new UndoState(combo, maxCombo, accuracyScore, comboScore, bonusScore, count300, count100, count50, countMiss));

            if (!tryResolve(hitObject, result, out var score))
                return;

            if (score.ApplyComboMultiplier && score.BaseValue > 0)
                comboScore += (int)(Math.Max(0, combo - 1) * (score.BaseValue / 25) * peppyStars * modMultiplier);

            if (score.IncreaseCombo)
                combo++;
            else if (score.AffectsCombo)
                combo = 0;

            if (combo > maxCombo)
                maxCombo = combo;

            if (score.IsBonus)
                bonusScore += score.BaseValue;
            else
                accuracyScore += score.BaseValue;
        }

        /// <summary>回退最近一次 <see cref="Apply"/>（rewind / revert）。</summary>
        public void Revert()
        {
            if (undoStates.Count == 0)
                return;

            UndoState state = undoStates[^1];
            undoStates.RemoveAt(undoStates.Count - 1);

            combo = state.Combo;
            maxCombo = state.MaxCombo;
            accuracyScore = state.AccuracyScore;
            comboScore = state.ComboScore;
            bonusScore = state.BonusScore;
            count300 = state.Count300;
            count100 = state.Count100;
            count50 = state.Count50;
            countMiss = state.CountMiss;
        }

        /// <summary>classic 准确率：geki 系不入账，分母为总判定 × 300。</summary>
        public double Accuracy => JudgedCount == 0 ? 1 : AccuracyPoints / (JudgedCount * 300.0);

        private bool tryResolve(HitObject hitObject, HitResult result, out HitScore score)
        {
            switch (hitObject)
            {
                // SliderHeadCircle / SliderEndCircle 都派生自 HitCircle，必须先于 HitCircle 分支。
                case SliderHeadCircle:
                case SliderRepeat:
                    score = new HitScore(value_slider_point, applyComboMultiplier: false, affectsCombo: true, increaseCombo: result.IsHit(), isBonus: false);
                    return true;

                // stable 漏掉滑条尾时不断连（-524288 不落 combo），但命中仍然加连击。
                case SliderTailCircle:
                    score = new HitScore(value_slider_point, applyComboMultiplier: false, affectsCombo: false, increaseCombo: result.IsHit(), isBonus: false);
                    return true;

                case SliderTick:
                    score = new HitScore(value_slider_tick, applyComboMultiplier: false, affectsCombo: true, increaseCombo: result.IsHit(), isBonus: false);
                    return true;

                case SpinnerBonusTick:
                    score = new HitScore(value_spinner_bonus_tick, applyComboMultiplier: false, affectsCombo: false, increaseCombo: false, isBonus: true);
                    return true;

                case SpinnerTick:
                    score = new HitScore(value_spinner_tick, applyComboMultiplier: false, affectsCombo: false, increaseCombo: false, isBonus: true);
                    return true;

                case HitCircle:
                case Spinner:
                    int value = BaseScoreForResult(result);

                    countByResult(result);

                    score = new HitScore(value, applyComboMultiplier: true, affectsCombo: true, increaseCombo: result.IsHit(), isBonus: false);
                    return true;

                // 滑条主体只贡献判定与乘区；连击由滑条头（classic 下按 tick 记）承担，与 stable 一致。
                case Slider:
                    countByResult(result);

                    score = new HitScore(BaseScoreForResult(result), applyComboMultiplier: true, affectsCombo: false, increaseCombo: false, isBonus: false);
                    return true;

                default:
                    score = default;
                    return false;
            }
        }

        private void countByResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Great:
                case HitResult.Perfect:
                    count300++;
                    break;

                case HitResult.Ok:
                case HitResult.Good:
                    count100++;
                    break;

                case HitResult.Meh:
                    count50++;
                    break;

                case HitResult.Miss:
                    countMiss++;
                    break;
            }
        }

        /// <summary>
        /// classic 档位分值：圆圈 / 滑条 / 转盘主体按 300 / 100 / 50 / 0 计，tick 与 bonus 不计入准确率。
        /// </summary>
        public static int BaseScoreForResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Great:
                case HitResult.Perfect:
                    return value_perfect;

                case HitResult.Ok:
                case HitResult.Good:
                    return 100;

                case HitResult.Meh:
                    return 50;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// stable <c>Beatmap.DifficultyPeppyStars</c>：用未改动的原始难度（<see cref="IBeatmap.BeatmapInfo"/> 未被 mod 改写）
        /// 与物件密度结算。与 <c>OsuLegacyScoreSimulator</c> 同源。
        /// </summary>
        public static int PeppyStarsFor(IBeatmap beatmap)
        {
            int objectCount = 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                switch (hitObject)
                {
                    case Slider:
                    case Spinner:
                    case HitCircle:
                        objectCount++;
                        break;
                }
            }

            int drainLength = 0;

            if (beatmap.HitObjects.Count > 0)
            {
                int breakLength = beatmap.Breaks.Sum(b => (int)Math.Round(b.EndTime) - (int)Math.Round(b.StartTime));
                drainLength = ((int)Math.Round(beatmap.HitObjects[^1].StartTime) - (int)Math.Round(beatmap.HitObjects[0].StartTime) - breakLength) / 1000;
            }

            return LegacyRulesetExtensions.CalculateDifficultyPeppyStars(beatmap.BeatmapInfo.Difficulty, objectCount, drainLength);
        }

        /// <summary>
        /// 已判定区间全 Perfect 时应达到的总分：stable 的乘区次序无法从帧头 / 进度反推，
        /// 退化为按已判定比例线性投影到满分。
        /// </summary>
        public static double ProjectPerfectJudged(long maximumTotalScore, int judgedHits, int maximumJudgements)
            => maximumTotalScore * (double)judgedHits / Math.Max(1, maximumJudgements);

        /// <summary>stable <c>ModManager.ScoreMultiplier</c>（std classic，非 ScoreV2）。</summary>
        public static double ModMultiplierFor(IReadOnlyList<Mod> mods)
        {
            double multiplier = 1;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case OsuModNoFail:
                    case OsuModEasy:
                        multiplier *= 0.5;
                        break;

                    case OsuModHalfTime:
                    case OsuModDaycore:
                        multiplier *= 0.3;
                        break;

                    case OsuModHidden:
                    case OsuModHardRock:
                        multiplier *= 1.06;
                        break;

                    case OsuModDoubleTime:
                    case OsuModNightcore:
                    case OsuModFlashlight:
                        multiplier *= 1.12;
                        break;

                    case OsuModSpunOut:
                        multiplier *= 0.9;
                        break;

                    case OsuModRelax:
                    case OsuModAutopilot:
                        return 0;
                }
            }

            return multiplier;
        }

        private readonly struct HitScore
        {
            public readonly int BaseValue;
            public readonly bool ApplyComboMultiplier;
            public readonly bool AffectsCombo;
            public readonly bool IncreaseCombo;
            public readonly bool IsBonus;

            public HitScore(int baseValue, bool applyComboMultiplier, bool affectsCombo, bool increaseCombo, bool isBonus)
            {
                BaseValue = baseValue;
                ApplyComboMultiplier = applyComboMultiplier;
                AffectsCombo = affectsCombo;
                IncreaseCombo = increaseCombo;
                IsBonus = isBonus;
            }
        }

        private readonly struct UndoState
        {
            public readonly int Combo;
            public readonly int MaxCombo;
            public readonly int AccuracyScore;
            public readonly int ComboScore;
            public readonly int BonusScore;
            public readonly int Count300;
            public readonly int Count100;
            public readonly int Count50;
            public readonly int CountMiss;

            public UndoState(int combo, int maxCombo, int accuracyScore, int comboScore, int bonusScore, int count300, int count100, int count50, int countMiss)
            {
                Combo = combo;
                MaxCombo = maxCombo;
                AccuracyScore = accuracyScore;
                ComboScore = comboScore;
                BonusScore = bonusScore;
                Count300 = count300;
                Count100 = count100;
                Count50 = count50;
                CountMiss = countMiss;
            }
        }
    }
}
