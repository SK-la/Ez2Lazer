// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class Ez2KeyCounterDisplay : KeyCounterDisplay
    {
        protected override FillFlowContainer<KeyCounter> KeyFlow { get; }

        public Ez2KeyCounterDisplay()
        {
            Child = KeyFlow = new FillFlowContainer<KeyCounter>
            {
                Direction = FillDirection.Horizontal,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(Stage.COLUMN_SPACING),
            };
        }

        protected override KeyCounter CreateCounter(InputTrigger trigger) => new Ez2KeyCounter(trigger);
    }
}
