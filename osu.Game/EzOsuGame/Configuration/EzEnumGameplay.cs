// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

// ReSharper disable InconsistentNaming

namespace osu.Game.EzOsuGame.Configuration
{
    public enum ColumnWidthStyle
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.EZ_STYLE_PRO_ONLY))]
        EzSkinOnly = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.GLOBAL_WIDTH))]
        GlobalWidth = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.GLOBAL_TOTAL_WIDTH))]
        GlobalTotalWidth = 2,
    }

    public enum EzEnumJudgeMode
    {
        [Description("Lazer")]
        Lazer = 0,

        /// <summary>
        /// 全规则集共用的 stable 经典判定档。
        /// </summary>
        [Description("Stable Classic")]
        StableClassic = 1,

        // 以下为 Mania 私有判定档。编码为「规则集 OnlineID + 序号」：Mania = 3 → 31..39。
        // 其他规则集若要加私有档，取自己的 ID 段（如 4x），与共享低段（0 Lazer / 1 StableClassic）隔离。

        [Description("EZ2AC: FNEX")]
        EZ2AC = 31,

        [Description("O2JAM")]
        O2Jam = 32,

        [Description("IIDX")]
        IIDX_HD = 33,

        [Description("LR2 Hard")]
        LR2_HD = 34,

        [Description("Raja Normal")]
        Raja_NM = 35,

        [Description("Malody E Judge")]
        Malody_E = 36,

        [Description("Malody B Judge")]
        Malody_B = 37,

        [Description("")]
        Classic = 38,
    }

    public enum EzEnumHealthMode
    {
        [Description("Lazer")]
        Lazer = 0,

        /// <summary>
        /// 全规则集共用的 stable 经典血量档。
        /// </summary>
        [Description("Stable Classic")]
        StableClassic = 1,

        // 以下为 Mania 私有血量档，编码同判定档（Mania = 3 → 31..39）。

        [Description("O2Jam Easy")]
        O2JamEasy = 31,

        [Description("O2Jam Normal")]
        O2JamNormal = 32,

        [Description("O2Jam Hard")]
        O2JamHard = 33,

        [Description("")]
        Ez2Ac = 34,

        [Description("IIDX Hard")]
        IIDX_HD = 35,

        [Description("LR2 Hard")]
        LR2_HD = 36,

        [Description("raja Hard")]
        Raja_HD = 37,
    }

    public enum EzEnumJudgePrecedence
    {
        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.JUDGE_PRECEDENCE_COMBO))]
        Combo = 0,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.JUDGE_PRECEDENCE_DURATION))]
        Duration = 1,

        [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.JUDGE_PRECEDENCE_EARLIEST))]
        Earliest = 2,

        // [LocalisableDescription(typeof(EzEnumStrings), nameof(EzEnumStrings.JUDGE_PRECEDENCE_SCORE))]
        // Score = 3,
    }
}
