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
