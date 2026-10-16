// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Mania.Edit.Scratch;
using osu.Game.Rulesets.Mania.UI;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Hosts the mania editor playfield alongside the background scratch tracks.
    /// </summary>
    /// <remarks>
    /// The playfield keeps the exact size, scale and column placement it has in gameplay. The background tracks are
    /// docked in the space the playfield canvas leaves unused to the right of its columns, so both stay visible at once.
    /// </remarks>
    public partial class ManiaEditorPlayfieldContent : CompositeDrawable
    {
        private const float dock_gap = 8;

        private readonly ScratchTrackSet trackSet = new ScratchTrackSet();

        private readonly ManiaPlayfield playfield;
        private readonly float rightReserve;

        private readonly HoverActivationContainer playfieldRegion;
        private readonly HoverActivationContainer dockRegion;

        /// <param name="rightReserve">
        /// Screen-space width kept clear on the right for the composer's toolboxes.
        /// </param>
        public ManiaEditorPlayfieldContent(Drawable playfieldContent, ManiaPlayfield playfield, float rightReserve)
        {
            this.playfield = playfield;
            this.rightReserve = rightReserve;

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                playfieldRegion = new HoverActivationContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    Child = playfieldContent,
                },
                dockRegion = new HoverActivationContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new DockScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Right = ScratchTrackControls.WIDTH },
                            Child = new ScratchArea(trackSet),
                        },
                        new ScratchTrackControls
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            AddRequested = () => trackSet.Append(),
                            RemoveRequested = () => trackSet.RemoveLast(),
                        },
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            // The playfield keeps occupying the full viewport; the background tracks are docked to the right of its columns.
            if (Parent != null)
                playfieldRegion.Width = Parent.DrawWidth;

            float left = columnsRightInLocalSpace() + dock_gap;

            dockRegion.X = left;
            dockRegion.Width = Math.Max(0, DrawWidth - rightReserve - left);
        }

        /// <summary>
        /// The right edge of the playfield columns, in this container's space.
        /// </summary>
        /// <remarks>
        /// Measured rather than derived, so it follows skin scaling, dual stages and any stage placement offset.
        /// </remarks>
        private float columnsRightInLocalSpace()
        {
            float right = float.NegativeInfinity;

            foreach (var stage in playfield.Stages)
            {
                var quad = stage.ScreenSpaceDrawQuad.AABBFloat;

                if (quad.Width <= 0)
                    continue;

                right = Math.Max(right, ToLocalSpace(new Vector2(quad.Right, quad.Centre.Y)).X);
            }

            // Before the stages have been laid out there is nothing to measure; keep the tracks clear of the centre.
            return float.IsNegativeInfinity(right) ? DrawWidth * 0.5f : right;
        }

        private partial class DockScrollContainer : OsuScrollContainer
        {
            public DockScrollContainer()
                : base(Direction.Horizontal)
            {
            }

            // The wheel stays reserved for the editor timeline; the dock scrolls by dragging its own scrollbar.
            protected override bool OnScroll(ScrollEvent e) => false;

            // Panning the tracks is an explicit gesture so that a plain left-drag stays owned by selection and note
            // interactions (the base implementation would also swallow the follow-up click).
            protected override bool OnDragStart(DragStartEvent e)
            {
                if (e.Button != MouseButton.Left || !e.ShiftPressed)
                    return false;

                return base.OnDragStart(e);
            }
        }
    }
}
