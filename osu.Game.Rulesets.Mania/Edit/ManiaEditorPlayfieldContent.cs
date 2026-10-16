// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Hosts the mania editor playfield alongside the background scratch area, allowing horizontal scrolling
    /// when the combined width exceeds the available viewport.
    /// </summary>
    /// <remarks>
    /// POC: the scratch area currently renders placeholder columns only. Track data and interaction arrive in later steps.
    /// </remarks>
    public partial class ManiaEditorPlayfieldContent : Container
    {
        private const float scratch_column_width = 60;

        private readonly Container playfieldHost;
        private readonly FillFlowContainer scratchColumns;

        public ManiaEditorPlayfieldContent(Drawable playfieldContent)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new HorizontalScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        playfieldHost = new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Child = playfieldContent,
                        },
                        scratchColumns = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Direction = FillDirection.Horizontal,
                        },
                    },
                },
            };

            for (int i = 1; i <= 5; i++)
                scratchColumns.Add(createPlaceholderColumn(i));
        }

        protected override void Update()
        {
            base.Update();

            // The playfield keeps occupying the full viewport; the scratch area is appended to its right.
            if (Parent != null)
                playfieldHost.Width = Parent.DrawWidth;
        }

        private static Drawable createPlaceholderColumn(int number) => new Container
        {
            RelativeSizeAxes = Axes.Y,
            Width = scratch_column_width,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.6f),
                },
                new OsuSpriteText
                {
                    Text = $"#{number}",
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Margin = new MarginPadding { Top = 5 },
                },
            },
        };

        private partial class HorizontalScrollContainer : OsuScrollContainer
        {
            public HorizontalScrollContainer()
                : base(Direction.Horizontal)
            {
            }

            // Horizontal scrolling is driven exclusively by dragging the scrollbar; the wheel stays reserved for the editor timeline.
            protected override bool OnScroll(ScrollEvent e) => false;

            // Content dragging is not a pan gesture here: horizontal scrolling is scrollbar-only, so the left button stays
            // free for selection (playfield now, scratch area later). The scrollbar handles its own dragging independently.
            protected override bool OnDragStart(DragStartEvent e) => false;
        }
    }
}
