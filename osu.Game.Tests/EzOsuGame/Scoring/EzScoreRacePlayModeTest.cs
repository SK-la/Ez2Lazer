// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreRacePlayModeTest
    {
        [Test]
        public void TestReplayPlayerResolvesToLocalReplayChase()
        {
            var replayPlayer = new ReplayPlayer(new Score());
            Assert.That(EzScoreRacePlayModeResolver.Resolve(replayPlayer), Is.EqualTo(EzScoreRacePlayMode.LocalReplayChase));
        }

        [Test]
        public void TestSoloPlayerResolvesToLocalLive()
        {
            var soloPlayer = new SoloPlayer();
            Assert.That(EzScoreRacePlayModeResolver.Resolve(soloPlayer), Is.EqualTo(EzScoreRacePlayMode.LocalLive));
        }

        [Test]
        public void TestSpectatorPlayerResolvesToSpectatingLive()
        {
            var spectatorPlayer = new SoloSpectatorPlayer(new Score());
            Assert.That(EzScoreRacePlayModeResolver.Resolve(spectatorPlayer), Is.EqualTo(EzScoreRacePlayMode.SpectatingLive));
        }
    }
}
