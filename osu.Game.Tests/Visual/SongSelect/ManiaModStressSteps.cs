// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Lists;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.Visual.SongSelect
{
    internal static class ManiaModStressSteps
    {
        public const int default_cycle_count = 12;
        public const int analysis_settle_wait_steps = 30;

        /// <summary>
        /// Allow wedge + a few carousel panels to retain live bindables after stress.
        /// </summary>
        public const int max_live_cache_bindables_slack = 4;

        /// <summary>
        /// Full SongSelect carousel may retain multiple bindables per beatmap change.
        /// </summary>
        public const int song_select_bindable_slack_per_cycle = 5;

        public static readonly IReadOnlyList<Mod> pattern_shift_mods = new Mod[] { new ManiaModPatternShift() };
        public static readonly IReadOnlyList<Mod> space_body_mods = new Mod[] { new ManiaModSpaceBody() };

        public static void performFullModCycle(Bindable<IReadOnlyList<Mod>> selectedMods)
        {
            selectedMods.SetDefault();
            selectedMods.Value = pattern_shift_mods;
            selectedMods.SetDefault();
            selectedMods.Value = space_body_mods;
            selectedMods.SetDefault();
        }

        public static int countAlive<T>(WeakList<T> weakList)
            where T : class
        {
            int count = 0;

            foreach (var _ in weakList)
                count++;

            return count;
        }

        public static int collectAndCount<T>(WeakList<T> weakList)
            where T : class
        {
            forceCollectionAndGetManagedMemory();
            return countAlive(weakList);
        }

        public static string formatMemoryDelta(long delta)
        {
            double kiloBytes = delta / 1024.0;
            return $"{kiloBytes:+0.0;-0.0;0.0} KB";
        }

        public static long forceCollectionAndGetManagedMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(true);
        }
    }
}
