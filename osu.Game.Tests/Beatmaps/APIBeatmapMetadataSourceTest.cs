// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Moq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Online.API;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class APIBeatmapMetadataSourceTest
    {
        [Test]
        public void TestAvailableWhenOnline()
        {
            Assert.That(createSource(APIState.Online).Available, Is.True);
        }

        /// <summary>
        /// <see cref="APIState.LocalOnline"/> is an Ez-only state which upstream has no test for, so a merge could
        /// silently fold it back into the "online" branch. A local account cannot serve lookups, and pretending
        /// otherwise makes every beatmap pay for a request that is bound to fail.
        /// </summary>
        [Test]
        public void TestUnavailableWhenLocalOnline()
        {
            Assert.That(createSource(APIState.LocalOnline).Available, Is.False);
        }

        [Test]
        public void TestUnavailableWhenNotOnline([Values(APIState.Offline, APIState.Failing, APIState.Connecting)] APIState state)
        {
            Assert.That(createSource(state).Available, Is.False);
        }

        private static APIBeatmapMetadataSource createSource(APIState state)
        {
            var api = new Mock<IAPIProvider>();

            api.Setup(a => a.State).Returns(new Bindable<APIState>(state));

            return new APIBeatmapMetadataSource(api.Object);
        }
    }
}
