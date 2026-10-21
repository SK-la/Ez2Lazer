// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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

        public readonly ExtensionNote Note;

        private readonly ExtensionLane lane;

        private readonly ExtensionNoteVisual visual;

        public DrawableExtensionNote(ExtensionNote note, ExtensionLane lane)
        {
            Note = note;
            this.lane = lane;

            RelativeSizeAxes = Axes.X;
            Height = minimum_height;

            InternalChild = visual = new ExtensionNoteVisual();
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo? ezSkinInfo)
        {
            // Mirrors the playfield: with the fake tail on, the tail piece gives way to the body reaching the end.
            visual.TailHidden = ezSkinInfo?.ManiaLNGradientEnable.Value != false;
        }

        protected override void Update()
        {
            base.Update();

            var span = lane.SpanOf(Note.Time, Note.Duration);

            Y = span.top;
            Height = span.height;

            float headHeight = Note.Duration > 0 ? Math.Min(minimum_height, span.height * 0.5f) : 0;

            visual.ApplySpan(span.height, headHeight, lane.ScrollsDown);
        }
    }
}
