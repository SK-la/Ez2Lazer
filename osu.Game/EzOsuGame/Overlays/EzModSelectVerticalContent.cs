// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Mods;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays
{
    internal partial class EzModSelectVerticalContent : ModSelectContent
    {
        private readonly OsuScrollContainer scroll;
        private readonly FillFlowContainer<ModSelectColumn> sections;

        public override IEnumerable<ModSelectColumn> Columns => sections.Children;
        public override double ScrollPosition => scroll.Current;
        public override float TransitionDistance => 16;

        public EzModSelectVerticalContent(bool showPresets)
        {
            Padding = new MarginPadding { Horizontal = 70 };
            Child = scroll = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarOverlapsContent = false,
                Child = sections = new FillFlowContainer<ModSelectColumn>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 20),
                    Padding = new MarginPadding { Horizontal = 12, Bottom = 10 },
                },
            };

            if (showPresets)
                sections.Add(new ModPresetColumn(true));
        }

        public override void ReplaceModColumns(IEnumerable<ModColumn> columns)
        {
            foreach (var section in sections.Children.OfType<ModColumn>().ToArray())
                sections.Remove(section, true);

            sections.AddRange(columns);
        }

        public override void SetBottomPadding(float bottom) => sections.Padding = sections.Padding with { Bottom = bottom };

        public override void ApplyShear(float shear)
        {
            foreach (var section in sections)
                section.ApplyEzLayoutShear(shear);
        }

        public override void RestoreScroll(double position) => scroll.ScrollTo(Math.Clamp(position, 0, scroll.ScrollableExtent), false);

        public override void RevealColumn(ModSelectColumn column) => scroll.ScrollIntoView(column, false);

        public override void AutoScroll(Vector2 screenPosition, double elapsed)
        {
            var local = scroll.ToLocalSpace(screenPosition);
            float direction = local.Y < 45 ? -1 : local.Y > scroll.DrawHeight - 45 ? 1 : 0;
            RestoreScroll(scroll.Current + direction * elapsed * 0.65);
        }
    }
}
