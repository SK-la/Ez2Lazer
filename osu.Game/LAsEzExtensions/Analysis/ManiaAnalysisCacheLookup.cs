// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.LAsEzExtensions.Analysis
{
    public readonly struct ManiaAnalysisCacheLookup : IEquatable<ManiaAnalysisCacheLookup>
    {
        public readonly BeatmapInfo BeatmapInfo;
        public readonly RulesetInfo Ruleset;
        public readonly Mod[] OrderedMods;
        public readonly int ModsSignature;

        // RequireXxySr 疑似会造成双重缓存，暂时禁用。
        // public readonly bool RequireXxySr;
        private static int modSnapshotFailCount;

        public ManiaAnalysisCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IEnumerable<Mod>? mods, bool requireXxySr)
        {
            BeatmapInfo = beatmapInfo;
            Ruleset = ruleset;
            // IMPORTANT: mod application order matters for beatmap conversion.
            // WorkingBeatmap.GetPlayableBeatmap() applies mods in the order provided.
            // Do not reorder here (eg. by Acronym), otherwise analysis may run on a different
            // playable beatmap than gameplay, which can cause incorrect results or crashes.
            OrderedMods = createModSnapshot(mods);
            // IMPORTANT: some custom mods (notably many YuLiangSSS mods) lazily assign a random seed during ApplyToBeatmap
            // (eg. Seed.Value ??= RNG.Next()). Because our cache key includes mod settings, such mutation would change
            // Mod.GetHashCode()/Equals() during computation and corrupt dictionary usage.
            // Pre-fill missing seeds deterministically on the cloned snapshot to keep cache keys stable.
            initialiseDeterministicSeedsIfRequired(OrderedMods, beatmapInfo);
            ModsSignature = computeModsSignature(OrderedMods);
            // RequireXxySr = requireXxySr;
        }

        private static int computeModsSignature(Mod[] orderedMods)
        {
            unchecked
            {
                var hash = new HashCode();

                // Include order. Order matters for conversion & gameplay.
                for (int i = 0; i < orderedMods.Length; i++)
                {
                    var mod = orderedMods[i];
                    hash.Add(mod.GetType());

                    // Mirror Mod.GetHashCode() semantics but decouple from mod instance mutation after signature is computed.
                    // Only settings exposed via [SettingSource] are included.
                    foreach (var setting in mod.SettingsBindables)
                        hash.Add(setting.GetUnderlyingSettingValue());
                }

                return hash.ToHashCode();
            }
        }

        private static void initialiseDeterministicSeedsIfRequired(Mod[] orderedMods, BeatmapInfo beatmapInfo)
        {
            if (orderedMods.Length == 0)
                return;

            unchecked
            {
                // Base seed derived from beatmap identity.
                int baseSeed = 17;
                baseSeed = baseSeed * 31 + beatmapInfo.ID.GetHashCode();
                baseSeed = baseSeed * 31 + (beatmapInfo.Hash?.GetHashCode(StringComparison.Ordinal) ?? 0);

                for (int i = 0; i < orderedMods.Length; i++)
                {
                    if (orderedMods[i] is not IHasSeed hasSeed)
                        continue;

                    if (hasSeed.Seed.Value != null)
                        continue;

                    // Mix in the mod type to avoid all seeded mods sharing the same seed.
                    int seed = baseSeed;
                    seed = seed * 31 + orderedMods[i].GetType().FullName!.GetHashCode(StringComparison.Ordinal);
                    seed = seed * 31 + i;

                    // Ensure non-null.
                    if (seed == 0)
                        seed = 1;

                    hasSeed.Seed.Value = seed;
                }
            }
        }

        private static Mod[] createModSnapshot(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                return Array.Empty<Mod>();

            var list = new List<Mod>();

            foreach (var mod in mods)
            {
                try
                {
                    list.Add(mod.DeepClone());
                }
                catch
                {
                    // If cloning fails, fall back to using the original instance.
                    // This is not ideal for caching, but is better than breaking analysis entirely.
                    if (Interlocked.Increment(ref modSnapshotFailCount) <= 10)
                        Logger.Log($"[EzBeatmapManiaAnalysisCache] Mod.DeepClone() failed for {mod.GetType().FullName}. Falling back to original instance.", LoggingTarget.Runtime, LogLevel.Important);

                    list.Add(mod);
                }
            }

            return list.ToArray();
        }

        public bool Equals(ManiaAnalysisCacheLookup other) => BeatmapInfo.ID.Equals(other.BeatmapInfo.ID)
                                                              && string.Equals(BeatmapInfo.Hash, other.BeatmapInfo.Hash, StringComparison.Ordinal)
                                                              && Ruleset.Equals(other.Ruleset)
                                                              // && RequireXxySr == other.RequireXxySr
                                                              && ModsSignature == other.ModsSignature;

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            hashCode.Add(BeatmapInfo.ID);
            hashCode.Add(BeatmapInfo.Hash);
            hashCode.Add(Ruleset.ShortName);
            // hashCode.Add(RequireXxySr);

            // Use precomputed signature rather than mod instances to avoid key mutation during analysis.
            hashCode.Add(ModsSignature);

            return hashCode.ToHashCode();
        }
    }
}
