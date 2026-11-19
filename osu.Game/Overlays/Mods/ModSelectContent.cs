// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.Overlays.Mods
{
    internal abstract partial class ModSelectContent : Container
    {
        public abstract IEnumerable<ModSelectColumn> Columns { get; }
        public abstract double ScrollPosition { get; }
        public virtual float TransitionDistance => 700;

        protected ModSelectContent()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public abstract void ReplaceModColumns(IEnumerable<ModColumn> columns);
        public abstract void SetBottomPadding(float bottom);
        public abstract void ApplyShear(float shear);
        public abstract void RestoreScroll(double position);
        public abstract void RevealColumn(ModSelectColumn column);
        public abstract void AutoScroll(Vector2 screenPosition, double elapsed);

        public virtual void InitialiseScroll() => RestoreScroll(0);

        public void FlushPendingSelections()
        {
            foreach (var column in Columns.OfType<ModColumn>())
                column.FlushPendingSelections();
        }
    }

    internal partial class HorizontalModSelectContent : ModSelectContent
    {
        private readonly ModSelectOverlay.ColumnScrollContainer scroll;
        private readonly ModSelectOverlay.ColumnFlowContainer flow;

        public override IEnumerable<ModSelectColumn> Columns => flow.Columns;
        public override double ScrollPosition => scroll.Current;

        public HorizontalModSelectContent(IEnumerable<ModSelectOverlay.ColumnDimContainer> columns)
        {
            Child = scroll = new ModSelectOverlay.ColumnScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Masking = false,
                ClampExtension = 100,
                ScrollbarOverlapsContent = false,
                Child = flow = new ModSelectOverlay.ColumnFlowContainer
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Direction = FillDirection.Horizontal,
                    Shear = OsuGame.SHEAR,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Margin = new MarginPadding { Horizontal = 70 },
                    Padding = new MarginPadding { Bottom = 10 },
                    ChildrenEnumerable = columns,
                },
            };
        }

        public override void ReplaceModColumns(IEnumerable<ModColumn> columns)
        {
            foreach (var child in flow.Children.Where(child => child.Column is ModColumn).ToArray())
                flow.Remove(child, true);

            foreach (var column in columns)
                flow.Add(new ModSelectOverlay.ColumnDimContainer(column));
        }

        public override void SetBottomPadding(float bottom) => flow.Padding = flow.Padding with { Bottom = bottom };

        public override void ApplyShear(float shear)
        {
            flow.Shear = new Vector2(shear, 0);

            foreach (var column in Columns)
                column.ApplyEzLayoutShear(shear);
        }

        public override void RestoreScroll(double position) => scroll.ScrollTo(Math.Clamp(position, 0, scroll.ScrollableExtent), false);

        public override void RevealColumn(ModSelectColumn column) => scroll.ScrollIntoView(column.Parent ?? column, false, 140);

        public override void InitialiseScroll()
        {
            scroll.ScrollToEnd(false);
            scroll.ScrollTo(0);
        }

        public override void AutoScroll(Vector2 screenPosition, double elapsed)
        {
            var local = scroll.ToLocalSpace(screenPosition);
            float direction = local.X < 45 ? -1 : local.X > scroll.DrawWidth - 45 ? 1 : 0;
            RestoreScroll(scroll.Current + direction * elapsed * 0.65);
        }
    }
}
