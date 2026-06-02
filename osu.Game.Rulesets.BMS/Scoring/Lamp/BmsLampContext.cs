// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// All inputs a <see cref="IBmsLampScheme"/> needs to decide which lamp a play
    /// earned. Designed as a flat readonly record so call sites can build it from
    /// either a live <c>ScoreInfo</c> or from rebuilt beatoraja score rows without
    /// taking a dependency on either type.
    /// </summary>
    /// <param name="HasPlayed">
    /// Whether the local user has any play record on this chart at all. Used to
    /// distinguish <see cref="BmsClearLamp.NoPlay"/> from <see cref="BmsClearLamp.Failed"/>.
    /// </param>
    /// <param name="Cleared">
    /// Whether gameplay finished without the gauge falling to zero on a "real" gauge.
    /// Assist gauges are reported separately via <paramref name="Gauge"/>.
    /// </param>
    /// <param name="Gauge">The gauge the player used during this attempt.</param>
    /// <param name="MissCount">Number of POOR/MISS judgements. <c>0</c> qualifies for FullCombo.</param>
    /// <param name="GreatCount">Number of mid-tier judgements (BMS GREAT / mania GREAT).</param>
    /// <param name="GoodCount">Number of low-tier judgements (BMS GOOD / mania OK).</param>
    /// <param name="BadCount">Number of BAD judgements that don't count as miss.</param>
    /// <param name="PerfectGreatCount">Number of highest-tier hits (BMS PGREAT / mania PERFECT).</param>
    /// <param name="TotalNotes">Total judged note count.</param>
    /// <param name="UsedHighestJudgementWindow">
    /// True if the played hit-window profile was the strictest available (used to
    /// distinguish <see cref="BmsClearLamp.Perfect"/> from <see cref="BmsClearLamp.Max"/>).
    /// </param>
    public readonly record struct BmsLampContext(
        bool HasPlayed,
        bool Cleared,
        BmsGaugeType Gauge,
        int MissCount,
        int GreatCount,
        int GoodCount,
        int BadCount,
        int PerfectGreatCount,
        int TotalNotes,
        bool UsedHighestJudgementWindow)
    {
        /// <summary>Helper: chart had no input recorded (NoPlay).</summary>
        public static BmsLampContext NoPlay() => new BmsLampContext(
            HasPlayed: false,
            Cleared: false,
            Gauge: BmsGaugeType.Normal,
            MissCount: 0,
            GreatCount: 0,
            GoodCount: 0,
            BadCount: 0,
            PerfectGreatCount: 0,
            TotalNotes: 0,
            UsedHighestJudgementWindow: false);

        /// <summary>True if there were zero misses (FC candidate).</summary>
        public bool IsFullCombo => MissCount == 0 && TotalNotes > 0;

        /// <summary>True if every judged note was the top tier (Perfect candidate).</summary>
        public bool IsAllPerfect => TotalNotes > 0 && PerfectGreatCount == TotalNotes;
    }
}
