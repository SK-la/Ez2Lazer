// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 角逐 HUD 共用指标维度。三柱 HUD 用于筛选对比哪条成绩；角逐榜用于实时排序。
    /// </summary>
    public enum EzScoreRaceMetric
    {
        [LocalisableDescription(typeof(EzScoreRaceStrings), nameof(EzScoreRaceStrings.METRIC_THEORETICAL_MAX_SCORE))]
        TheoreticalMaxScore,

        [LocalisableDescription(typeof(EzScoreRaceStrings), nameof(EzScoreRaceStrings.METRIC_TOTAL_SCORE))]
        TotalScore,

        [LocalisableDescription(typeof(EzScoreRaceStrings), nameof(EzScoreRaceStrings.METRIC_ACCURACY))]
        Accuracy,

        [LocalisableDescription(typeof(EzScoreRaceStrings), nameof(EzScoreRaceStrings.METRIC_MAX_COMBO))]
        MaxCombo,

        [LocalisableDescription(typeof(EzScoreRaceStrings), nameof(EzScoreRaceStrings.METRIC_MISS_COUNT))]
        MissCount,
    }

    public enum EzScoreModFilter
    {
        [LocalisableDescription(typeof(EzScoreRaceStrings), nameof(EzScoreRaceStrings.MOD_FILTER_SAME_AS_CURRENT))]
        SameAsCurrent,

        [LocalisableDescription(typeof(EzScoreRaceStrings), nameof(EzScoreRaceStrings.MOD_FILTER_ANY))]
        Any,
    }

    public static class EzScoreRaceStrings
    {
        public static readonly LocalisableString METRIC_THEORETICAL_MAX_SCORE = new EzLocalizationManager.EzLocalisableString("理论最高分", "Theoretical max score");
        public static readonly LocalisableString METRIC_TOTAL_SCORE = new EzLocalizationManager.EzLocalisableString("分数", "Score");
        public static readonly LocalisableString METRIC_ACCURACY = new EzLocalizationManager.EzLocalisableString("Acc", "Acc");
        public static readonly LocalisableString METRIC_MAX_COMBO = new EzLocalizationManager.EzLocalisableString("Combo", "Combo");
        public static readonly LocalisableString METRIC_MISS_COUNT = new EzLocalizationManager.EzLocalisableString("Miss", "Miss");

        public static readonly LocalisableString MOD_FILTER_SAME_AS_CURRENT = new EzLocalizationManager.EzLocalisableString("相同 Mod", "Same mods as current");
        public static readonly LocalisableString MOD_FILTER_ANY = new EzLocalizationManager.EzLocalisableString("任意 Mod", "Any mods");
    }
}
