// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Database;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Read/write helpers for skill Realm objects. Does not open the game for the caller.
    /// </summary>
    public sealed class EzSkillStore
    {
        private readonly RealmAccess realmAccess;

        public EzSkillStore(RealmAccess realmAccess)
        {
            this.realmAccess = realmAccess;
        }

        public IReadOnlyDictionary<string, double> GetBeatmapSkills(string beatmapHash, string systemId, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.BeatmapHash == beatmapHash
                                        && v.SystemId == systemId
                                        && v.AlgorithmVersion == version);

                return rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
            });
        }

        public bool TryGetBeatmapSkill(string beatmapHash, string skillId, out double value, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;
            double found = double.NaN;

            realmAccess.Run(r =>
            {
                var row = r.All<EzBeatmapSkillValue>()
                           .FirstOrDefault(v => v.BeatmapHash == beatmapHash
                                                && v.SkillId == skillId
                                                && v.AlgorithmVersion == version);
                if (row != null)
                    found = row.Value;
            });

            if (double.IsNaN(found))
            {
                value = 0;
                return false;
            }

            value = found;
            return true;
        }

        public void WriteBeatmapMsd(string beatmapHash, EzSkillsetVector vector, Guid beatmapId = default, DateTimeOffset? computedAt = null, double? holdRatio = null)
        {
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                foreach (var (axis, value) in vector.Enumerate())
                {
                    r.Add(new EzBeatmapSkillValue
                    {
                        BeatmapHash = beatmapHash,
                        BeatmapId = beatmapId,
                        SystemId = EzSkillSystems.BEATMAP_MSD,
                        SkillId = axis.ToMsdSkillId(),
                        Value = value,
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }

                if (holdRatio is double ratio && double.IsFinite(ratio))
                {
                    r.Add(new EzBeatmapSkillValue
                    {
                        BeatmapHash = beatmapHash,
                        BeatmapId = beatmapId,
                        SystemId = EzSkillSystems.BEATMAP_MSD,
                        SkillId = EzSkillSystems.MsdHoldRatioSkillId,
                        Value = Math.Clamp(ratio, 0, 1),
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }
            });
        }

        /// <summary>
        /// Deletes persisted beatmap MSD rows. When <paramref name="hashes"/> is null, clears all MSD rows.
        /// </summary>
        public void ClearBeatmapMsd(IEnumerable<string>? hashes = null)
        {
            if (hashes == null)
            {
                realmAccess.Write(r =>
                {
                    var rows = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                .ToList();

                    foreach (var row in rows)
                        r.Remove(row);
                });
                return;
            }

            var hashList = hashes as IList<string> ?? hashes.ToList();
            if (hashList.Count == 0)
                return;

            const int batch_size = 200;

            for (int i = 0; i < hashList.Count; i += batch_size)
            {
                var batch = hashList.Skip(i).Take(batch_size).ToHashSet(StringComparer.Ordinal);

                realmAccess.Write(r =>
                {
                    var rows = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                .AsEnumerable()
                                .Where(v => batch.Contains(v.BeatmapHash))
                                .ToList();

                    foreach (var row in rows)
                        r.Remove(row);
                });
            }
        }

        /// <summary>
        /// Beatmap hashes that already have a complete current-version MSD cache (all axes + hold ratio).
        /// </summary>
        public HashSet<string> GetCompleteBeatmapMsdHashes(int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var byHash = r.All<EzBeatmapSkillValue>()
                              .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD
                                          && v.AlgorithmVersion == version)
                              .AsEnumerable()
                              .GroupBy(v => v.BeatmapHash, StringComparer.Ordinal);

                var complete = new HashSet<string>(StringComparer.Ordinal);

                foreach (var group in byHash)
                {
                    var skills = group.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
                    if (EzBeatmapMsdComputer.IsCurrentMsdCache(skills))
                        complete.Add(group.Key);
                }

                return complete;
            });
        }

        public IReadOnlyDictionary<string, double> GetPlayerSkills(string username, int keyCount, string systemId, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == systemId
                                        && v.AlgorithmVersion == version);

                return rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
            });
        }

        public EzPlayerSsrSnapshot GetPlayerSsrSnapshot(string username, int keyCount, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == EzSkillSystems.PLAYER_SSR
                                        && v.AlgorithmVersion == version)
                            .ToList();

                if (rows.Count == 0)
                    return new EzPlayerSsrSnapshot();

                var values = rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
                var meta = rows.FirstOrDefault(v => v.SkillId == EzMinaSkillAxis.Overall.ToSsrSkillId()) ?? rows[0];

                return new EzPlayerSsrSnapshot
                {
                    Values = values,
                    AnalyzedPlays = meta.AnalyzedPlays,
                    Provisional = meta.Provisional,
                    Stale = meta.Stale,
                    ComputedAt = meta.ComputedAt,
                };
            });
        }

        public IReadOnlyList<int> GetPlayerSsrKeyCounts(string username, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                return r.All<EzPlayerSkillValue>()
                        .Where(v => v.Username == username
                                    && v.SystemId == EzSkillSystems.PLAYER_SSR
                                    && v.AlgorithmVersion == version)
                        .AsEnumerable()
                        .Select(v => v.KeyCount)
                        .Distinct()
                        .OrderBy(k => k)
                        .ToList();
            });
        }

        public void WritePlayerSsr(
            string username,
            int keyCount,
            EzSkillsetVector vector,
            int analyzedPlays,
            bool provisional = false,
            bool stale = false,
            DateTimeOffset? computedAt = null,
            bool appendHistory = true)
        {
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzPlayerSkillValue>()
                                .Where(v => v.Username == username
                                            && v.KeyCount == keyCount
                                            && v.SystemId == EzSkillSystems.PLAYER_SSR)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                foreach (var (axis, value) in vector.Enumerate())
                {
                    string skillId = axis.ToSsrSkillId();

                    r.Add(new EzPlayerSkillValue
                    {
                        Username = username,
                        KeyCount = keyCount,
                        SystemId = EzSkillSystems.PLAYER_SSR,
                        SkillId = skillId,
                        Value = value,
                        AnalyzedPlays = analyzedPlays,
                        Provisional = provisional,
                        Stale = stale,
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });

                    if (appendHistory && value > 0)
                    {
                        r.Add(new EzPlayerSkillHistoryPoint
                        {
                            Username = username,
                            KeyCount = keyCount,
                            SkillId = skillId,
                            Value = value,
                            RecordedAt = at,
                            AlgorithmVersion = version,
                        });
                    }
                }
            });
        }

        public IReadOnlyList<EzPlayerSkillHistoryPoint> GetPlayerSkillHistory(
            string username,
            int keyCount,
            string skillId,
            int maxPoints = 64)
        {
            return realmAccess.Run(r =>
            {
                return r.All<EzPlayerSkillHistoryPoint>()
                        .Where(v => v.Username == username
                                    && v.KeyCount == keyCount
                                    && v.SkillId == skillId)
                        .ToList()
                        .OrderByDescending(v => v.RecordedAt)
                        .Take(maxPoints)
                        .Select(v => v.Detach())
                        .Reverse()
                        .ToList();
            });
        }

        public EzDanEstimate? GetDanEstimate(string username, int keyCount, string side, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var row = r.All<EzDanEstimate>()
                           .FirstOrDefault(v => v.Username == username
                                                && v.KeyCount == keyCount
                                                && v.Side == side
                                                && v.AlgorithmVersion == version);
                return row?.Detach();
            });
        }

        public void WriteDanEstimate(EzDanEstimate estimate)
        {
            ArgumentNullException.ThrowIfNull(estimate);

            realmAccess.Write(r =>
            {
                var existing = r.All<EzDanEstimate>()
                                .Where(v => v.Username == estimate.Username
                                            && v.KeyCount == estimate.KeyCount
                                            && v.Side == estimate.Side)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzDanEstimate
                {
                    Username = estimate.Username,
                    KeyCount = estimate.KeyCount,
                    Side = estimate.Side,
                    RawDan = estimate.RawDan,
                    Label = estimate.Label,
                    Clears = estimate.Clears,
                    BeyondTable = estimate.BeyondTable,
                    CourseName = estimate.CourseName,
                    CourseAccuracy = estimate.CourseAccuracy,
                    ClearWindowHave = estimate.ClearWindowHave,
                    ClearWindowNeed = estimate.ClearWindowNeed,
                    AlgorithmVersion = estimate.AlgorithmVersion != 0 ? estimate.AlgorithmVersion : EzDanAlgorithm.VERSION,
                    ComputedAt = estimate.ComputedAt == default ? DateTimeOffset.UtcNow : estimate.ComputedAt,
                });
            });
        }
    }
}
