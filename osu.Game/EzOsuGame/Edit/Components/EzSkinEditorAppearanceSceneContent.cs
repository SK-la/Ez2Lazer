// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Appearance scene: gameplay viewport in the scene content region.
    /// Playback controls live in <see cref="EzSkinEditorSceneBar"/>.
    /// </summary>
    public partial class EzSkinEditorAppearanceSceneContent : Container
    {
        private readonly EzSkinEditorSceneContext context;

        private Container playerViewport = null!;

        public EzSkinEditorAppearanceSceneContent(EzSkinEditorSceneContext context)
        {
            this.context = context;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = playerViewport = new Container { RelativeSizeAxes = Axes.Both, Masking = true };
            showPlaceholder();
        }

        public void SetEmbeddedPlayer(EzSkinEditorEmbeddedPlayer? player)
        {
            playerViewport.Clear();

            if (player == null)
            {
                showPlaceholder();
                return;
            }

            if (!player.CanBeMounted)
            {
                showPlaceholder();
                return;
            }

            player.DetachForRemount();
            playerViewport.Child = new EzSkinEditorEmbeddedPlayerHost(player);
        }

        public void RefreshFromContext(EzSkinEditorSceneContext newContext)
        {
            context.GetEmbeddedPlayer = newContext.GetEmbeddedPlayer;
        }

        private void showPlaceholder()
        {
            playerViewport.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED);
        }

        private static OsuSpriteText createPlaceholder(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Colour = Color4.White,
        };
    }
}
