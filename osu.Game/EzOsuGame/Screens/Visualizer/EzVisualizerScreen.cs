// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Music;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Visualizer
{
    [Cached]
    public partial class EzVisualizerScreen : ScreenWithBeatmapBackground, IKeyBindingHandler<GlobalAction>
    {
        public override bool HideOverlaysOnEnter => true;

        public override bool? AllowGlobalTrackControl => true;

        protected override bool InitialBackButtonVisibility => false;

        /// <summary>
        /// Slightly larger than the legacy back button so the cursor can move onto it without hiding.
        /// </summary>
        private static readonly Vector2 back_reveal_size = TwoLayerButton.SIZE_EXTENDED + new Vector2(48, 48);

        private const float playlist_width = 400;

        private readonly Bindable<WorkingBeatmap> beatmap = new Bindable<WorkingBeatmap>();

        private PlaylistOverlay playlist = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new EzVisualizerStage(),
                new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = playlist_width,
                    Height = PlaylistOverlay.PLAYLIST_HEIGHT,
                    Margin = new MarginPadding { Right = 24 },
                    Masking = true,
                    Child = playlist = new PlaylistOverlay
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindTo(Beatmap);
            beatmap.BindValueChanged(b =>
            {
                if (!this.IsCurrentScreen()) return;

                ApplyToBackground(bg => bg.Beatmap = b.NewValue);
            });
        }

        public void TogglePlaylist()
        {
            if (Beatmap.Disabled)
                return;

            playlist.ToggleVisibility();
        }

        public IBindable<Visibility> PlaylistState => playlist.State;

        protected override void Update()
        {
            base.Update();

            if (!this.IsCurrentScreen())
                return;

            var inputManager = GetContainingInputManager();
            if (inputManager == null)
                return;

            var mouse = inputManager.CurrentState.Mouse.Position;
            var quad = ScreenSpaceDrawQuad;

            bool nearBackCorner = mouse.X <= quad.TopLeft.X + back_reveal_size.X
                                  && mouse.Y >= quad.BottomLeft.Y - back_reveal_size.Y;

            if (BackButtonVisibility.Value != nearBackCorner)
                BackButtonVisibility.Value = nearBackCorner;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            ApplyToBackground(b =>
            {
                b.Beatmap = Beatmap.Value;
                b.IgnoreUserSettings.Value = false;
                b.DimWhenUserSettingsIgnored.Value = 0.3f;
                b.FadeColour(OsuColour.Gray(0.4f), 250, Easing.OutQuint);
            });

            this.FadeInFromZero(400, Easing.OutQuint);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            playlist.Hide();
            BackButtonVisibility.Value = false;

            this.FadeOut(250, Easing.OutQuint);

            ApplyToBackground(b => b.FadeColour(Color4.White, 250, Easing.OutQuint));

            return base.OnExiting(e);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            switch (e.Action)
            {
                case GlobalAction.Back:
                    if (playlist.State.Value == Visibility.Visible)
                    {
                        playlist.Hide();
                        return true;
                    }

                    this.Exit();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
