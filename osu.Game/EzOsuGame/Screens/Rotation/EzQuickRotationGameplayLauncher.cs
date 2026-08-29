// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationGameplayLauncher
    {
        public static void Start(IScreen host,
                                 BeatmapManager beatmapManager,
                                 Bindable<WorkingBeatmap> beatmap,
                                 Bindable<RulesetInfo> ruleset,
                                 Bindable<IReadOnlyList<Mod>> mods,
                                 BeatmapInfo beatmapInfo,
                                 RulesetInfo rulesetInfo,
                                 IReadOnlyList<Mod> modList)
        {
            beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapInfo, true);

            if (beatmap.IsDefault)
                return;

            ruleset.Value = rulesetInfo;
            mods.Value = modList.Select(m => m.DeepClone()).ToArray();
            host.Push(new EzQuickRotationPlayerLoader(() => new SoloPlayer()));
        }
    }
}
