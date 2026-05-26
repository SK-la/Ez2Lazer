// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods
{
    /// <summary>
    /// Centralised mod incompatibility declarations for Ez mania mods.
    /// Keeps <see cref="TestSceneModValidity"/>'s two-way incompatibility checks satisfied.
    /// </summary>
    internal static class EzManiaModCompatibility
    {
        public static readonly Type[] RATE_ADJUST_COMMUNITY_MODS =
        {
            typeof(ManiaModHealthAdaptive),
            typeof(ManiaModAccuracyAdaptive),
            typeof(ManiaModChangeSpeedByAccuracy),
            typeof(ManiaModNiceBPM),
            typeof(ManiaModAdjust),
        };

        public static readonly Type[] MANIA_RATE_ADJUST_MODS =
        {
            typeof(ManiaModHalfTime),
            typeof(ManiaModDoubleTime),
            typeof(ManiaModDaycore),
            typeof(ManiaModNightcore),
        };

        public static readonly Type[] HOLD_OFF_COMMUNITY_MODS =
        {
            typeof(ManiaModSpaceBody),
            typeof(ManiaModReleaseAdjust),
        };

        public static readonly Type[] TIME_RAMP_MODS =
        {
            typeof(ModWindUp),
            typeof(ModWindDown),
        };
    }
}
