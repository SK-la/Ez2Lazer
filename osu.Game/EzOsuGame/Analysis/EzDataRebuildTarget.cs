// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Analysis
{
    public enum EzDataRebuildTarget
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_TAGS))]
        RealmTags = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_XXY))]
        RealmXxy = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_PP))]
        RealmPp = 2,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_ALL))]
        RealmAll = 3,

        /// <summary>
        /// 全量成绩重算（修复被错误后台转换破坏的本地成绩）。
        /// 尝试补算 = 仅 mania；完全重算 = 全部游戏模式。
        /// </summary>
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_SCORES))]
        RealmScores = 4,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_SQLITE_MAIN))]
        SqliteMain = 5,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_SQLITE_BRANCHES))]
        SqliteSongsBranches = 6,

        /// <summary>
        /// Scene: beatmap MSD (4K Skill yellow, DualPanel Rating feed, ChartDan inputs).
        /// Scope: <see cref="Database.EzRealmMetadataScope.Msd"/> only.
        /// </summary>
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_CHART_MSD))]
        RealmChartMSD = 7,

        /// <summary>
        /// Scene: DualPanel chart side — CSI tags + ChartDan (no Rating).
        /// Scope: <see cref="Database.EzRealmMetadataScope.ChartSkillInfo"/> | <see cref="Database.EzRealmMetadataScope.ChartDan"/>.
        /// Assumes MSD already present; ChartDan defers maps still waiting on MSD.
        /// </summary>
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_CHART_CSI_AND_DAN))]
        RealmChartCSIAndDan = 8,

        /// <summary>
        /// Scene: full chart skill chain — MSD + CSI + ChartDan (includes Rating feed).
        /// Scope: <see cref="Database.EzRealmMetadataScope.Msd"/> | ChartSkillInfo | ChartDan.
        /// </summary>
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.DATA_REBUILD_TARGET_REALM_CHART_SKILL_CHAIN))]
        RealmChartSkillChain = 9,
    }
}
