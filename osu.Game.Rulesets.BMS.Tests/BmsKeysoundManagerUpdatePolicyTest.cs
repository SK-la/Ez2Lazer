// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsKeysoundManagerUpdatePolicyTest
    {
        [Test]
        public void TestUpdateCapsTriggeredEventsPerFrame()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();
            var events = new List<BmsBackgroundSoundEvent>();

            for (int i = 0; i < 100; i++)
                events.Add(new BmsBackgroundSoundEvent(0, string.Empty));

            manager.SetBackgroundSoundEvents(events);
            manager.Update(0);

            int nextIndex = TestReflectionHelpers.GetField<int>(manager, "nextBackgroundIndex");
            Assert.That(nextIndex, Is.EqualTo(32), "update should cap background triggers to 32 per frame");
        }

        [Test]
        public void TestUpdateDropsStaleEvents()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();

            manager.SetBackgroundSoundEvents(new List<BmsBackgroundSoundEvent>
            {
                new BmsBackgroundSoundEvent(0, string.Empty),
                new BmsBackgroundSoundEvent(50, string.Empty),
                new BmsBackgroundSoundEvent(100, string.Empty),
            });

            manager.Update(1000);

            int nextIndex = TestReflectionHelpers.GetField<int>(manager, "nextBackgroundIndex");
            Assert.That(nextIndex, Is.EqualTo(3), "all events should be discarded as stale when far behind current time");
        }

        [Test]
        public void TestUpdateRewindResetsIndexFromCurrentTime()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();

            manager.SetBackgroundSoundEvents(new List<BmsBackgroundSoundEvent>
            {
                new BmsBackgroundSoundEvent(100, string.Empty),
                new BmsBackgroundSoundEvent(200, string.Empty),
                new BmsBackgroundSoundEvent(300, string.Empty),
            });

            manager.Update(350);
            manager.Update(150);

            int nextIndex = TestReflectionHelpers.GetField<int>(manager, "nextBackgroundIndex");
            Assert.That(nextIndex, Is.EqualTo(1), "rewind should reposition index based on current time and then progress");
        }
    }
}
