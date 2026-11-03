// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Note silhouette shared by the editor's extension notes and the replay input overlay:
    /// an opaque head block, a translucent body and a lighter tail block.
    /// </summary>
    /// <remarks>
    /// Sizing is supplied by the caller because the editor clamps short notes to a minimum height while replay input
    /// notes must keep the exact held duration.
    /// </remarks>
    public partial class ExtensionNoteVisual : CompositeDrawable
    {
        private const float body_opacity = 0.75f;
        private const float tail_opacity = 0.45f;

        private readonly Box body;
        private readonly Box head;
        private readonly Box tail;

        public Colour4 BodyColour
        {
            set => body.Colour = value.Opacity(body_opacity);
        }

        public Colour4 HeadColour
        {
            set => head.Colour = value;
        }

        public Colour4 TailColour
        {
            set => tail.Colour = value.Opacity(tail_opacity);
        }

        /// <summary>
        /// Hides the tail block when the LN gradient is enabled, mirroring the playfield.
        /// </summary>
        public bool TailHidden
        {
            set => tail.Alpha = value ? 0 : 1;
        }

        public ExtensionNoteVisual()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                body = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.White.Opacity(body_opacity) },
                tail = new Box { RelativeSizeAxes = Axes.X, Colour = Colour4.White.Opacity(tail_opacity) },
                head = new Box { RelativeSizeAxes = Axes.X, Colour = Colour4.White },
            };
        }

        /// <summary>
        /// Positions the head and tail blocks inside a note of <paramref name="height"/>.
        /// </summary>
        /// <param name="height">Total height of the note, in local units.</param>
        /// <param name="headHeight">Height of the head (and tail) block; zero hides the head.</param>
        /// <param name="scrollsDown">Whether the note's start edge is at the bottom of the note.</param>
        public void ApplySpan(float height, float headHeight, bool scrollsDown)
        {
            if (scrollsDown)
            {
                head.Y = height - headHeight;
                tail.Y = 0;
            }
            else
            {
                head.Y = 0;
                tail.Y = height - headHeight;
            }

            head.Height = headHeight;
            tail.Height = headHeight;
            head.Alpha = headHeight > 0 ? 1 : 0;
        }
    }
}
