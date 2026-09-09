// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Realm persistence for <see cref="EzChartSkillInfo"/> keyed by beatmap hash (EZ≥9).
    /// Optionally imports orphaned analysis SQLite <c>chart_skill_info</c> rows on cache miss, then stops writing SQLite.
    /// </summary>
    public sealed class EzChartSkillInfoStore
    {
        private const char pattern_separator = '\u001f';

        private static readonly JsonSerializerOptions json_options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly RealmAccess realmAccess;
        private readonly EzAnalysisPersistentStore? legacyAnalysisStore;

        public EzChartSkillInfoStore(RealmAccess realmAccess, EzAnalysisPersistentStore? legacyAnalysisStore = null)
        {
            this.realmAccess = realmAccess;
            this.legacyAnalysisStore = legacyAnalysisStore;
        }

        public bool TryGet(string beatmapHash, out EzChartSkillInfo? info)
        {
            info = null;

            if (string.IsNullOrEmpty(beatmapHash))
                return false;

            EzChartSkillInfo? fromRealm = null;

            realmAccess.Run(r =>
            {
                var row = r.All<EzBeatmapChartSkillInfo>()
                           .FirstOrDefault(v => v.BeatmapHash == beatmapHash
                                                && v.InfoVersion == EzChartSkillInfo.VERSION);
                if (row != null)
                    fromRealm = ToDto(row);
            });

            if (fromRealm != null)
            {
                info = fromRealm;
                return true;
            }

            if (legacyAnalysisStore != null
                && legacyAnalysisStore.TryGetChartSkillInfo(beatmapHash, out var legacy)
                && legacy != null)
            {
                Upsert(beatmapHash, legacy);
                info = legacy;
                return true;
            }

            return false;
        }

        public void Upsert(string beatmapHash, EzChartSkillInfo info, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(beatmapHash);
            ArgumentNullException.ThrowIfNull(info);

            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapChartSkillInfo>()
                                .Where(v => v.BeatmapHash == beatmapHash)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzBeatmapChartSkillInfo
                {
                    BeatmapHash = beatmapHash,
                    BeatmapId = beatmapId,
                    InfoVersion = EzChartSkillInfo.VERSION,
                    ComputedAt = at,
                    PatternTagsJoined = JoinPatterns(info.Patterns),
                    JackDemand = info.JackDemand,
                    JackShare = info.JackShare ?? -1,
                    StreamShare = info.StreamShare ?? -1,
                    TechCategory = info.TechCategory,
                    ClusterTrill = info.ClusterTrill,
                    HandstreamCluster = info.HandstreamCluster,
                    HandstreamEndurance = info.HandstreamEndurance,
                    TechScore = info.TechScore,
                    ChordjackScore = info.ChordjackScore,
                    JackScore = info.JackScore ?? -1,
                    MotionRhythmBreak = info.Motion?.RhythmBreak ?? -1,
                    MotionCrossHandTrill = info.Motion?.CrossHandTrill ?? -1,
                    MotionMiniJack = info.Motion?.MiniJack ?? -1,
                    MotionSameHand = info.Motion?.SameHand ?? -1,
                    LnRatio = info.LnRatio ?? -1,
                    Vibro = info.Vibro,
                    DanEligible = info.DanEligible,
                    LengthSeconds = info.LengthSeconds ?? -1,
                    KeyCount = info.KeyCount ?? -1,
                });
            });
        }

        public static EzChartSkillInfo ToDto(EzBeatmapChartSkillInfo row)
        {
            ArgumentNullException.ThrowIfNull(row);

            EzMotionFeatures? motion = null;

            if (row.MotionRhythmBreak >= 0
                || row.MotionCrossHandTrill >= 0
                || row.MotionMiniJack >= 0
                || row.MotionSameHand >= 0)
            {
                motion = new EzMotionFeatures
                {
                    RhythmBreak = row.MotionRhythmBreak < 0 ? 0 : row.MotionRhythmBreak,
                    CrossHandTrill = row.MotionCrossHandTrill < 0 ? 0 : row.MotionCrossHandTrill,
                    MiniJack = row.MotionMiniJack < 0 ? 0 : row.MotionMiniJack,
                    SameHand = row.MotionSameHand < 0 ? 0 : row.MotionSameHand,
                };
            }

            return new EzChartSkillInfo
            {
                Patterns = SplitPatterns(row.PatternTagsJoined),
                JackDemand = row.JackDemand,
                JackShare = optionalDouble(row.JackShare),
                StreamShare = optionalDouble(row.StreamShare),
                TechCategory = row.TechCategory,
                ClusterTrill = row.ClusterTrill,
                HandstreamCluster = row.HandstreamCluster,
                HandstreamEndurance = row.HandstreamEndurance,
                TechScore = row.TechScore,
                ChordjackScore = row.ChordjackScore,
                JackScore = optionalDouble(row.JackScore),
                Motion = motion,
                LnRatio = optionalDouble(row.LnRatio),
                Vibro = row.Vibro,
                DanEligible = row.DanEligible,
                LengthSeconds = optionalDouble(row.LengthSeconds),
                KeyCount = row.KeyCount < 0 ? null : row.KeyCount,
            };
        }

        internal static string JoinPatterns(string[]? patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return string.Empty;

            return string.Join(pattern_separator, patterns.Where(static t => !string.IsNullOrWhiteSpace(t)));
        }

        internal static string[] SplitPatterns(string? joined)
        {
            if (string.IsNullOrEmpty(joined))
                return [];

            return joined.Split(pattern_separator, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>Legacy JSON helper for importing orphaned analysis SQLite rows.</summary>
        internal static string Serialize(EzChartSkillInfo info)
            => JsonSerializer.Serialize(info, json_options);

        /// <summary>Legacy JSON helper for importing orphaned analysis SQLite rows.</summary>
        internal static bool TryDeserialize(string json, out EzChartSkillInfo? info)
        {
            try
            {
                info = JsonSerializer.Deserialize<EzChartSkillInfo>(json, json_options);
                return info != null;
            }
            catch
            {
                info = null;
                return false;
            }
        }

        private static double? optionalDouble(double value)
            => value < 0 ? null : value;
    }
}
