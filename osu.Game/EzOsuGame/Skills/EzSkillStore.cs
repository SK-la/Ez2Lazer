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

        public void WriteBeatmapMsd(string beatmapHash, EzSkillsetVector vector, DateTimeOffset? computedAt = null)
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

                foreach ((string axis, double value) in vector.Enumerate())
                {
                    r.Add(new EzBeatmapSkillValue
                    {
                        BeatmapHash = beatmapHash,
                        SystemId = EzSkillSystems.BEATMAP_MSD,
                        SkillId = EzSkillIds.Msd(axis),
                        Value = value,
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }
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

        public void WritePlayerSsr(string username, int keyCount, EzSkillsetVector vector, int analyzedPlays, DateTimeOffset? computedAt = null)
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

                foreach ((string axis, double value) in vector.Enumerate())
                {
                    r.Add(new EzPlayerSkillValue
                    {
                        Username = username,
                        KeyCount = keyCount,
                        SystemId = EzSkillSystems.PLAYER_SSR,
                        SkillId = EzSkillIds.Ssr(axis),
                        Value = value,
                        AnalyzedPlays = analyzedPlays,
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }
            });
        }

        public EzDanEstimate? GetDanEstimate(string username, int keyCount, string side)
        {
            return realmAccess.Run(r =>
            {
                var row = r.All<EzDanEstimate>()
                           .FirstOrDefault(v => v.Username == username && v.KeyCount == keyCount && v.Side == side);
                return row?.Detach();
            });
        }
    }
}
