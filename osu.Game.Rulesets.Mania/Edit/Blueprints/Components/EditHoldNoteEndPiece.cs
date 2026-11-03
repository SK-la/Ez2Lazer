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

namespace osu.Game.Rulesets.Mania.Edit.Blueprints.Components
{
    public partial class EditHoldNoteEndPiece : CompositeDrawable
    {
        /// <summary>
        /// Minimum height for editor drag handles when Ez skins report zero tail/head <see cref="Drawable.DrawHeight"/>.
        /// </summary>
        public const float MINIMUM_INTERACTION_HEIGHT = ArgonNotePiece.NOTE_HEIGHT;

        public static float GetInteractionHeight(float skinDrawableHeight) =>
            Math.Max(skinDrawableHeight, MINIMUM_INTERACTION_HEIGHT);

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

        protected override bool OnDragStart(DragStartEvent e) => false;

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
