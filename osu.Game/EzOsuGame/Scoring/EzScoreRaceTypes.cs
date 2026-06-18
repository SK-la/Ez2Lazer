// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.EzOsuGame.Scoring
{
    public enum EzScorePickCriterion
    {
        [Description("Total score")]
        TotalScore,

        [Description("Accuracy")]
        Accuracy,

        [Description("Max combo")]
        MaxCombo,

        [Description("Miss count")]
        MissCount,
    }

    /// <summary>
    /// 三柱对比条件：筛选要对比的历史成绩；柱高始终按分数绘制。
    /// </summary>
    public enum EzScoreCompareCondition
    {
        [Description("理论最高分数")]
        TheoreticalMaxScore,

        [Description("历史最高分数")]
        BestTotalScore,

        [Description("历史最高 Acc")]
        BestAccuracy,

        [Description("历史最大 Combo")]
        BestMaxCombo,

        [Description("历史最低 Miss")]
        BestMissCount,
    }

    public enum EzScoreRaceSortCriterion
    {
        [Description("Total score")]
        TotalScore,

        [Description("Accuracy")]
        Accuracy,

        [Description("Miss count")]
        MissCount,
    }

    public enum EzScoreModFilter
    {
        [Description("Same mods as current")]
        SameAsCurrent,

        [Description("Any mods")]
        Any,
    }
}
