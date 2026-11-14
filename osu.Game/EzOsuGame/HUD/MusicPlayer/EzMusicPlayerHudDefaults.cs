// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Screens.Menu;
using osu.Game.EzOsuGame.Screens.Visualizer;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.HUD.MusicPlayer
{
    public static class EzMusicPlayerHudDefaults
    {
        public const float VISUALISER_ALPHA = 0.5f;
        public const float LOGO_TO_CONTROLS_SPACING = 20;

        public static void ApplyIfEmpty(EzLayoutContainer container)
        {
            if (container.Components.Count > 0)
                return;

            TryPlace(container, CreateVisualisation());
            TryPlace(container, new EzVisualizerBeatmapLogo
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                UsesFixedAnchor = true,
            });
            TryPlace(container, new EzVisualizerPlaybackBar
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.TopCentre,
                Y = EzVisualizerBeatmapLogo.BASE_SIZE / 2 + LOGO_TO_CONTROLS_SPACING,
                UsesFixedAnchor = true,
            });
            TryPlace(container, new EzVisualizerPlaylist
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Margin = new MarginPadding { Right = 24 },
                UsesFixedAnchor = true,
            });
        }

        public static EzMenuLogoVisualisation CreateVisualisation() => new EzMenuLogoVisualisation
        {
            Size = new Vector2(EzVisualizerBeatmapLogo.BASE_SIZE) * OsuLogo.SCALE_ADJUST,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Alpha = VISUALISER_ALPHA,
            UsesFixedAnchor = true,
        };

        private static void TryPlace(EzLayoutContainer container, ISerialisableDrawable component) =>
            container.TryPlace(component, applyDefaults: false);
    }
}
