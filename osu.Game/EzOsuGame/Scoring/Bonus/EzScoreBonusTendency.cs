// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <summary>
    /// 附加分倾向：两项 KPS 权重方向互为镜像。
    /// </summary>
    public enum EzScoreBonusTendency
    {
        /// <summary>
        /// 判定加成看重低 KPS 区间；Miss 罚分 KPS 越高越重。
        /// </summary>
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.SCORE_BONUS_TENDENCY_JUDGE_TO_MISS))]
        JudgeToMiss = 0,

        /// <summary>
        /// 判定加成看重高 KPS 区间；Miss 罚分 KPS 越低越重。
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
