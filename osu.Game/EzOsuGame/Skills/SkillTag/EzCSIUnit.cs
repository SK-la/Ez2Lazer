// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// Runs the structure (CSI) system for one chart: gate, shared conversion, engine, store.
    /// </summary>
    /// <remarks>
    /// Write path only. A read path never persists: durable rows are owned by the backfill, which is the only
    /// caller that can stamp a settled stub, so a transient miss from song select cannot retire a chart.
    /// </remarks>
    public sealed class EzCSIUnit
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartSkillStore skillStore;
        private readonly EzAnalysisLiveCache liveCache;

        /// <summary>Session-only compute failures - keep the miss out of Realm so the next backfill retries.</summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> sessionMisses =
            new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        public EzCSIUnit(BeatmapManager beatmapManager, EzChartSkillStore skillStore, EzAnalysisLiveCache? liveCache = null)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.liveCache = liveCache ?? new EzAnalysisLiveCache();
        }

        /// <summary>
        /// BDSP-facing ensure for one chart. Returns true when the hash is settled afterwards (a current row, a fresh
        /// compute, or a stub because the chart cannot be loaded or its Mina settled as unrateable), false when the
        /// failure is transient and should be retried on a later run.
        /// </summary>
        public bool TryEnsure(BeatmapInfo beatmapInfo)
        {
            var section = TryCompute(beatmapInfo);

            if (section == null)
                return false;

            switch (section)
            {
                case EzCsiMaintenanceSection.Computed:
                case EzCsiMaintenanceSection.Unavailable:
                    skillStore.ApplyCsiSection(beatmapInfo.Hash, beatmapInfo.ID, section);
                    liveCache.InvalidateSubject(beatmapInfo.Hash);
                    sessionMisses.TryRemove(beatmapInfo.Hash, out _);
                    return true;

                case EzCsiMaintenanceSection.Skipped:
                    liveCache.InvalidateSubject(beatmapInfo.Hash);
                    return true;

                case EzCsiMaintenanceSection.TransientFailure:
                    recordSessionMiss(beatmapInfo.Hash);
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// CPU-only half of CSI maintenance. Uses in-memory Mina when supplied so same-iteration chain stages need no Realm read.
        /// </summary>
        /// <param name="chartChainPass">When true, skips Realm settled-row reads; the writer applies DTO-only stubs.</param>
        public EzCsiMaintenanceSection? TryCompute(BeatmapInfo beatmapInfo, EzMinaMaintenanceSection? inMemoryMina = null, bool chartChainPass = false)
        {
            if (beatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID)
                return new EzCsiMaintenanceSection.Skipped();

            if (isUnrateableMina(beatmapInfo.Hash, inMemoryMina, chartChainPass))
            {
                if (!chartChainPass)
                {
                    if (!skillStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var settled) || settled == null || !settled.IsUnavailable)
                    {
                        Logger.Log($"[EzSkills] CSI not derived for {beatmapInfo}: Mina settled as unrateable; settling as unavailable.",
                            Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                    }
                }

                return new EzCsiMaintenanceSection.Unavailable();
            }

            if (!chartChainPass && skillStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) && stored != null)
                return new EzCsiMaintenanceSection.Skipped();

            IBeatmap map;

            try
            {
                map = EzPlayableBeatmapCache.GetShared(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                Logger.Log($"[EzSkills] CSI backfill cannot load {beatmapInfo} ({e.Message}); settling as unavailable.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return new EzCsiMaintenanceSection.Unavailable();
            }

            try
            {
                var input = EzCSIEngine.FromPlayable(map);
                var axes = EzCSIEngine.ComputeAxes(input, out var info);
                return new EzCsiMaintenanceSection.Computed(info, axes);
            }
            catch (Exception e)
            {
                Logger.Log($"[EzSkills] CSI backfill analysis failed for {beatmapInfo} ({e.Message}); will retry.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return new EzCsiMaintenanceSection.TransientFailure();
            }
        }

        /// <summary>Invalidates live cache subjects once per hash after a maintenance batch.</summary>
        public void InvalidateSubjectsAfterBatch(IEnumerable<string> beatmapHashes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string hash in beatmapHashes)
            {
                if (string.IsNullOrEmpty(hash) || !seen.Add(hash))
                    continue;

                liveCache.InvalidateSubject(hash);
            }
        }

        /// <summary>Records a transient CSI miss after the maintenance writer committed (chart-chain batch).</summary>
        public void RecordSessionMissAfterCommit(string beatmapHash) => recordSessionMiss(beatmapHash);

        private bool isUnrateableMina(string beatmapHash, EzMinaMaintenanceSection? inMemoryMina, bool chartChainPass)
        {
            switch (inMemoryMina)
            {
                case EzMinaMaintenanceSection.Unrateable:
                    return true;

                case EzMinaMaintenanceSection.Vector:
                    return false;

                case EzMinaMaintenanceSection.TransientFailure when chartChainPass:
                    return false;
            }

            return skillStore.GetBeatmapSystemState(beatmapHash, EzAnalysisSystem.Mina) == EzSkillRowState.Unrateable;
        }

        private void recordSessionMiss(string beatmapHash)
        {
            if (sessionMisses.Count >= 1024)
                sessionMisses.Clear();

            sessionMisses.TryAdd(beatmapHash, 0);
        }
    }
}