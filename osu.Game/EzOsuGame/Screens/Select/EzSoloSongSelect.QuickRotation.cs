// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Screens.Select
{
    public static class EzSoloSongSelect
    {
        public static void TryBeginQuickRotation(BeatmapManager beatmaps,
                                                 WorkingBeatmap beatmap,
                                                 Bindable<RulesetInfo> ruleset,
                                                 Bindable<IReadOnlyList<Mod>> mods,
                                                 FilterControl filterControl)
        {
            if (!EzQuickRotationSession.IsEnabled || EzQuickRotationCoordinator.Session.IsActive)
                return;

            var criteria = filterControl.CreateCriteria();
            var beatmapInfo = beatmap.BeatmapInfo;
            double baseline = EzQuickRotationDifficultyHelper.GetBaselineStarRating(beatmaps, beatmapInfo, ruleset.Value, mods.Value);

            EzQuickRotationCoordinator.Session.Begin(beatmaps, criteria, beatmapInfo, ruleset.Value, mods.Value, baseline);
        }
    }
}
