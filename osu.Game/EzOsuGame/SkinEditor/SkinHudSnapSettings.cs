// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.SkinEditor
{
    public static class SkinHudSnapSettings
    {
        public static readonly float[] DISTANCE_PRESETS = { 10, 15, 20, 25, 50 };

        public static bool CanSnap(GlobalSkinnableContainerLookup? target) =>
            target?.Lookup is GlobalSkinnableContainers.MainHUDComponents or GlobalSkinnableContainers.SongSelect;

        public static float SnapToPreset(float value)
        {
            if (DISTANCE_PRESETS.Contains(value))
                return value;

            return DISTANCE_PRESETS.OrderBy(p => Math.Abs(p - value)).First();
        }
    }
}
