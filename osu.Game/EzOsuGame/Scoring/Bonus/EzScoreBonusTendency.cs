// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    public enum EzScoreBonusTendency
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCORE_BONUS_TENDENCY_JUDGE_TO_MISS))]
        JudgeToMiss = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCORE_BONUS_TENDENCY_MISS_TO_JUDGE))]
        MissToJudge = 1,
    }

    /// <summary>
    /// 按谱面星级在判定加成与 Miss 罚分之间分配权重：低星端强调一项、高星端强调另一项，中间平滑过渡。
    /// 只在展示层应用，原始附加分不变。
    /// </summary>
    public static class EzScoreBonusWeighting
    {
        public const double STAR_LOW = 3;
        public const double STAR_HIGH = 8;

        /// <summary>
        /// 被弱化一侧保留的最低权重。
        /// </summary>
        public const double MIN_FACTOR = 0.25;

        public static EzScoreBonusResult Resolve(EzScoreBonusResult raw, double starRating, EzScoreBonusTendency tendency)
        {
            double t = Math.Clamp((starRating - STAR_LOW) / (STAR_HIGH - STAR_LOW), 0, 1);
            t = t * t * (3 - 2 * t);

            double towardHigh = MIN_FACTOR + (1 - MIN_FACTOR) * t;
            double towardLow = 1 - (1 - MIN_FACTOR) * t;

            (double judgeFactor, double missFactor) = tendency == EzScoreBonusTendency.JudgeToMiss
                ? (towardLow, towardHigh)
                : (towardHigh, towardLow);

            return raw with
            {
                JudgeBonus = (int)Math.Round(raw.JudgeBonus * judgeFactor),
                MissPenalty = (int)Math.Round(raw.MissPenalty * missFactor),
            };
        }
    }
}
