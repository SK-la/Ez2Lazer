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
    /// Appearance scene foreground: transparent overlay with bottom playback controls bound to the embedded player.
    /// </summary>
    public partial class EzSkinEditorAppearanceSceneContent : Container
    {
        private const float playback_controls_height = 48;

        private readonly EzSkinEditorSceneContext context;

        private EzSkinEditorPlaybackControls playbackControls = null!;
        private Container placeholderContainer = null!;

        private double lastProgressDisplayTime = double.MinValue;

        public EzSkinEditorAppearanceSceneContent(EzSkinEditorSceneContext context)
        {
            this.context = context;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 1),
                    new Dimension(GridSizeMode.Absolute, playback_controls_height),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        placeholderContainer = new Container { RelativeSizeAxes = Axes.Both },
                    },
                    new Drawable[]
                    {
                        playbackControls = new EzSkinEditorPlaybackControls
                        {
                            OnSeek = seekTo,
                            OnPlayStateChanged = setPlaying,
                        },
                    },
                },
            };

            refreshControls();
        }

        protected override void Update()
        {
            base.Update();

            var player = context.GetEmbeddedPlayer?.Invoke();

            if (player == null)
                return;

            if (player.GameplayClock.IsRunning && player.GameplayClock.CurrentTime - lastProgressDisplayTime >= 16)
            {
                playbackControls.SetCurrentTime(player.GameplayClock.CurrentTime);
                lastProgressDisplayTime = player.GameplayClock.CurrentTime;
            }
        }

        public void RefreshFromContext(EzSkinEditorSceneContext newContext)
        {
            context.GetEmbeddedPlayer = newContext.GetEmbeddedPlayer;
            refreshControls();
        }

        private void refreshControls()
        {
            var player = context.GetEmbeddedPlayer?.Invoke();

            if (player == null)
            {
                placeholderContainer.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED);
                playbackControls.Alpha = 0;
                return;
            }

            placeholderContainer.Clear();
            playbackControls.Alpha = 1;
            playbackControls.SetRange(player.BeatmapMinTime, player.BeatmapMaxTime);
            playbackControls.SetCurrentTime(player.GameplayClock.CurrentTime);
            playbackControls.SetPlaying(player.GameplayClock.IsRunning);
        }

        private void seekTo(double time)
        {
            context.GetEmbeddedPlayer?.Invoke()?.Seek(time);
            playbackControls.SetCurrentTime(time, seeking: true);
        }

        private void setPlaying(bool playing)
        {
            context.GetEmbeddedPlayer?.Invoke()?.SetPlaying(playing);
            playbackControls.SetPlaying(playing);
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
