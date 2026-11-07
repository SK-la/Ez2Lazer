// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Database;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Turns a maintenance action into the background work that performs it. Which store an action touches is
    /// declared by the catalog, so this class only maps an action onto the queue that owns it.
    /// </summary>
    public class EzDataRebuildDispatcher
    {
        private readonly Func<EzRealmMetadataScope, bool, EzDataRebuildDispatchResult>? queueRealm;
        private readonly Func<bool, EzDataRebuildDispatchResult>? queueSqliteMain;
        private readonly Func<bool, EzDataRebuildDispatchResult>? queueSqliteSongsBranches;
        private readonly Func<bool, EzDataRebuildDispatchResult>? queueRealmScores;

        public EzDataRebuildDispatcher(
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzAnalysisWarmupProcessor? warmupProcessor)
            : this(
                backgroundDataStoreProcessor == null ? null : backgroundDataStoreProcessor.QueueEzRealmMetadataRebuild,
                warmupProcessor == null ? null : warmupProcessor.QueueSqliteMainRebuild,
                warmupProcessor == null ? null : warmupProcessor.QueueSqliteSongsBranchesRebuild,
                backgroundDataStoreProcessor == null ? null : backgroundDataStoreProcessor.QueueEzScoreFullRecalculation)
        { }

        internal EzDataRebuildDispatcher(
            Func<EzRealmMetadataScope, bool, EzDataRebuildDispatchResult>? queueRealm,
            Func<bool, EzDataRebuildDispatchResult>? queueSqliteMain,
            Func<bool, EzDataRebuildDispatchResult>? queueSqliteSongsBranches,
            Func<bool, EzDataRebuildDispatchResult>? queueRealmScores = null)
        {
            this.queueRealm = queueRealm;
            this.queueSqliteMain = queueSqliteMain;
            this.queueSqliteSongsBranches = queueSqliteSongsBranches;
            this.queueRealmScores = queueRealmScores;
        }

        public bool CanDispatch(EzDataRebuildAction action)
        {
            if (EzDataRebuildScopes.RealmScopeOf(action) != null)
                return queueRealm != null;

            switch (action)
            {
                case EzDataRebuildAction.RealmScores:
                    return queueRealmScores != null;

                case EzDataRebuildAction.SqliteMain:
                    return queueSqliteMain != null;

                case EzDataRebuildAction.SqliteSongsBranches:
                    return queueSqliteSongsBranches != null;

                default:
                    return false;
            }
        }

        public EzDataRebuildDispatchResult Execute(EzDataRebuildAction action, bool forceAll)
            => Execute(new[] { action }, forceAll).FirstFailure;

        /// <summary>
        /// Dispatches a whole selection. Actions owned by the same queue are merged into one request: the Realm
        /// metadata queue accepts a scope union, and asking it twice would report the second call as already
        /// running even though it actually deferred the work.
        /// </summary>
        public EzDataRebuildDispatchOutcome Execute(IReadOnlyList<EzDataRebuildAction> actions, bool forceAll)
        {
            var queued = new List<EzDataRebuildAction>(actions.Count);
            var failures = new List<EzDataRebuildDispatchFailure>();

            var realmGroup = new List<EzDataRebuildAction>();
            var scoreGroup = new List<EzDataRebuildAction>();
            var sqliteMainGroup = new List<EzDataRebuildAction>();
            var songsBranchesGroup = new List<EzDataRebuildAction>();
            var unknownGroup = new List<EzDataRebuildAction>();

            EzRealmMetadataScope realmScope = default;

            foreach (var action in actions)
            {
                if (EzDataRebuildScopes.RealmScopeOf(action) is { } scope)
                {
                    realmGroup.Add(action);
                    realmScope |= scope;
                    continue;
                }

                switch (action)
                {
                    case EzDataRebuildAction.RealmScores:
                        scoreGroup.Add(action);
                        break;

                    case EzDataRebuildAction.SqliteMain:
                        sqliteMainGroup.Add(action);
                        break;

                    case EzDataRebuildAction.SqliteSongsBranches:
                        songsBranchesGroup.Add(action);
                        break;

                    default:
                        unknownGroup.Add(action);
                        break;
                }
            }

            void Resolve(List<EzDataRebuildAction> group, EzDataRebuildDispatchResult result)
            {
                foreach (var action in group)
                {
                    if (result == EzDataRebuildDispatchResult.Queued)
                        queued.Add(action);
                    else
                        failures.Add(new EzDataRebuildDispatchFailure(action, result));
                }
            }

            if (realmGroup.Count > 0)
                Resolve(realmGroup, dispatchRealm(realmScope, forceAll));

            if (scoreGroup.Count > 0)
                Resolve(scoreGroup, dispatchRealmScores(forceAll));

            if (sqliteMainGroup.Count > 0)
                Resolve(sqliteMainGroup, dispatchSqliteMain(forceAll));

            if (songsBranchesGroup.Count > 0)
                Resolve(songsBranchesGroup, dispatchSqliteSongsBranches(forceAll));

            foreach (var action in unknownGroup)
            {
                Logger.Log($"Unknown data rebuild action: {action}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                failures.Add(new EzDataRebuildDispatchFailure(action, EzDataRebuildDispatchResult.UnknownTarget));
            }

            return new EzDataRebuildDispatchOutcome(queued, failures);
        }

        private EzDataRebuildDispatchResult dispatchRealm(EzRealmMetadataScope scope, bool forceAll)
        {
            if (queueRealm == null)
            {
                Logger.Log($"Cannot queue Realm metadata rebuild ({scope}): background data store processor is unavailable.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return EzDataRebuildDispatchResult.UnavailableProcessor;
            }

            return queueRealm(scope, forceAll);
        }

        private EzDataRebuildDispatchResult dispatchRealmScores(bool forceAll)
        {
            if (queueRealmScores == null)
            {
                Logger.Log("Cannot queue full score recalculation: background data store processor is unavailable.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return EzDataRebuildDispatchResult.UnavailableProcessor;
            }

            return queueRealmScores(forceAll);
        }

        private EzDataRebuildDispatchResult dispatchSqliteMain(bool forceAll)
        {
            if (queueSqliteMain == null)
            {
                Logger.Log("Cannot queue SQLite main rebuild: analysis warmup processor is unavailable.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return EzDataRebuildDispatchResult.UnavailableProcessor;
            }

            return queueSqliteMain(forceAll);
        }

        private EzDataRebuildDispatchResult dispatchSqliteSongsBranches(bool forceAll)
        {
            if (queueSqliteSongsBranches == null)
            {
                Logger.Log("Cannot queue SQLite songs branch rebuild: analysis warmup processor is unavailable.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return EzDataRebuildDispatchResult.UnavailableProcessor;
            }

            return queueSqliteSongsBranches(forceAll);
        }
    }
}
