// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osuTK;

// ReSharper disable once CheckNamespace
namespace osu.Game.Rulesets.BMS.UI.MenuEntry
{
    /// <summary>
    /// Drawable returned by <see cref="BMSRuleset.CreateIcon"/>.
    /// Wraps the visual icon so we can piggy-back a hidden <see cref="BmsMainMenuButtonInjector"/>
    /// into the OsuGame Drawable tree without modifying any osu.Game code.
    ///
    /// Must NOT use <see cref="CompositeDrawable.AutoSizeAxes"/>: callers such as
    /// <c>PanelBeatmapSet.SpreadDisplay</c> assign <see cref="Drawable.Size"/> directly on the
    /// drawable returned from <see cref="Ruleset.CreateIcon"/>, which throws on auto-sized composites.
    /// </summary>
    public partial class BmsRulesetIcon : CompositeDrawable
    {
        public BmsRulesetIcon()
        {
            // Default to a size comparable to what other ruleset icons render at.
            // Consumers (toolbar, beatmap set spread, etc.) typically overwrite this anyway.
            Size = new Vector2(20);

            InternalChildren = new Drawable[]
            {
                new SpriteIcon
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = OsuIcon.RulesetMania,
                },
                new BmsMainMenuButtonInjector
                {
                    Alpha = 0f,
                    AlwaysPresent = true,
                },
            };
        }
    }
}
