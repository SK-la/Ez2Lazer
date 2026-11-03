// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Wires autoplay replay input into in-editor ruleset previews (same as <see cref="Rulesets.Edit.DrawableEditorRulesetWrapper{TObject}"/>).
    /// </summary>
    public static class EzSkinEditorRulesetPreviewBootstrap
    {
        public static void ApplyAutoplayReplay(DrawableRuleset ruleset, IBeatmap beatmap)
        {
            var autoplay = ruleset.Mods.OfType<ICreateReplayData>().FirstOrDefault();

            if (autoplay != null)
                ruleset.SetReplayScore(autoplay.CreateScoreFromReplayData(beatmap, ruleset.Mods));
        }
    }
}
