// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Unified read API for consumers (song select, local profile Track, …).
    /// </summary>
    public sealed class EzSkillProvider
    {
        private readonly EzSkillStore store;

        public EzSkillProvider(EzSkillStore store, EzSkillRegistry? registry = null)
        {
            this.store = store;
            Registry = registry ?? new EzSkillRegistry();
        }

        public EzSkillRegistry Registry { get; }

        public IReadOnlyDictionary<string, double> GetBeatmapMsd(string beatmapHash)
            => store.GetBeatmapSkills(beatmapHash, EzSkillSystems.BEATMAP_MSD);

        public bool TryGetBeatmapMsdSkill(string beatmapHash, string axisId, out double value)
            => store.TryGetBeatmapSkill(beatmapHash, EzSkillIds.Msd(axisId), out value);

        public IReadOnlyDictionary<string, double> GetPlayerSsr(string username, int keyCount)
            => store.GetPlayerSkills(username, keyCount, EzSkillSystems.PLAYER_SSR);

        public IReadOnlyList<int> GetPlayerSsrKeyCounts(string username)
            => store.GetPlayerSsrKeyCounts(username);

        public IReadOnlyList<EzPlayerSkillHistoryPoint> GetPlayerSkillHistory(string username, int keyCount, string skillId, int maxPoints = 64)
            => store.GetPlayerSkillHistory(username, keyCount, skillId, maxPoints);

        public EzDanEstimate? GetDan(string username, int keyCount, string side)
            => store.GetDanEstimate(username, keyCount, side);
    }
}
