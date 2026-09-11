// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Nomod chart-dan baseline + skillset label stamps (EZ≥10). One row per beatmap hash.
    /// Rate-mod (DT) chart dan is not stored here. Missing side uses <c>RawDan = -1</c>.
    /// </summary>
    [MapTo("EzBeatmapChartDan")]
    public class EzBeatmapChartDan : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; } = Guid.NewGuid();

        [Indexed]
        public string BeatmapHash { get; set; } = string.Empty;

        [Indexed]
        public Guid BeatmapId { get; set; } = Guid.Empty;

        /// <summary><see cref="EzDanAlgorithm.VERSION"/> at compute time.</summary>
        public int AlgorithmVersion { get; set; }

        public int KeyCount { get; set; }

        public double HoldRatio { get; set; }

        public double OverallMsd { get; set; }

        /// <summary>-1 when RC half is gated off / unavailable.</summary>
        public double RcRawDan { get; set; } = -1;

        public string RcLabel { get; set; } = string.Empty;

        /// <summary>-1 when LN half is gated off / unavailable.</summary>
        public double LnRawDan { get; set; } = -1;

        public string LnLabel { get; set; } = string.Empty;

        /// <summary>
        /// Hub stamps: <c>id\u001flabel</c> entries joined with <c>\u001e</c>.
        /// </summary>
        public string RcSkillsetLabelsJoined { get; set; } = string.Empty;

        /// <summary>
        /// Hub stamps: <c>id\u001flabel</c> entries joined with <c>\u001e</c>.
        /// </summary>
        public string LnSkillsetLabelsJoined { get; set; } = string.Empty;

        public DateTimeOffset ComputedAt { get; set; }
    }
}
