// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// The gameplay gauge a BMS score was played on. Maps to the gauge type used
    /// by the lamp scheme to decide which lamp tier the score qualifies for.
    /// </summary>
    /// <remarks>
    /// Ordering matches beatoraja's gauge slots (see <c>ClearType.gaugetype</c> entries),
    /// so external lamp data referenced by gauge id keeps working without translation.
    /// </remarks>
    public enum BmsGaugeType
    {
        /// <summary>Light assist gauge (LIGHT ASSIST EASY).</summary>
        LightAssistEasy = 0,

        /// <summary>Easy gauge.</summary>
        Easy = 1,

        /// <summary>Groove / Normal gauge.</summary>
        Normal = 2,

        /// <summary>Hard gauge.</summary>
        Hard = 3,

        /// <summary>EX-Hard gauge.</summary>
        ExHard = 4,

        /// <summary>Hazard / Full Combo gauge.</summary>
        FullCombo = 5,

        /// <summary>Beatoraja-style "P" gauge (Normal variant).</summary>
        PNormal = 6,

        /// <summary>Beatoraja-style "P" gauge (Hard variant).</summary>
        PHard = 7,

        /// <summary>Beatoraja-style "P" gauge (EX-Hard variant).</summary>
        PExHard = 8,

        /// <summary>Assist gauge (full assist).</summary>
        Assist = 9,
    }
}
