// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Screens.Play
{
    public static class EzFlowMode
    {
        public static bool IsEnabled =>
            GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.FlowMode);

        public static bool ShouldSkipResults(IScreen from)
        {
            if (!IsEnabled)
                return false;

            if (EzQuickRotationCoordinator.Session.IsActive)
                return false;

            for (var screen = from; screen != null; screen = screen.GetParentScreen())
            {
                if (screen is SoloSongSelect)
                    return true;
            }

            return false;
        }

        public static void ReturnToSongSelect(OsuGame? game) =>
            game?.PerformFromScreen(_ => { }, new[] { typeof(SoloSongSelect) });
    }
}
