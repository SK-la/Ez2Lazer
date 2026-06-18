// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    public enum EzScoreRacePlayMode
    {
        LocalLive,
        LocalReplayChase,
        SpectatingLive,
    }

    internal static class EzScoreRacePlayModeResolver
    {
        public static EzScoreRacePlayMode Resolve(Player player)
        {
            switch (player)
            {
                case ReplayPlayer:
                    return EzScoreRacePlayMode.LocalReplayChase;

                case SpectatorPlayer:
                    return EzScoreRacePlayMode.SpectatingLive;

                default:
                    return EzScoreRacePlayMode.LocalLive;
            }
        }
    }
}
