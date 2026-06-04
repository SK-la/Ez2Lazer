// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.EzOsuGame.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    public class ManiaModAccuracyAdaptive : ModAccuracyAdaptive, IManiaRateAdjustmentMod
    {
        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[]
        {
            typeof(ManiaModChangeSpeedByAccuracy),
        }).ToArray();
    }
}
