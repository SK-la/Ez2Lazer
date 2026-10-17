// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Renders a single extension note as a body with an opaque head and tail.
    /// </summary>
    /// <remarks>
    /// The geometry is derived from the owning <see cref="ExtensionLane"/>'s scroll mapping, so it advances with the editor
    /// clock exactly like the playfield's own objects. Selection highlighting is drawn by the note's blueprint, not here,
    /// which keeps this drawable free of interaction state.
    /// </remarks>
    public partial class DrawableExtensionNote : CompositeDrawable
    {
        private const float minimum_height = ExtensionLane.MINIMUM_NOTE_HEIGHT;
        private const float body_opacity = 0.75f;
        private const float tail_opacity = 0.45f;

        public readonly ExtensionNote Note;

        private readonly ExtensionLane lane;

        private readonly Box body;
        private readonly Box head;
        private readonly Box tail;

        private bool hideTail;

        public DrawableExtensionNote(ExtensionNote note, ExtensionLane lane)
        {
            Note = note;
            this.lane = lane;

            RelativeSizeAxes = Axes.X;
            Height = minimum_height;

            InternalChildren = new Drawable[]
            {
                body = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.White.Opacity(body_opacity) },
                tail = new Box { RelativeSizeAxes = Axes.X, Height = minimum_height, Colour = Colour4.White.Opacity(tail_opacity) },
                head = new Box { RelativeSizeAxes = Axes.X, Height = minimum_height, Colour = Colour4.White },
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo? ezSkinInfo)
        {
            // Mirrors the playfield: with the fake tail on, the tail piece gives way to the body reaching the end.
            hideTail = ezSkinInfo?.ManiaLNGradientEnable.Value != false;
            tail.Alpha = hideTail ? 0 : 1;
        }

        protected override void Update()
        {
            base.Update();

            var span = lane.SpanOf(Note.Time, Note.Duration);

            Y = span.top;
            Height = span.height;

            float height = span.height;
            float endHeight = Note.Duration > 0 ? Math.Min(minimum_height, height * 0.5f) : 0;

            if (lane.ScrollsDown)
            {
                head.Y = height - endHeight;
                tail.Y = 0;
            }
            else
            {
                head.Y = 0;
                tail.Y = height - endHeight;
            }

            head.Height = endHeight;
            tail.Height = endHeight;

            head.Alpha = endHeight > 0 ? 1 : 0;
        }
    }
}
