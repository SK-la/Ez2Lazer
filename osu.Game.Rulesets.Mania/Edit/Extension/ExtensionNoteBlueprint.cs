// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// The editor overlay of a single extension note: outlines it and receives selection input on its drawn rectangle.
    /// </summary>
    /// <remarks>
    /// Sizing itself to the note's rendered rectangle is what makes the selection box and click selection agree with what is
    /// on screen; no time band or column arithmetic is involved.
    /// </remarks>
    public partial class ExtensionNoteBlueprint : SelectionBlueprint<ExtensionNote>
    {
        private const float minimum_height = ExtensionLane.MINIMUM_NOTE_HEIGHT;
        private const float handle_height = 6;

        private readonly ExtensionArea area;
        private readonly Container outline;
        private readonly Box headHandle;
        private readonly Box tailHandle;

        public ExtensionNoteBlueprint(ExtensionNote note, ExtensionArea area)
            : base(note)
        {
            this.area = area;

            RelativeSizeAxes = Axes.None;

            InternalChild = outline = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                BorderThickness = 2,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        AlwaysPresent = true,
                    },
                    // Resize grips for the hold's head and tail. They live inside the outline so the selection state
                    // controls their visibility without per-frame alpha fights.
                    headHandle = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = handle_height,
                        Colour = Colour4.Gold,
                    },
                    tailHandle = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = handle_height,
                        Colour = Colour4.Gold,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            outline.BorderColour = Colour4.Gold;
        }

        protected override void Update()
        {
            base.Update();

            if (Parent == null || Item.Column == null || area.LaneOf(Item.Column) is not ExtensionLane lane || lane.Parent == null)
                return;

            var span = lane.SpanOf(Item.Time, Item.Duration);

            Vector2 topLeft = Parent.ToLocalSpace(lane.ToScreenSpace(new Vector2(0, span.top)));
            Vector2 bottomRight = Parent.ToLocalSpace(lane.ToScreenSpace(new Vector2(lane.DrawWidth, span.top + span.height)));

            Position = new Vector2(Math.Min(topLeft.X, bottomRight.X), Math.Min(topLeft.Y, bottomRight.Y));
            Size = new Vector2(Math.Max(1, Math.Abs(bottomRight.X - topLeft.X)), Math.Max(minimum_height, Math.Abs(bottomRight.Y - topLeft.Y)));

            updateHandles(lane);
        }

        private void updateHandles(ExtensionLane lane)
        {
            bool hasEnds = Item.Duration > 0 && Size.Y >= handle_height * 3;

            headHandle.Alpha = hasEnds ? 1 : 0;
            tailHandle.Alpha = hasEnds ? 1 : 0;

            if (!hasEnds)
                return;

            // The head sits at the start time, which is the low end of the note for an upward-scrolling column.
            if (lane.ScrollsDown)
            {
                headHandle.Y = Size.Y - handle_height;
                tailHandle.Y = 0;
            }
            else
            {
                headHandle.Y = 0;
                tailHandle.Y = Size.Y - handle_height;
            }
        }
    }
}
