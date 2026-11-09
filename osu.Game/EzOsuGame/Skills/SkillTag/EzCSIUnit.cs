// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
            if (beatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID)
                return true;

            // An unrateable Mina settles the whole chain, so the stub form is what belongs here. A complete row in
            // that state was derived from the stub axis by an earlier pass and is replaced.
            if (isSettledUnrateableMina(beatmapInfo.Hash))
            {
                if (!skillStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var settled) || settled == null || !settled.IsUnavailable)
                {
                    Logger.Log($"[EzSkills] CSI not derived for {beatmapInfo}: Mina settled as unrateable; settling as unavailable.",
                        Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                    skillStore.WriteChartSkillInfoUnavailable(beatmapInfo.Hash, beatmapInfo.ID);
                    liveCache.InvalidateSubject(beatmapInfo.Hash);
                }

                return true;
            }

            // Complete row, or a stub a previous backfill already settled.
            if (skillStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var stored) && stored != null)
            {
                liveCache.InvalidateSubject(beatmapInfo.Hash);
                return true;
            }

            IBeatmap map;

            try
            {
                map = EzPlayableBeatmapCache.GetShared(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                // Content-addressed: the file backing this hash will not become loadable on its own, so settle it
                // rather than re-converting it on every backfill. Re-importing produces a new hash, and a full
                // rebuild clears the stub.
                Logger.Log($"[EzSkills] CSI backfill cannot load {beatmapInfo} ({e.Message}); settling as unavailable.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                skillStore.WriteChartSkillInfoUnavailable(beatmapInfo.Hash, beatmapInfo.ID);
                liveCache.InvalidateSubject(beatmapInfo.Hash);
                return true;
            }

            try
            {
                var input = EzCSIEngine.FromPlayable(map);
                // Motion / pattern shares are rate-invariant; always measure at 1.0x like hub.
                var axes = EzCSIEngine.ComputeAxes(input, out var info);
                skillStore.UpsertChartSkillInfo(beatmapInfo.Hash, info, beatmapInfo.ID);
                // The typed row carries the tags; the axis table is what a reader draws, so both are written here,
                // by the one pass that measured them.
                skillStore.WriteBeatmapSystemSkills(
                    beatmapInfo.Hash,
                    EzAnalysisSystem.CSI,
                    axes,
                    beatmapInfo.ID,
                    rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);

                sessionMisses.TryRemove(beatmapInfo.Hash, out _);
                liveCache.InvalidateSubject(beatmapInfo.Hash);
                return true;
            }
            catch (Exception e)
            {
                // Analysis-side failure: leave it unstamped so the next backfill retries it.
                Logger.Log($"[EzSkills] CSI backfill analysis failed for {beatmapInfo} ({e.Message}); will retry.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                recordSessionMiss(beatmapInfo.Hash);
                return false;
            }
        }

        private bool isSettledUnrateableMina(string beatmapHash)
            => skillStore.GetBeatmapSystemState(beatmapHash, EzAnalysisSystem.Mina) == EzSkillRowState.Unrateable;

        private void recordSessionMiss(string beatmapHash)
        {
            // Several analysis workers settle charts at once, so this is written concurrently.
            if (sessionMisses.Count >= 1024)
                sessionMisses.Clear();

            sessionMisses.TryAdd(beatmapHash, 0);
        }
    }
}
