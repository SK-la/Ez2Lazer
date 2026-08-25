// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// DI façade for the single shared local profile store and aggregation.
    /// </summary>
    public class EzLocalProfileService : IDisposable
    {
        private readonly EzLocalProfileStore store;
        private readonly EzLocalProfileAggregator aggregator;
        private readonly object computeLock = new object();
        private CancellationTokenSource? computeCts;

        public Bindable<EzLocalProfileSnapshot> Snapshot { get; } = new Bindable<EzLocalProfileSnapshot>(new EzLocalProfileSnapshot());

        public BindableBool IsComputing { get; } = new BindableBool();

        public EzLocalProfileService(Storage storage, RealmAccess realm, EzAnalysisPersistentStore analysisStore)
        {
            store = new EzLocalProfileStore(storage);
            aggregator = new EzLocalProfileAggregator(realm, analysisStore);
            Snapshot.Value = store.LoadSnapshot();
        }

        public IReadOnlyList<EzLocalProfileUsernameCount> ScanUsernameCounts() => aggregator.ScanUsernameCounts();

        public IReadOnlyList<string> GetPreviouslyIncludedUsernames() => store.LoadIncludedUsernames();

        public bool HasOnlineScoreContributions() => store.LoadOnlineScoreContributions().Count > 0;

        public Task ComputeAsync(IReadOnlyCollection<string> includedUsernames, CancellationToken cancellationToken = default)
        {
            lock (computeLock)
            {
                computeCts?.Cancel();
                computeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = computeCts.Token;

                IsComputing.Value = true;

                return Task.Run(() =>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        var result = aggregator.Aggregate(includedUsernames, store.LoadOnlineScoreContributions());
                        token.ThrowIfCancellationRequested();
                        store.ReplaceAll(result);
                    }
                    catch (OperationCanceledException)
                    {
                        // ignored
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "[EzLocalProfile] Failed to compute local profile statistics.", Ez2ConfigManager.LOGGER_NAME);
                        throw;
                    }
                    finally
                    {
                        IsComputing.Value = false;
                    }
                }, token);
            }
        }

        public void ReloadFromDisk()
        {
            Snapshot.Value = store.LoadSnapshot();
        }

        public void Dispose()
        {
            computeCts?.Cancel();
            computeCts?.Dispose();
            store.Dispose();
        }
    }
}
