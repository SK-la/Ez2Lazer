// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <summary>
    /// 附加分倾向：类余切贴横轴，类反余切是它转 180° 后贴着 ±1。默认类反余切。
    /// </summary>
    public enum EzScoreBonusTendency
    {
        /// <summary>
        /// 类余切：价值贴着横轴，44ms 到 16ms 加得很慢，16ms 到 6ms 加得快。
        /// </summary>
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCORE_BONUS_TENDENCY_JUDGE_TO_MISS))]
        JudgeToMiss = 0,

        /// <summary>
        /// 类反余切：6–44 是类余切绕中心转 180°，价值贴着 +1 和 -1。默认。
        /// </summary>
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCORE_BONUS_TENDENCY_MISS_TO_JUDGE))]
        MissToJudge = 1,
    }

    /// <summary>
    /// 同一次计算产出的两种倾向结果；切换设置只换取值，不需要重跑回放。
    /// </summary>
    public readonly record struct EzScoreBonusSet(EzScoreBonusResult JudgeToMiss, EzScoreBonusResult MissToJudge)
    {
        public EzScoreBonusResult For(EzScoreBonusTendency tendency)
            => tendency == EzScoreBonusTendency.MissToJudge ? MissToJudge : JudgeToMiss;
    }
}
