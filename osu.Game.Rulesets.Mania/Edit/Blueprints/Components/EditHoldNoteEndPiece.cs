// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Skinning.Argon;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Blueprints.Components
{
    public partial class EditHoldNoteEndPiece : CompositeDrawable
    {
        /// <summary>
        /// Minimum height for editor drag handles. Ez skins may hide the tail drawable (zero <see cref="Drawable.DrawHeight"/>);
        /// the interaction area must remain usable regardless of skin visuals.
        /// </summary>
        public const float MINIMUM_INTERACTION_HEIGHT = ArgonNotePiece.NOTE_HEIGHT;

        public static float GetInteractionHeight(float skinDrawableHeight) =>
            Math.Max(skinDrawableHeight, MINIMUM_INTERACTION_HEIGHT);

        public Action? DragStarted { get; init; }
        public Action<Vector2>? Dragging { get; init; }
        public Action? DragEnded { get; init; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Height = DefaultNotePiece.NOTE_HEIGHT;

            InternalChild = new EditNotePiece
            {
                RelativeSizeAxes = Axes.Both,
                Height = 1,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateState();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            DragStarted?.Invoke();
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);
            Dragging?.Invoke(e.ScreenSpaceMousePosition);
            updateState();
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            base.OnDragEnd(e);
            DragEnded?.Invoke();
            updateState();
        }

        private void updateState()
        {
            InternalChild.Colour = Colour4.White;

            var colour = colours.Yellow;

            if (IsHovered || IsDragged)
                colour = colour.Lighten(1);

            Colour = colour;
        }
    }
}
