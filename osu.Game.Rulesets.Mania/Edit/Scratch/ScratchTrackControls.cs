// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Scratch
{
    /// <summary>
    /// A vertical stack of buttons for appending and removing background tracks.
    /// </summary>
    /// <remarks>
    /// Raises intent only; the owner decides how to mutate the track set.
    /// </remarks>
    public partial class ScratchTrackControls : CompositeDrawable
    {
        public const float WIDTH = 40;
        private const float button_size = 30;

        public Action? AddRequested { get; set; }

        public Action? RemoveRequested { get; set; }

        public ScratchTrackControls()
        {
            RelativeSizeAxes = Axes.Y;
            Width = WIDTH;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new IconButton
                    {
                        Icon = FontAwesome.Solid.Plus,
                        Size = new Vector2(button_size),
                        Action = () => AddRequested?.Invoke(),
                    },
                    new IconButton
                    {
                        Icon = FontAwesome.Solid.Minus,
                        Size = new Vector2(button_size),
                        Action = () => RemoveRequested?.Invoke(),
                    },
                },
            };
        }
    }
}
