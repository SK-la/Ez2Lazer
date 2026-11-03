// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzOsuGame.Startup;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public partial class BackgroundDataStoreStartupDelayTest
    {
        [Test]
        public void ProductionStartupBackfillDelayUsesEzStartupTuningDefault()
        {
            var probe = new ProbeBackgroundDataStoreProcessor();
            Assert.That(probe.ExposedStartupBackfillDelay, Is.EqualTo(TimeSpan.FromSeconds(EzStartupTuning.BdspStartupBackfillDelaySeconds)));
        }

        private partial class ProbeBackgroundDataStoreProcessor : BackgroundDataStoreProcessor
        {
            public TimeSpan ExposedStartupBackfillDelay => StartupBackfillDelay;
        }
    }
}
