// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzAnalysisSystemStorageTest
    {
        [Test]
        public void Pattern_round_trips_as_player_skill_tag()
        {
            Assert.That(EzAnalysisSystem.Pattern.ToStorageId(), Is.EqualTo("player_skill_tag"));
            Assert.That(EzAnalysisSystemStorage.FromStorageId("player_skill_tag"), Is.EqualTo(EzAnalysisSystem.Pattern));
        }
    }
}
