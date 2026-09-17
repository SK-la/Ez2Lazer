// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Plays the chart-side skill chain still owes a result for, derived on read from the drill ledger against the
    /// chain's own coverage instead of being flagged on the player's rows.
    /// <para>
    /// A play waits when its chart is one the chain will rate - the beatmap still exists, its keymode is inside the
    /// engine's range, and MSD has not settled it as unrateable - but carries no current chart-skill row. The two
    /// shapes are not equally fixable: <see cref="WaitingOnCsiByUser"/> charts have a settled, rateable MSD and only
    /// need the CSI stage to run, while <see cref="WaitingOnMsdByUser"/> charts have no settled MSD at all, so that
    /// stage never yields the row they need and they stay in debt until it succeeds.
    /// </para>
    /// <para>
    /// Deriving rather than storing means the debt cannot outlive its cause: once the chain writes the row, the play
    /// stops counting, with no clear step left to forget.
    /// </para>
    /// </summary>
    public sealed class EzChartChainDebt
    {
        /// <summary>No play waits on the chain - a library whose chain has fully caught up.</summary>
        public static readonly EzChartChainDebt EMPTY = new EzChartChainDebt(new Dictionary<string, int>(StringComparer.Ordinal), new Dictionary<string, int>(StringComparer.Ordinal));

        private readonly Dictionary<string, int> waitingOnMsd;
        private readonly Dictionary<string, int> waitingOnCsi;

        private EzChartChainDebt(Dictionary<string, int> waitingOnMsd, Dictionary<string, int> waitingOnCsi)
        {
            this.waitingOnMsd = waitingOnMsd;
            this.waitingOnCsi = waitingOnCsi;

            Usernames = waitingOnMsd.Keys.Union(waitingOnCsi.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
            TotalPlays = waitingOnMsd.Values.Sum() + waitingOnCsi.Values.Sum();
        }

        /// <summary>Waits that cannot end until MSD produces a row for the chart, per player: the keymode is rateable
        /// but MSD has settled nothing for the chart at all.</summary>
        public IReadOnlyDictionary<string, int> WaitingOnMsdByUser => waitingOnMsd;

        /// <summary>Waits the CSI stage can still answer, per player: MSD settled the chart as rateable, so only the
        /// CSI row is outstanding.</summary>
        public IReadOnlyDictionary<string, int> WaitingOnCsiByUser => waitingOnCsi;

        /// <summary>Players with at least one play waiting on the chain.</summary>
        public IReadOnlyList<string> Usernames { get; }

        /// <summary>Plays waiting in total, across every player.</summary>
        public int TotalPlays { get; }

        public bool HasDebt => TotalPlays > 0;

        /// <summary>Plays this player waits on, or 0 when the chain owes them nothing.</summary>
        public int PlaysFor(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            return (waitingOnMsd.GetValueOrDefault(username, 0))
                   + (waitingOnCsi.GetValueOrDefault(username, 0));
        }

        public static EzChartChainDebt Collect(IEnumerable<(string Username, string BeatmapHash)> plays, EzSkillStore store)
        {
            ArgumentNullException.ThrowIfNull(plays);
            ArgumentNullException.ThrowIfNull(store);

            var rateableCharts = store.GetRateableChartHashes();
            var settledCsi = store.GetSettledChartSkillInfoHashes();
            var settledMsd = store.GetSettledBeatmapMsdHashes();
            var unrateableMsd = store.GetUnrateableMsdHashes();

            var waitingOnMsd = new Dictionary<string, int>(StringComparer.Ordinal);
            var waitingOnCsi = new Dictionary<string, int>(StringComparer.Ordinal);
            var counted = new HashSet<(string Username, string BeatmapHash)>();

            foreach (var play in plays)
            {
                if (string.IsNullOrEmpty(play.Username) || string.IsNullOrEmpty(play.BeatmapHash))
                    continue;

                if (!rateableCharts.Contains(play.BeatmapHash) || settledCsi.Contains(play.BeatmapHash))
                    continue;

                // A chart the chain settled as unrateable is not waiting on anything, whatever CSI row an earlier
                // pass may have stamped for it from the stub MSD axis.
                if (unrateableMsd.Contains(play.BeatmapHash))
                    continue;

                if (!counted.Add(play))
                    continue;

                var bucket = settledMsd.Contains(play.BeatmapHash) ? waitingOnCsi : waitingOnMsd;
                bucket[play.Username] = (bucket.GetValueOrDefault(play.Username, 0)) + 1;
            }

            if (waitingOnMsd.Count == 0 && waitingOnCsi.Count == 0)
                return EMPTY;

            return new EzChartChainDebt(waitingOnMsd, waitingOnCsi);
        }
    }
}
