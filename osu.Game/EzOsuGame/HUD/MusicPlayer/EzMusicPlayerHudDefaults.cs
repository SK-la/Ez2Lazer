// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
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

        private static readonly Type[] default_types =
        {
            typeof(EzMenuLogoVisualisation),
            typeof(EzVisualizerBeatmapLogo),
            typeof(EzVisualizerPlaybackBar),
            typeof(EzVisualizerPlaylist),
        };

        /// <summary>
        /// Fills an empty loaded container with the built-in music player HUD. Must run after
        /// <see cref="SkinnableContainer.ComponentsLoaded"/>: calling it during <c>Reload</c> adds
        /// the same instances to <see cref="SkinnableContainer.Components"/> twice, which serialises
        /// as extra copies and crashes undo with "drawable in multiple containers".
        /// </summary>
        public static void ApplyIfEmpty(EzLayoutContainer container)
        {
            if (!container.ComponentsLoaded)
                return;

            TrimExactDuplicatedDefaultSet(container);

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

        /// <summary>
        /// Repairs the serialisation bug that wrote N identical default sets. Leaves any other layout
        /// alone, including an extra visualisation or playlist the user placed.
        /// </summary>
        public static void TrimExactDuplicatedDefaultSet(EzLayoutContainer container)
        {
            var components = container.Components;
            int[] counts = default_types.Select(t => components.Count(c => c.GetType() == t)).ToArray();

            if (counts.Any(c => c <= 1) || counts.Any(c => c != counts[0]))
                return;

            if (components.Count != counts[0] * default_types.Length)
                return;

            foreach (var type in default_types)
            {
                foreach (var extra in components.Where(c => c.GetType() == type).Skip(1).ToArray())
                    container.Remove(extra, true);
            }
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
