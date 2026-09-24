// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    public readonly struct EzAnalysisLookupCache : IEquatable<EzAnalysisLookupCache>
    {
        public readonly BeatmapInfo BeatmapInfo;
        public readonly RulesetInfo Ruleset;
        public readonly Mod[] OrderedMods;
        public readonly int ModsSignature;

        public EzAnalysisLookupCache(BeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods)
        {
            BeatmapInfo = beatmapInfo;
            Ruleset = (rulesetInfo as RulesetInfo) ?? BeatmapInfo.Ruleset;

            // Clone so wedge analysis does not share setting instances with SelectedMods while hashing.
            // The snapshot also carries the effective seed, so this key equals what the conversion will use.
            OrderedMods = EzModSignature.SnapshotForConversion(mods);
            ModsSignature = EzModSignature.Compute(OrderedMods);
        }

        /// <summary>
        /// Cheap mods signature for conversion-derived cache keys, computed straight off the live mods.
        /// </summary>
        /// <remarks>
        /// Resolves null <see cref="IHasSeed.Seed"/> values as a side effect, so the key equals the conversion
        /// input rather than the pre-roll user-visible state.
        /// </remarks>
        public static int ComputeModsSignature(IEnumerable<Mod>? mods) => EzModSignature.ComputeResolvingSeeds(mods);

        public bool Equals(EzAnalysisLookupCache other) => BeatmapInfo.ID.Equals(other.BeatmapInfo.ID)
                                                           && string.Equals(BeatmapInfo.Hash, other.BeatmapInfo.Hash, StringComparison.Ordinal)
                                                           && Ruleset.Equals(other.Ruleset)
                                                           && EzModSignature.SequenceEqual(OrderedMods, other.OrderedMods);

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            hashCode.Add(BeatmapInfo.ID);
            hashCode.Add(BeatmapInfo.Hash);
            hashCode.Add(Ruleset.OnlineID);
            hashCode.Add(ModsSignature);

            return hashCode.ToHashCode();
        }
    }
}
