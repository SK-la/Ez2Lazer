// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Screens.Visualizer;
using osu.Game.Overlays.Music;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.HUD.MusicPlayer
{
    public partial class EzVisualizerPlaylist : CompositeDrawable, ISerialisableDrawable, IEzSceneHudComponent
    {
        public const float WIDTH = 400;

        public bool UsesFixedAnchor { get; set; }

        public GlobalSkinnableContainers LayoutTarget => GlobalSkinnableContainers.MusicPlayer;

        private PlaylistOverlay playlist = null!;

        [Resolved(canBeNull: true)]
        private EzVisualizerScreen? visualizerScreen { get; set; }

        public EzVisualizerPlaylist()
        {
            Size = new Vector2(WIDTH, PlaylistOverlay.PLAYLIST_HEIGHT);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = playlist = new PlaylistOverlay
            {
                RelativeSizeAxes = Axes.Both,
            };

            visualizerScreen?.AttachPlaylist(playlist);
        }
    }
}
