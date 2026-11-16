// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Beatmaps
{
    /// <summary>
    /// Compatibility shim forwarding to <see cref="WorkingBeatmap"/> playable caching. Prefer calling
    /// <see cref="WorkingBeatmap.GetPlayableBeatmap"/> / <see cref="WorkingBeatmap.GetMutablePlayableBeatmap"/> directly.
    /// </summary>
    [Obsolete("Use WorkingBeatmap.GetPlayableBeatmap / GetMutablePlayableBeatmap")]
    public static class EzPlayableBeatmapCache
    {
        public const int SHARED_SCOPE = 0;

        public const int MAX_ENTRIES_PER_WORKING_BEATMAP = WorkingBeatmap.MAX_SHARED_PLAYABLE_ENTRIES;

        public const long MAX_HIT_OBJECTS_PER_WORKING_BEATMAP = WorkingBeatmap.MAX_SHARED_PLAYABLE_HIT_OBJECTS;

        public static IBeatmap GetShared(IWorkingBeatmap working, IRulesetInfo ruleset, IReadOnlyList<Mod>? mods = null, CancellationToken? token = null)
        {
            ArgumentNullException.ThrowIfNull(working);
            ArgumentNullException.ThrowIfNull(ruleset);

            return token is CancellationToken cancellation
                ? working.GetPlayableBeatmap(ruleset, mods, cancellation)
                : working.GetPlayableBeatmap(ruleset, mods);
        }

        public static IBeatmap GetBound(IWorkingBeatmap working, IRulesetInfo ruleset, IReadOnlyList<Mod>? mods, int bindingScope, Action<IBeatmap> bind,
                                       CancellationToken? token = null)
        {
            ArgumentNullException.ThrowIfNull(working);
            ArgumentNullException.ThrowIfNull(ruleset);
            ArgumentNullException.ThrowIfNull(bind);
            ArgumentOutOfRangeException.ThrowIfLessThan(bindingScope, 1);

            if (working is WorkingBeatmap workingBeatmap)
            {
                return token is CancellationToken cancellation
                    ? workingBeatmap.GetMutablePlayableBeatmap(ruleset, mods, cancellation, bind)
                    : workingBeatmap.GetMutablePlayableBeatmap(ruleset, mods, bind);
            }

            IBeatmap converted = GetShared(working, ruleset, mods, token);
            bind(converted);
            return converted;
        }

        public static bool IsSharedInstance(IBeatmap beatmap)
            => WorkingBeatmap.IsCachedReadOnlyPlayableInstance(beatmap);

        public static void Reset()
        {
            WorkingBeatmap.ClearCachedReadOnlyMarkersForTesting();
        }
    }
}