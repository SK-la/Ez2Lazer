// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Session-only storage for mania gameplay modes, keyed by local <see cref="ScoreInfo"/> id.
    /// Cleared when the process exits. Does not touch Realm or disk.
    /// </summary>
    public static class EzManiaScoreModeSessionCache
    {
        private static readonly ConcurrentDictionary<Guid, (int hitMode, int healthMode)> cache = new ConcurrentDictionary<Guid, (int hitMode, int healthMode)>();

        public static void Store(Guid scoreId, int hitMode, int healthMode)
        {
            if (scoreId == Guid.Empty)
                return;

            cache[scoreId] = (hitMode, healthMode);
        }

        public static bool TryGet(Guid scoreId, out int hitMode, out int healthMode)
        {
            if (cache.TryGetValue(scoreId, out var entry))
            {
                hitMode = entry.hitMode;
                healthMode = entry.healthMode;
                return true;
            }

            hitMode = EzManiaScoreModeExtensions.UNSET_MODE;
            healthMode = EzManiaScoreModeExtensions.UNSET_MODE;
            return false;
        }
    }
}
