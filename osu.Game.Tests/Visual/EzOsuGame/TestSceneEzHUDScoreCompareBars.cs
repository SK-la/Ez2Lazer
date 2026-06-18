// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Tests.Gameplay;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneEzHUDScoreCompareBars : OsuTestScene
    {
        [Cached]
        private readonly GameplayState gameplayState = TestGameplayState.Create(new OsuRuleset());

        [Cached(typeof(IGameplayClock))]
        private readonly IGameplayClock gameplayClock = new GameplayClockContainer(new osu.Framework.Audio.Track.TrackVirtual(60000), false, false);

        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly EmptyGameplayLeaderboardProvider leaderboardProvider = new EmptyGameplayLeaderboardProvider();

        public TestSceneEzHUDScoreCompareBars()
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new EzHUDScoreCompareBars
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }
    }
}
