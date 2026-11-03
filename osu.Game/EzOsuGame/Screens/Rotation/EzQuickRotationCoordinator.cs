// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Game.EzOsuGame.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationCoordinator
    {
        public static EzQuickRotationSession Session { get; } = new EzQuickRotationSession();

        public static void NavigateAfterPlay(IScreen from)
        {
            if (!Session.IsActive)
                return;

            if (EzFlowMode.IsEnabled)
                from.Push(new EzQuickRotationTransitionScreen());
            else
                from.Push(new EzQuickRotationPickScreen());
        }

        public static void EndSession(OsuGame? game)
        {
            Session.End();
            game?.PerformFromScreen(_ => { }, new[] { typeof(SoloSongSelect) });
        }
    }
}
