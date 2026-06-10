// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Appearance scene: gameplay viewport in the scene content region plus bottom playback controls.
    /// </summary>
    public partial class EzSkinEditorAppearanceSceneContent : Container
    {
        private const float playback_controls_height = 48;

        private readonly EzSkinEditorSceneContext context;

        private EzSkinEditorPlaybackControls playbackControls = null!;
        private Container playerViewport = null!;

        private double lastProgressDisplayTime = double.MinValue;

        private Bindable<EzBeatmapPreviewMode>? previewModeBindable;

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
                        playerViewport = new Container { RelativeSizeAxes = Axes.Both, Masking = true },
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

            bindPreviewMode();
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
            refreshControls();
        }

        protected override void Dispose(bool isDisposing)
        {
            previewModeBindable?.UnbindAll();
            base.Dispose(isDisposing);
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

        private void showPlaceholder()
        {
            playerViewport.Child = createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED);
            playbackControls.Alpha = 0;
        }

        private void refreshControls()
        {
            var player = context.GetEmbeddedPlayer?.Invoke();

            if (player == null)
            {
                showPlaceholder();
                return;
            }

            playbackControls.Alpha = 1;
            playbackControls.SetRange(player.BeatmapMinTime, player.BeatmapMaxTime);
            playbackControls.SetCurrentTime(player.GameplayClock.CurrentTime);
            playbackControls.SetPlaying(context.PreviewState?.Mode.Value == EzBeatmapPreviewMode.Dynamic);
        }

        private void bindPreviewMode()
        {
            previewModeBindable?.UnbindAll();

            if (context.PreviewState == null)
                return;

            previewModeBindable = context.PreviewState.Mode.GetBoundCopy();
            previewModeBindable.BindValueChanged(mode =>
            {
                bool playing = mode.NewValue == EzBeatmapPreviewMode.Dynamic;
                context.GetEmbeddedPlayer?.Invoke()?.SetPlaying(playing);
                playbackControls.SetPlaying(playing);
            }, true);
        }

        private void seekTo(double time)
        {
            context.GetEmbeddedPlayer?.Invoke()?.Seek(time);
            playbackControls.SetCurrentTime(time, seeking: true);
        }

        private void setPlaying(bool playing)
        {
            if (playing)
                context.PreviewState?.ResumeBeatmapPlayback();
            else
                context.PreviewState?.PauseBeatmapPlayback();
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
