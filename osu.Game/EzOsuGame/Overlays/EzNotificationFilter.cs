// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Overlays;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Controls whether notification <em>presentation</em> (toast + sample + window flash) is muted.
    /// Notifications are never discarded — muted posts still enter the overlay history.
    /// </summary>
    public static class EzNotificationFilter
    {
        /// <summary>
        /// When true, <see cref="NotificationOverlay"/> must still accept the notification into
        /// permanent storage, but must not show a toast, play pop-in sound, or flash the window.
        /// </summary>
        public static bool ShouldMutePresentation(OsuGame? game)
        {
            switch (GlobalConfigStore.EzConfig.Get<EzNotificationBehaviour>(Ez2Setting.NotificationBehaviour))
            {
                case EzNotificationBehaviour.Never:
                    return true;

                case EzNotificationBehaviour.InGameFocus:
                    return isInGameplaySession(game);

                default:
                    return false;
            }
        }

        private static bool isInGameplaySession(OsuGame? game) =>
            game?.ScreenStack.CurrentScreen is Player or PlayerLoader;
    }
}
