// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Overlays
{
    public static class EzNotificationFilter
    {
        public static bool ShouldSuppress(OsuGame? game)
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
