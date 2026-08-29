// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Select
{
    public partial class SoloSongSelect
    {
        private void tryBeginQuickRotation()
        {
            if (!EzQuickRotationSession.IsEnabled || EzQuickRotationCoordinator.Session.IsActive)
                return;

            var criteria = FilterControl.CreateCriteria();
            var beatmapInfo = Beatmap.Value.BeatmapInfo;
            double baseline = EzQuickRotationDifficultyHelper.GetBaselineStarRating(beatmaps, beatmapInfo, Ruleset.Value, Mods.Value);

            EzQuickRotationCoordinator.Session.Begin(beatmaps, criteria, beatmapInfo, Ruleset.Value, Mods.Value, baseline);
        }
    }
}
