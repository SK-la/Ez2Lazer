// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One read of the chart skill chain's persisted state, plus every per-stage answer derived from it: the
    /// charts the chain may rate, which charts each stage still owes a row, and which are settled without one.
    /// <para>
    /// The three backfill stages, the status readout and the player-chain debt all consume this single
    /// derivation. They previously each re-stated "does this chart still need X" against their own set of
    /// queries, so one rule - or one fix to it - had to be applied per reader and the readers could disagree
    /// (the dan stage retrying charts it could never rate is what that cost). Here the rules live once, the
    /// candidate universe is scanned once per read, and a stage that settles a chart without a row is settled
    /// for every consumer at the same time.
    /// </para>
    /// <para>
    /// Everything is derived from the rows themselves, so a verdict retires itself the moment those rows
    /// change. Nothing here is persisted, and the sets are already filtered to rateable charts.
    /// </para>
    /// </summary>
    public sealed class EzChartChainState
    {
        private readonly struct RateableChartMeta
        {
            public readonly int KeyCount;
            public readonly double? XxyStarRating;

            public RateableChartMeta(int keyCount, double? xxyStarRating)
            {
                KeyCount = keyCount;
                XxyStarRating = xxyStarRating;
            }
        }

        private EzChartChainState(
            IReadOnlyDictionary<string, Guid> rateableBeatmapIds,
            IReadOnlyList<EzDataStateFacet> facets,
            IReadOnlySet<string> settledMsd,
            IReadOnlySet<string> completeMsd,
            IReadOnlySet<string> unrateableMsd,
            IReadOnlySet<string> settledCsi,
            IReadOnlySet<string> completeCsi,
            IReadOnlySet<string> completeDan,
            IReadOnlySet<string> unresolvableDan)
        {
            RateableBeatmapIds = rateableBeatmapIds;
            Facets = facets;

            RateableCharts = rateableBeatmapIds.Keys.ToHashSet(StringComparer.Ordinal);
            SettledMsd = settledMsd;
            CompleteMsd = completeMsd;
            UnrateableMsd = unrateableMsd;
            SettledChartSkillInfo = settledCsi;
            CompleteChartSkillInfo = completeCsi;
            CompleteChartDan = completeDan;
            UnresolvableChartDan = unresolvableDan;

            MsdOwed = RateableCharts.Where(h => !settledMsd.Contains(h)).ToHashSet(StringComparer.Ordinal);

            // A CSI row an unrateable MSD invalidated came from the stub axis rather than from real axes, so it is
            // re-queued to be replaced by the stub form the chain settles with - it counts as settled, so nothing
            // else would ever revisit it.
            ChartSkillInfoOwed = settledMsd.Where(h => !settledCsi.Contains(h))
                                           .Union(completeCsi.Where(unrateableMsd.Contains), StringComparer.Ordinal)
                                           .ToHashSet(StringComparer.Ordinal);

            // Dan has no settled-miss row form, so a chart the stored inputs cannot resolve a dan for has no row
            // either: it is settled rather than owed, which is what stops the stage re-attempting and reporting the
            // same charts on every launch.
            ChartDanOwed = completeMsd.Where(h => !completeDan.Contains(h))
                                      .Where(h => !unresolvableDan.Contains(h))
                                      .ToHashSet(StringComparer.Ordinal);

            SettledChartDan = unrateableMsd.Union(unresolvableDan, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        }

        /// <summary>Mania charts the chain may rate at all (<see cref="EzChartChainCoverage.IsRateableChart"/>).</summary>
        public IReadOnlySet<string> RateableCharts { get; }

        /// <summary>Rateable charts grouped by hash to their Realm id, so a stage can load the beatmap it rates.</summary>
        public IReadOnlyDictionary<string, Guid> RateableBeatmapIds { get; }

        public int RateableChartCount => RateableBeatmapIds.Count;

        /// <summary>Charts MSD settled - with a complete axis set or as a settled unrateable.</summary>
        public IReadOnlySet<string> SettledMsd { get; }

        /// <summary>Charts with a complete current MSD axis set.</summary>
        public IReadOnlySet<string> CompleteMsd { get; }

        /// <summary>Charts MSD settled as unrateable; no later facet can produce a row for them.</summary>
        public IReadOnlySet<string> UnrateableMsd { get; }

        /// <summary>Charts with a current CSI row (a result or the unavailable stub).</summary>
        public IReadOnlySet<string> SettledChartSkillInfo { get; }

        /// <summary>Charts with a current CSI row that is a result rather than a stub.</summary>
        public IReadOnlySet<string> CompleteChartSkillInfo { get; }

        /// <summary>Charts with a current ChartDan row.</summary>
        public IReadOnlySet<string> CompleteChartDan { get; }

        /// <summary>
        /// Charts that have a complete MSD but whose stored inputs resolve to no dan at all, so the dan stage can
        /// never write a row for them.
        /// </summary>
        public IReadOnlySet<string> UnresolvableChartDan { get; }

        /// <summary>Charts the dan stage is settled for without holding a row.</summary>
        public IReadOnlySet<string> SettledChartDan { get; }

        /// <summary>Charts MSD still owes a result for - the MSD stage's missing set.</summary>
        public IReadOnlySet<string> MsdOwed { get; }

        /// <summary>Charts the CSI stage still owes a result for, heal set included.</summary>
        public IReadOnlySet<string> ChartSkillInfoOwed { get; }

        /// <summary>Charts the dan stage still owes a row for.</summary>
        public IReadOnlySet<string> ChartDanOwed { get; }

        /// <summary>
        /// The facets this state declares, in dependency order, ready for the generic
        /// <see cref="EzDataStateChecker"/>. A facet added to the chain shows up in the readout without touching it.
        /// </summary>
        public IReadOnlyList<EzDataStateFacet> Facets { get; }

        /// <summary>
        /// The completeness report for this state. Counted from the same rows the stages filter on, so
        /// "Pending &gt; 0" and "the next backfill has work" cannot disagree.
        /// </summary>
        public EzDataStateReport ToReport(DateTimeOffset measuredAt)
            => new EzDataStateReport
            {
                UniverseCount = RateableChartCount,
                Facets = Facets.Select(EzDataStateChecker.Count).ToList(),
                MeasuredAt = measuredAt,
            };

        /// <summary>
        /// Derives the whole state in one Realm read. Call inside <c>RealmAccess.Run</c>; the returned state holds
        /// no Realm references, so it outlives the transaction.
        /// </summary>
        internal static EzChartChainState Build(Realm r)
        {
            ArgumentNullException.ThrowIfNull(r);

            int msdRevision = EzAnalysisRevision.Msd;
            int csiRevision = EzAnalysisRevision.ChartSkillInfo;
            int danRevision = EzAnalysisRevision.ChartDan;

            var rateableBeatmapIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var rateableChartMeta = new Dictionary<string, RateableChartMeta>(StringComparer.Ordinal);

            foreach (var beatmap in r.All<BeatmapInfo>().Where(b => b.BeatmapSet != null).AsEnumerable())
            {
                if (beatmap.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID)
                    continue;

                if (string.IsNullOrEmpty(beatmap.Hash))
                    continue;

                if (!EzChartChainCoverage.IsRateableKeyCount((int)Math.Round(beatmap.Difficulty.CircleSize)))
                    continue;

                rateableBeatmapIds[beatmap.Hash] = beatmap.ID;

                int keyCount = (int)Math.Round(beatmap.Difficulty.CircleSize);
                double? xxySr = beatmap.XxyStarRating >= 0 ? beatmap.XxyStarRating : null;
                rateableChartMeta[beatmap.Hash] = new RateableChartMeta(keyCount, xxySr);
            }

            var msdRowsForReader = new List<EzBeatmapSkillValue>();
            var msdSkillsAtRevision = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);

            foreach (var value in r.All<EzBeatmapSkillValue>()
                                   .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                   .AsEnumerable())
            {
                if (!rateableBeatmapIds.ContainsKey(value.BeatmapHash))
                    continue;

                msdRowsForReader.Add(value);

                if (value.AlgorithmVersion != msdRevision)
                    continue;

                if (!msdSkillsAtRevision.TryGetValue(value.BeatmapHash, out var skills))
                {
                    skills = new Dictionary<string, double>(StringComparer.Ordinal);
                    msdSkillsAtRevision[value.BeatmapHash] = skills;
                }

                skills[value.SkillId] = value.Value;
            }

            var msdRows = EzRealmFacetReader.Read(
                msdRowsForReader,
                static v => v.BeatmapHash,
                static v => v.AlgorithmVersion,
                msdRevision,
                static v => v.SkillId == EzSkillSystems.MsdUnrateableSkillId);

            var csiRows = EzRealmFacetReader.Read(
                r.All<EzBeatmapChartSkillInfo>()
                 .AsEnumerable()
                 .Where(v => rateableBeatmapIds.ContainsKey(v.BeatmapHash)),
                static v => v.BeatmapHash,
                static v => v.InfoVersion,
                csiRevision,
                static v => v.KeyCount < 0);

            // ChartDan has no settled-miss row form, so no row of it is a stub.
            var danRows = EzRealmFacetReader.Read(
                r.All<EzBeatmapChartDan>()
                 .AsEnumerable()
                 .Where(v => rateableBeatmapIds.ContainsKey(v.BeatmapHash)),
                static v => v.BeatmapHash,
                static v => v.AlgorithmVersion,
                danRevision,
                static _ => false);

            // MSD settles either with a complete axis set or as the unrateable stub. Completeness is a property of
            // the whole axis set, so it is read separately from the per-row revision state above.
            var unrateableMsd = msdRows.Where(static kv => kv.Value.CurrentStub).Select(static kv => kv.Key).ToHashSet(StringComparer.Ordinal);
            var completeMsdSkills = readCompleteMsdSkills(msdRows, msdSkillsAtRevision);
            var completeMsd = completeMsdSkills.Keys.ToHashSet(StringComparer.Ordinal);
            var settledMsd = completeMsd.Union(unrateableMsd, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

            var settledCsi = csiRows.Where(kv => kv.Value.Revision == csiRevision).Select(static kv => kv.Key).ToHashSet(StringComparer.Ordinal);
            var completeCsi = csiRows.Where(kv => kv.Value.Revision == csiRevision && !kv.Value.CurrentStub).Select(static kv => kv.Key).ToHashSet(StringComparer.Ordinal);
            var completeDan = danRows.Where(kv => kv.Value.Revision == danRevision).Select(static kv => kv.Key).ToHashSet(StringComparer.Ordinal);

            var unresolvableDan = deriveUnresolvableChartDan(completeMsdSkills, rateableChartMeta);
            var settledChartDan = unrateableMsd.Union(unresolvableDan, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

            int universe = rateableBeatmapIds.Count;

            var facets = new[]
            {
                new EzDataStateFacet
                {
                    Id = "MSD",
                    Facet = EzAnalysisFacet.Msd,
                    CurrentRevision = msdRevision,
                    UniverseCount = universe,
                    Rows = msdRows,
                },
                new EzDataStateFacet
                {
                    Id = "CSI",
                    Facet = EzAnalysisFacet.ChartSkillInfo,
                    CurrentRevision = csiRevision,
                    UniverseCount = universe,
                    Rows = csiRows,
                    SettledWithoutRow = unrateableMsd,
                },
                new EzDataStateFacet
                {
                    Id = "Dan",
                    Facet = EzAnalysisFacet.ChartDan,
                    CurrentRevision = danRevision,
                    UniverseCount = universe,
                    Rows = danRows,
                    SettledWithoutRow = settledChartDan,
                },
            };

            return new EzChartChainState(
                rateableBeatmapIds,
                facets,
                settledMsd,
                completeMsd,
                unrateableMsd,
                settledCsi,
                completeCsi,
                completeDan,
                unresolvableDan);
        }

        /// <summary>
        /// Chart MSD axis sets that are complete at the current revision, keyed by hash, for the two verdicts that
        /// need the axes rather than a row's revision: whether MSD settled, and whether a dan resolves from them.
        /// </summary>
        private static Dictionary<string, IReadOnlyDictionary<string, double>> readCompleteMsdSkills(
            IReadOnlyDictionary<string, EzFacetRowState> msdRows,
            IReadOnlyDictionary<string, Dictionary<string, double>> msdSkillsAtRevision)
        {
            var complete = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);

            foreach (var (hash, skills) in msdSkillsAtRevision)
            {
                if (!msdRows.TryGetValue(hash, out var state) || state.CurrentStub)
                    continue;

                if (EzBeatmapMsdComputer.IsCurrentMsdCache(skills))
                    complete[hash] = skills;
            }

            return complete;
        }

        /// <summary>
        /// Charts whose complete MSD resolves to no dan: the dan stage's own write gate
        /// (<see cref="EzChartDanEstimator.FromMsd"/> resolving nothing) applied to the inputs that stage reads.
        /// </summary>
        private static HashSet<string> deriveUnresolvableChartDan(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> completeMsdSkills,
            IReadOnlyDictionary<string, RateableChartMeta> rateableChartMeta)
        {
            var unresolvable = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (hash, msd) in completeMsdSkills)
            {
                if (!rateableChartMeta.TryGetValue(hash, out var meta))
                    continue;

                int keyCount = meta.KeyCount;
                if (keyCount <= 0)
                    keyCount = 4;

                // A complete MSD always carries the hold ratio column, so the dan stage's CSI LnRatio fallback is
                // dead here and deliberately not repeated.
                double holdRatio = msd.TryGetValue(EzSkillSystems.MsdHoldRatioSkillId, out double hold) && double.IsFinite(hold)
                    ? Math.Clamp(hold, 0, 1)
                    : 0;

                if (EzChartDanEstimator.FromMsd(msd, keyCount, holdRatio, meta.XxyStarRating) == null)
                    unresolvable.Add(hash);
            }

            return unresolvable;
        }
    }
}
