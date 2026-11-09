// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzOsuGame.Startup;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class DetachedBeatmapStoreSyncPolicyTest
    {
        [Test]
        public void Screen_transition_extends_mute_until()
        {
            var policy = new DetachedBeatmapStoreSyncPolicy();
            double muteMs = EzDetachedBeatmapStoreTuning.ScreenTransitionMuteMs;

            policy.NotifyScreenTransition(100);
            Assert.That(policy.AllowDrain(100 + muteMs - 1, null, null), Is.False);
            Assert.That(policy.AllowDrain(100 + muteMs, null, null), Is.True);
        }

        [Test]
        public void Overlapping_transitions_take_the_latest_mute_end()
        {
            var policy = new DetachedBeatmapStoreSyncPolicy();
            double muteMs = EzDetachedBeatmapStoreTuning.ScreenTransitionMuteMs;

            policy.NotifyScreenTransition(0);
            policy.NotifyScreenTransition(400);

            Assert.That(policy.AllowDrain(400 + muteMs - 1, null, null), Is.False);
            Assert.That(policy.AllowDrain(400 + muteMs, null, null), Is.True);
        }
    }
}
