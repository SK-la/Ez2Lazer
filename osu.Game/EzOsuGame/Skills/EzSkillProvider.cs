// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Unified read API for skill metrics (song select, local profile Track, …).
    /// Covers beatmap MSD, player SSR, player dan estimates, and chart-dan verdicts.
    /// Writers (computers / aggregators) are separate DI services — see <see cref="EzSkillSystems"/>.
    /// </summary>
    public sealed class EzSkillProvider
    {
        private readonly EzSkillStore store;
        private readonly EzChartDanEstimator? chartDanEstimator;
        private readonly EzLocalProfileStore? localProfileStore;
        private readonly EzAnalysisDatabase? analysisDatabase;

        public EzSkillProvider(
            EzSkillStore store,
            EzSkillRegistry? registry = null,
            EzChartDanEstimator? chartDanEstimator = null,
            EzLocalProfileStore? localProfileStore = null,
            EzAnalysisDatabase? analysisDatabase = null)
        {
            this.store = store;
            this.chartDanEstimator = chartDanEstimator;
            this.localProfileStore = localProfileStore;
            this.analysisDatabase = analysisDatabase;
            Registry = registry ?? new EzSkillRegistry();
        }

        public EzSkillRegistry Registry { get; }

        public IReadOnlyDictionary<string, double> GetBeatmapMsd(string beatmapHash)
            => store.GetBeatmapSkills(beatmapHash, EzSkillSystems.BEATMAP_MSD);

        /// <summary>
        /// Chart dan from persisted MSD (+ Realm xxy when present). No MSD compute / no beatmap note load.
        /// Hold ratio prefers stored analysis mania summary when available.
        /// </summary>
        public EzChartDanVerdict? TryGetCachedChartDan(BeatmapInfo beatmapInfo)
        {
            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            var msd = GetBeatmapMsd(beatmapInfo.Hash);
            if (msd.Count == 0)
                return null;

            int keyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            if (keyCount <= 0)
                keyCount = 4;

            double holdRatio = 0;

            if (analysisDatabase != null
                && analysisDatabase.TryGetStoredSqliteSlice(beatmapInfo, beatmapInfo.Ruleset, out var stored)
                && stored.ManiaSummary is EzManiaSummary mania
                && EzChartDanEstimator.TryHoldRatioFromManiaSummary(mania) is double ratio)
            {
                holdRatio = ratio;
            }
            else if (msd.TryGetValue(EzSkillSystems.MsdHoldRatioSkillId, out double cachedHold) && double.IsFinite(cachedHold))
            {
                holdRatio = Math.Clamp(cachedHold, 0, 1);
            }

            double? xxySr = beatmapInfo.XxyStarRating >= 0 ? beatmapInfo.XxyStarRating : null;
            return EzChartDanEstimator.FromMsd(msd, keyCount, holdRatio, xxySr);
        }

        public bool TryGetBeatmapMsdSkill(string beatmapHash, string axisId, out double value)
        {
            string skillId = EzMinaSkillAxisExtensions.TryParse(axisId, out var axis)
                ? axis.ToMsdSkillId()
                : axisId;
            return store.TryGetBeatmapSkill(beatmapHash, skillId, out value);
        }

        public IReadOnlyDictionary<string, double> GetPlayerSsr(string username, int keyCount)
            => GetPlayerSsrSnapshot(username, keyCount).Values;

        public EzPlayerSsrSnapshot GetPlayerSsrSnapshot(string username, int keyCount)
        {
            var snapshot = store.GetPlayerSsrSnapshot(username, keyCount);
            if (snapshot.Values.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return snapshot;

            return store.GetPlayerSsrSnapshot(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount);
        }

        public IReadOnlyList<int> GetPlayerSsrKeyCounts(string username)
        {
            var keys = store.GetPlayerSsrKeyCounts(username);
            if (keys.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return keys;

            return store.GetPlayerSsrKeyCounts(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME);
        }

        public IReadOnlyList<EzPlayerSkillHistoryPoint> GetPlayerSkillHistory(string username, int keyCount, string skillId, int maxPoints = 64)
        {
            var history = store.GetPlayerSkillHistory(username, keyCount, skillId, maxPoints);
            if (history.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username))
                return history;

            return store.GetPlayerSkillHistory(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME, keyCount, skillId, maxPoints);
        }

        public EzDanEstimate? GetDan(string username, int keyCount, string side)
            => store.GetDanEstimate(username, keyCount, side);

        /// <summary>
        /// Chart-side dan for song select me-vs-chart. Null when estimator unset or chart cannot be rated.
        /// </summary>
        public EzChartDanVerdict? TryGetChartDan(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
            => chartDanEstimator?.TryEstimate(beatmapInfo, mods);

        /// <summary>Dan clear evidence (sqlite). Empty when local-profile store unset.</summary>
        public IReadOnlyList<EzDanClearEvidenceRow> GetDanClears(string username, int? keyCount = null, string? side = null, int? algorithmVersion = null)
        {
            var rows = localProfileStore?.GetDanClears(username, keyCount, side, algorithmVersion) ?? [];
            if (rows.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username) || localProfileStore == null)
                return rows;

            return localProfileStore.GetDanClears(
                EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME,
                keyCount,
                side,
                algorithmVersion);
        }

        /// <summary>SSR axis play evidence (sqlite). Empty when local-profile store unset.</summary>
        public IReadOnlyList<EzAxisPlayEvidenceRow> GetAxisPlays(string username, int? keyCount = null, string? skillId = null, int? algorithmVersion = null)
        {
            var rows = localProfileStore?.GetAxisPlays(username, keyCount, skillId, algorithmVersion) ?? [];
            if (rows.Count > 0 || !EzLocalProfileConstants.IsGuestUsername(username) || localProfileStore == null)
                return rows;

            return localProfileStore.GetAxisPlays(
                EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME,
                keyCount,
                skillId,
                algorithmVersion);
        }
    }
}
