// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Typed Realm row for hub-aligned chart skill filing (EZ≥9).
    /// Mirrors the <see cref="EzChartSkillInfo"/> DTO; large blobs stay out of Realm.
    /// Nullable doubles use -1; unset key count uses -1; missing motion uses -1 on all motion columns.
    /// </summary>
    [MapTo("EzBeatmapChartSkillInfo")]
    public class EzBeatmapChartSkillInfo : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; } = Guid.NewGuid();

        [Indexed]
        public string BeatmapHash { get; set; } = string.Empty;

        [Indexed]
        public Guid BeatmapId { get; set; } = Guid.Empty;

        public int InfoVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }

        /// <summary>
        /// Pattern tags joined with ASCII unit separator (U+001F). Closed filing vocabulary — not a JSON blob.
        /// </summary>
        public string PatternTagsJoined { get; set; } = string.Empty;

        public bool JackDemand { get; set; }

        /// <summary>-1 when unset.</summary>
        public double JackShare { get; set; } = -1;

        /// <summary>-1 when unset.</summary>
        public double StreamShare { get; set; } = -1;

        public bool? TechCategory { get; set; }

        public bool? ClusterTrill { get; set; }

        public bool? HandstreamCluster { get; set; }

        public bool HandstreamEndurance { get; set; }

        public double TechScore { get; set; }

        public double ChordjackScore { get; set; }

        /// <summary>-1 when unset (reserved / pattern jack score).</summary>
        public double JackScore { get; set; } = -1;

        /// <summary>-1 when motion absent.</summary>
        public double MotionRhythmBreak { get; set; } = -1;

        /// <summary>-1 when motion absent.</summary>
        public double MotionCrossHandTrill { get; set; } = -1;

        /// <summary>-1 when motion absent.</summary>
        public double MotionMiniJack { get; set; } = -1;

        /// <summary>-1 when motion absent.</summary>
        public double MotionSameHand { get; set; } = -1;

        /// <summary>-1 when unset.</summary>
        public double LnRatio { get; set; } = -1;

        public bool Vibro { get; set; }

        public bool DanEligible { get; set; } = true;

        /// <summary>-1 when unset.</summary>
        public double LengthSeconds { get; set; } = -1;

        /// <summary>-1 when unset.</summary>
        public int KeyCount { get; set; } = -1;
    }
}
