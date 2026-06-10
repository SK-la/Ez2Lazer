// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Scales an embedded player to fit the background while preserving gameplay aspect ratio.
    /// </summary>
    public partial class EzSkinEditorEmbeddedPlayerHost : Container
    {
        private const float base_width = 1024;
        private const float base_height = 768;

        private readonly Container scaleContainer;

        public EzSkinEditorEmbeddedPlayerHost(EzSkinEditorEmbeddedPlayer player)
        {
            RelativeSizeAxes = Axes.Both;

            Child = scaleContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(base_width, base_height),
                Child = player,
            };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (DrawWidth <= 1 || DrawHeight <= 1)
                return;

            float scale = Math.Min(DrawWidth / base_width, DrawHeight / base_height);
            scaleContainer.Scale = new Vector2(Math.Max(0.05f, scale));
        }
    }
}
