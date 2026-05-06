// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsKeysoundManagerLifecycleTest
    {
        [Test]
        public void TestDisposedManagerGuardsAllPublicOperations()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();

            Assert.DoesNotThrow(() => manager.Dispose());
            Assert.That(manager.IsDisposed, Is.True);
            Assert.DoesNotThrow(() => manager.Dispose());

            Assert.DoesNotThrow(() => manager.SetOffset(12));
            Assert.DoesNotThrow(() => manager.SetVolume(0.5));
            Assert.DoesNotThrow(() => manager.SetBackgroundSoundEvents(new List<BmsBackgroundSoundEvent>
            {
                new BmsBackgroundSoundEvent(100, "a.wav"),
            }));
            Assert.DoesNotThrow(() => manager.Update(1000));
            Assert.DoesNotThrow(() => manager.TriggerKeysound("a.wav"));
            Assert.DoesNotThrow(() => manager.LoadKeysound("a.wav"));
            Assert.DoesNotThrow(() => manager.PreloadKeysounds(Array.Empty<osu.Game.Rulesets.Objects.HitObject>()));
        }
    }
}
