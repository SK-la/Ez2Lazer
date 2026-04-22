// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// BMS clear-lamp tier. Models the canonical beatoraja <c>ClearType</c> spectrum,
    /// from "never played" up through "perfect / max", so existing BMS players see a
    /// familiar progression and so we can layer extra tiers in the future without
    /// breaking the ordering.
    /// </summary>
    /// <remarks>
    /// The integer values mirror beatoraja's <c>ClearType</c> ids (see
    /// <c>src/bms/player/beatoraja/ClearType.java</c>) on purpose. Keeping the IDs
    /// aligned lets us import/export lamps from beatoraja-format score data verbatim
    /// later on. Higher ordinal == better lamp; UI code can rely on that for sorting
    /// and "best lamp so far" comparisons.
    /// </remarks>
    public enum BmsClearLamp
    {
        /// <summary>The chart has never been played by the local user.</summary>
        NoPlay = 0,

        /// <summary>The player failed the chart (health hit zero on a non-assist gauge).</summary>
        Failed = 1,

        /// <summary>Cleared on a heavy-assist gauge (e.g. AEASY) - not a "real" clear.</summary>
        AssistEasy = 2,

        /// <summary>Cleared on a lighter assist gauge.</summary>
        LightAssistEasy = 3,

        /// <summary>Cleared on the Easy gauge.</summary>
        Easy = 4,

        /// <summary>Cleared on the Groove / Normal gauge.</summary>
        Normal = 5,

        /// <summary>Cleared on the Hard gauge (no instant-death miss allowed past a threshold).</summary>
        Hard = 6,

        /// <summary>Cleared on the EX-Hard gauge (more punishing version of Hard).</summary>
        ExHard = 7,

        /// <summary>Full Combo - no miss across the whole chart.</summary>
        FullCombo = 8,

        /// <summary>Perfect - every judgement was at the highest tier (typically PG).</summary>
        Perfect = 9,

        /// <summary>Max - perfect score with the strictest possible judgement window.</summary>
        Max = 10,
    }
}
