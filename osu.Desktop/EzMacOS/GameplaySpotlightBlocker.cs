// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.Versioning;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK.Input;
using osu.Game.Screens.Play;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Desktop.EzMacOS
{
    [SupportedOSPlatform("macos")]
    public partial class GameplaySpotlightBlocker : Drawable
    {
        private Bindable<bool> disableCmdSpace = null!;
        private IBindable<LocalUserPlayingState> localUserPlaying = null!;
        private IBindable<bool> isActive = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(ILocalUserPlayInfo localUserInfo, Ez2ConfigManager ezConfig)
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;

            localUserPlaying = localUserInfo.PlayingState.GetBoundCopy();
            localUserPlaying.BindValueChanged(_ => updateBlocking());

            isActive = host.IsActive.GetBoundCopy();
            isActive.BindValueChanged(_ => updateBlocking());

            disableCmdSpace = ezConfig.GetBindable<bool>(Ez2Setting.GameplayDisableCmdSpace);
            disableCmdSpace.BindValueChanged(_ => updateBlocking(), true);
        }

        private void updateBlocking()
        {
            // Block during active gameplay, including breaks; only allow when NotPlaying.
            bool shouldDisable = isActive.Value && disableCmdSpace.Value && localUserPlaying.Value != LocalUserPlayingState.NotPlaying;

            if (shouldDisable)
                host.InputThread.Scheduler.Add(SpotlightKey.Disable);
            else
                host.InputThread.Scheduler.Add(SpotlightKey.Enable);
        }

        public override bool HandleNonPositionalInput => true;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // As a fallback for "read input" only, swallow Space when Command is held
            // to avoid triggering in-game actions while Spotlight is opening.
            bool shouldDisable = isActive.Value && disableCmdSpace.Value && localUserPlaying.Value != LocalUserPlayingState.NotPlaying;
            if (shouldDisable && e.Key == Key.Space && e.SuperPressed)
                return true; // handled: don't propagate Space to game

            return base.OnKeyDown(e);
        }
    }
}
