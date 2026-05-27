// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    /// <summary>
    /// Mania 版 LP：将 <see cref="ModLoopPlayClip.ConstantSpeed"/> 接到恒定 scroll（无 SV 变速）。
    /// </summary>
    public class ManiaUniversalLoopPlayClip : UniversalLoopPlayClip, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            if (!ConstantSpeed.Value)
                return;

            ((DrawableManiaRuleset)drawableRuleset).VisualisationMethod = ScrollVisualisationMethod.Constant;
        }
    }
}
