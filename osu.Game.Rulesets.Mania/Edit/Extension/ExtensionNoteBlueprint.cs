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
        private const float minimum_height = 8;

        private readonly ExtensionArea area;
        private readonly Container outline;

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
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    AlwaysPresent = true,
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

            float startY = lane.OffsetAt(Item.Time);
            float endY = lane.OffsetAt(Item.Time + Item.Duration);

            float top = Math.Min(startY, endY);
            float bottom = Math.Max(startY, endY);

            if (bottom - top < minimum_height)
            {
                float padding = (minimum_height - (bottom - top)) / 2;
                top -= padding;
                bottom += padding;
            }

            Vector2 topLeft = Parent.ToLocalSpace(lane.ToScreenSpace(new Vector2(0, top)));
            Vector2 bottomRight = Parent.ToLocalSpace(lane.ToScreenSpace(new Vector2(lane.DrawWidth, bottom)));

            Position = new Vector2(Math.Min(topLeft.X, bottomRight.X), Math.Min(topLeft.Y, bottomRight.Y));
            Size = new Vector2(Math.Max(1, Math.Abs(bottomRight.X - topLeft.X)), Math.Max(minimum_height, Math.Abs(bottomRight.Y - topLeft.Y)));
        }
    }
}
