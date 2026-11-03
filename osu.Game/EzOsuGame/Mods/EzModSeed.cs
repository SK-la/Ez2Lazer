// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Mods
{
    /// <summary>
    /// Resolves nullable <see cref="IHasSeed"/>-style <c>Seed</c> bindables for conversion.
    /// Null means "not yet rolled" (or user cleared the number box to re-roll).
    /// The value used for conversion is returned immediately; writing the bindable for UI
    /// always happens on the update thread so SettingsNumberBox is not mutated off-thread.
    /// </summary>
    public static class EzModSeed
    {
        private static Scheduler? updateScheduler;

        private static readonly ConditionalWeakTable<Bindable<int?>, SeedClaim> claims = new ConditionalWeakTable<Bindable<int?>, SeedClaim>();

        /// <summary>
        /// Bind the game update-thread scheduler (call once from game load).
        /// </summary>
        public static void BindUpdateScheduler(Scheduler scheduler)
        {
            ArgumentNullException.ThrowIfNull(scheduler);
            updateScheduler = scheduler;
        }

        /// <summary>
        /// Returns the seed to use for this conversion. If <paramref name="seed"/> is null,
        /// rolls once, claims that value for parallel callers, and schedules bindable write
        /// onto the update thread when needed.
        /// </summary>
        public static int Resolve(Bindable<int?> seed)
        {
            ArgumentNullException.ThrowIfNull(seed);

            if (seed.Value is int existing)
                return existing;

            var claim = claims.GetOrCreateValue(seed);

            lock (claim.Gate)
            {
                if (seed.Value is int nowSet)
                    return nowSet;

                if (claim.Pending is int pending)
                    return pending;

                int generated = RNG.Next();
                if (generated == 0)
                    generated = 1;

                claim.Pending = generated;
                writeSeed(seed, generated, claim);
                return generated;
            }
        }

        private static void writeSeed(Bindable<int?> seed, int generated, SeedClaim claim)
        {
            if (ThreadSafety.IsUpdateThread)
            {
                seed.Value ??= generated;
                claim.Pending = null;
                return;
            }

            var scheduler = updateScheduler;

            if (scheduler == null)
            {
                // Game not bound yet (tests / early boot): still assign so conversion stays consistent.
                seed.Value ??= generated;
                claim.Pending = null;
                return;
            }

            scheduler.Add(() =>
            {
                lock (claim.Gate)
                {
                    seed.Value ??= generated;
                    claim.Pending = null;
                }
            }, true);
        }

        private sealed class SeedClaim
        {
            public readonly object Gate = new object();
            public int? Pending;
        }
    }
}
