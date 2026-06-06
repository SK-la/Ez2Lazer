// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Database;

namespace osu.Game.EzOsuGame.Analysis
{
    public class EzDataRebuildDispatcher
    {
        private readonly BackgroundDataStoreProcessor? backgroundDataStoreProcessor;
        private readonly EzAnalysisWarmupProcessor? warmupProcessor;
        private readonly Func<EzRealmMetadataScope, bool, EzDataRebuildDispatchResult>? queueRealm;
        private readonly Func<bool, EzDataRebuildDispatchResult>? queueSqliteMain;
        private readonly Func<bool, EzDataRebuildDispatchResult>? queueSqliteSongsBranches;

        public EzDataRebuildDispatcher(
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzAnalysisWarmupProcessor? warmupProcessor)
            : this(
                backgroundDataStoreProcessor == null ? null : backgroundDataStoreProcessor.QueueEzRealmMetadataRebuild,
                warmupProcessor == null ? null : warmupProcessor.QueueSqliteMainRebuild,
                warmupProcessor == null ? null : warmupProcessor.QueueSqliteSongsBranchesRebuild)
        {
            this.backgroundDataStoreProcessor = backgroundDataStoreProcessor;
            this.warmupProcessor = warmupProcessor;
        }

        internal EzDataRebuildDispatcher(
            Func<EzRealmMetadataScope, bool, EzDataRebuildDispatchResult>? queueRealm,
            Func<bool, EzDataRebuildDispatchResult>? queueSqliteMain,
            Func<bool, EzDataRebuildDispatchResult>? queueSqliteSongsBranches)
        {
            this.queueRealm = queueRealm;
            this.queueSqliteMain = queueSqliteMain;
            this.queueSqliteSongsBranches = queueSqliteSongsBranches;
        }

        public bool CanDispatch(EzDataRebuildTarget target)
        {
            switch (target)
            {
                case EzDataRebuildTarget.RealmTags:
                case EzDataRebuildTarget.RealmXxy:
                case EzDataRebuildTarget.RealmPp:
                case EzDataRebuildTarget.RealmAll:
                    return queueRealm != null;

                case EzDataRebuildTarget.SqliteMain:
                    return queueSqliteMain != null;

                case EzDataRebuildTarget.SqliteSongsBranches:
                    return queueSqliteSongsBranches != null;

                default:
                    return false;
            }
        }

        public EzDataRebuildDispatchResult Execute(EzDataRebuildTarget target, bool forceAll)
        {
            switch (target)
            {
                case EzDataRebuildTarget.RealmTags:
                    return dispatchRealm(EzRealmMetadataScope.Tags, forceAll);

                case EzDataRebuildTarget.RealmXxy:
                    return dispatchRealm(EzRealmMetadataScope.Xxy, forceAll);

                case EzDataRebuildTarget.RealmPp:
                    return dispatchRealm(EzRealmMetadataScope.Pp, forceAll);

                case EzDataRebuildTarget.RealmAll:
                    return dispatchRealm(EzRealmMetadataScope.All, forceAll);

                case EzDataRebuildTarget.SqliteMain:
                    return dispatchSqliteMain(forceAll);

                case EzDataRebuildTarget.SqliteSongsBranches:
                    return dispatchSqliteSongsBranches(forceAll);

                default:
                    Logger.Log($"Unknown data rebuild target: {target}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                    return EzDataRebuildDispatchResult.UnknownTarget;
            }
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
