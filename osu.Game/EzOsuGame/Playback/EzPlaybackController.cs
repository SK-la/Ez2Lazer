// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Playback
{
    /// <summary>
    /// Which clock a <see cref="EzPlaybackController"/> is currently driving.
    /// </summary>
    public enum EzPlaybackTarget
    {
        /// <summary>No controllable playback is available.</summary>
        None,

        /// <summary>The current gameplay screen's clock.</summary>
        Gameplay,

        /// <summary>The global music track (<see cref="MusicController"/>).</summary>
        Music,
    }

    /// <summary>
    /// Functional-layer transport shared by every playback UI (the editor progress bar, the visualizer ring).
    /// Resolves the current control target automatically: an active gameplay screen wins, otherwise the global
    /// music track. Exposes a read-only transport surface so components never touch clocks or tracks directly.
    /// </summary>
    public partial class EzPlaybackController : Component
    {
        private readonly Func<IScreen?> getCurrentScreen;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        public IBindable<EzPlaybackTarget> ActiveTarget => activeTarget;

        public IBindable<bool> IsActive => isActive;

        public IBindable<bool> IsPlaying => isPlaying;

        public IBindable<double> CurrentTime => currentTime;

        public IBindable<double> MinTime => minTime;

        public IBindable<double> MaxTime => maxTime;

        private readonly Bindable<EzPlaybackTarget> activeTarget = new Bindable<EzPlaybackTarget>(EzPlaybackTarget.None);
        private readonly BindableBool isActive = new BindableBool();
        private readonly BindableBool isPlaying = new BindableBool();
        private readonly BindableDouble currentTime = new BindableDouble();
        private readonly BindableDouble minTime = new BindableDouble();
        private readonly BindableDouble maxTime = new BindableDouble();

        private Player? boundPlayer;
        private GameplayClockContainer? boundClock;
        private bool controlsClockDirectly;

        /// <param name="getCurrentScreen">Retrieves the game's current screen; used to resolve the gameplay target.</param>
        public EzPlaybackController(Func<IScreen?> getCurrentScreen)
        {
            this.getCurrentScreen = getCurrentScreen;
        }

        protected override void Update()
        {
            base.Update();

            updateBoundTarget();
            updateTransport();
        }

        private void updateBoundTarget()
        {
            Player? player = getCurrentScreen() as Player;

            if (player?.IsLoaded != true)
            {
                boundPlayer = null;
                boundClock = null;
                controlsClockDirectly = false;
                return;
            }

            if (ReferenceEquals(boundPlayer, player))
                return;

            boundPlayer = player;
            boundClock = player.ChildrenOfType<GameplayClockContainer>().FirstOrDefault();

            // Replay-style players (including the editor's endless autoplay) cannot pause through the player
            // state machine, so their clock is driven directly.
            controlsClockDirectly = player is ReplayPlayer;
        }

        private void updateTransport()
        {
            if (boundPlayer != null && boundClock != null)
            {
                activeTarget.Value = EzPlaybackTarget.Gameplay;
                isActive.Value = true;
                isPlaying.Value = !boundClock.IsPaused.Value;
                minTime.Value = 0;
                maxTime.Value = Math.Max(boundPlayer.Beatmap.Value.BeatmapInfo.Length, 1);
                currentTime.Value = boundClock.CurrentTime;
                return;
            }

            var track = musicController.CurrentTrack;

            // A dummy device means the "track" is the virtual placeholder, not real music (mirrors MusicController).
            if (track.IsDummyDevice)
            {
                activeTarget.Value = EzPlaybackTarget.None;
                isActive.Value = false;
                isPlaying.Value = false;
                return;
            }

            activeTarget.Value = EzPlaybackTarget.Music;
            isActive.Value = true;
            isPlaying.Value = track.IsRunning;
            minTime.Value = 0;
            maxTime.Value = track.Length;
            currentTime.Value = track.CurrentTime;
        }

        public void Seek(double time)
        {
            if (boundClock != null)
            {
                boundClock.Seek(time);
                return;
            }

            musicController.SeekTo(time);
        }

        public void SetPlaying(bool playing)
        {
            if (boundPlayer != null && boundClock != null)
            {
                if (controlsClockDirectly)
                {
                    if (playing)
                        boundClock.Start();
                    else
                        boundClock.Stop();

                    return;
                }

                if (playing)
                    boundPlayer.Resume();
                else
                    boundPlayer.Pause();

                return;
            }

            if (playing)
                musicController.Play(requestedByUser: true);
            else
                musicController.Stop(requestedByUser: true);
        }

        public void TogglePlayback() => SetPlaying(!isPlaying.Value);
    }
}
