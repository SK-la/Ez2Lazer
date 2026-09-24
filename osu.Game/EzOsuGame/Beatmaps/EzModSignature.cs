// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Beatmaps
{
    /// <summary>
    /// The single source of truth for the mods half of a conversion/difficulty cache key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The signature is order-preserving on purpose. <c>WorkingBeatmap.GetPlayableBeatmap</c> applies
    /// conversion mods with a stable sort on <c>IEzApplyOrder</c>, so two mods sharing an order keep their
    /// list order and can produce different beatmaps; a key that sorted by acronym would merge those two
    /// distinct inputs into one entry.
    /// </para>
    /// <para>
    /// Values mirror <see cref="Mod.GetHashCode"/> (type plus the settings exposed through
    /// <c>[SettingSource]</c>), so the key is decoupled from the mod instance mutating after the fact.
    /// </para>
    /// </remarks>
    public static class EzModSignature
    {
        private static int modSnapshotFailCount;

        /// <summary>
        /// Signature of the settings as they currently read. A null <see cref="IHasSeed.Seed"/> stays absent.
        /// </summary>
        public static int Compute(IEnumerable<Mod>? mods) => compute(mods, resolveSeeds: false);

        /// <summary>
        /// Resolves null seeds and then signs the same instances, i.e. "the signature of what conversion will do".
        /// </summary>
        /// <remarks>
        /// Prefer this over bare <see cref="Compute"/> for any key that fronts a conversion result: a null seed
        /// is absent from the pure signature while still changing the converted beatmap, so a key that skips
        /// resolution is blind to a re-rolled seed.
        /// </remarks>
        public static int ComputeResolvingSeeds(IEnumerable<Mod>? mods) => compute(mods, resolveSeeds: true);

        /// <summary>
        /// Deep-clones <paramref name="mods"/> into a conversion input, carrying each mod's effective seed into the
        /// copy so the copy converts with the same roll the key was built from.
        /// </summary>
        /// <remarks>
        /// <see cref="EzModSeed.Resolve"/> may only *claim* a rolled seed when it runs off the update thread; the
        /// write-back to the live bindable happens later. A plain <c>DeepClone()</c> inside that window would
        /// snapshot a null seed and let the copy roll its own, which is exactly the "key and result disagree"
        /// behaviour we are removing.
        /// </remarks>
        public static Mod[] SnapshotForConversion(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                return Array.Empty<Mod>();

            var source = mods as IReadOnlyList<Mod> ?? mods.ToList();
            var snapshot = new Mod[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                Mod original = source[i];
                int? effectiveSeed = original is IHasSeed hasSeed ? EzModSeed.Resolve(hasSeed.Seed) : null;
                Mod clone = cloneOrSelf(original);

                if (effectiveSeed is int seed && clone is IHasSeed cloneWithSeed)
                    cloneWithSeed.Seed.Value = seed;

                snapshot[i] = clone;
            }

            return snapshot;
        }

        /// <summary>
        /// Deep, order-sensitive settings equality of two mod sequences — the equality half of <see cref="Compute"/>.
        /// </summary>
        /// <remarks>
        /// A 32-bit signature is not an equality test. Boxed <c>0</c> and <c>null</c> hash identically
        /// (<c>0f.GetHashCode() == 0</c>, and <see cref="HashCode"/> adds <c>0</c> for null), so a key whose
        /// <c>Equals</c> compared signatures would answer one mod's settings with another's cached value —
        /// the exact failure <c>TestSceneBeatmapDifficultyCache.TestStarDifficultyAdjustHashCodeConflict</c> pins.
        /// Signatures are for <c>GetHashCode</c>; this is for <c>Equals</c>.
        /// </remarks>
        public static bool SequenceEqual(IReadOnlyList<Mod>? a, IReadOnlyList<Mod>? b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null || a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }

            return true;
        }

        private static int compute(IEnumerable<Mod>? mods, bool resolveSeeds)
        {
            var hash = new HashCode();

            if (mods == null)
                return hash.ToHashCode();

            foreach (var mod in mods)
            {
                // 包含顺序。顺序对转换和游戏很重要。
                hash.Add(mod.GetType());

                IBindable? seedBindable = mod is IHasSeed hasSeed ? hasSeed.Seed : null;
                int? effectiveSeed = resolveSeeds && seedBindable is Bindable<int?> nullableSeed ? EzModSeed.Resolve(nullableSeed) : null;

                foreach (var setting in mod.SettingsBindables)
                {
                    // 跨线程时 seed 可能只被 claim、还没写回 bindable，所以直接取解析结果，不读 bindable。
                    object? value = ReferenceEquals(setting, seedBindable) && effectiveSeed is int seed
                        ? seed
                        : setting.GetUnderlyingSettingValue();

                    hash.Add(value);
                }
            }

            return hash.ToHashCode();
        }

        private static Mod cloneOrSelf(Mod mod)
        {
            try
            {
                return mod.DeepClone();
            }
            catch
            {
                if (Interlocked.Increment(ref modSnapshotFailCount) <= 10)
                {
                    Logger.Log(
                        $"[EzModSignature] Mod.DeepClone() failed for {mod.GetType().FullName}. Falling back to original instance.",
                        Ez2ConfigManager.LOGGER_NAME,
                        LogLevel.Important);
                }

                return mod;
            }
        }
    }
}
