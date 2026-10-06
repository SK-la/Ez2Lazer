// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Database;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Tests.EzOsuGame.Database
{
    [TestFixture]
    public class EzScoreScanStampTest
    {
        [Test]
        public void WriteThenMatchesSameSnapshot()
        {
            using var storage = new TemporaryNativeStorage($"{nameof(EzScoreScanStampTest)}-{nameof(WriteThenMatchesSameSnapshot)}");
            var snapshot = new EzScoreScanStamp.Snapshot(ScoreCount: 12, FailedCount: 1);

            EzScoreScanStamp.Write(storage, snapshot);

            Assert.That(EzScoreScanStamp.Matches(storage, snapshot), Is.True);
        }

        [Test]
        public void MismatchWhenScoreCountChanges()
        {
            using var storage = new TemporaryNativeStorage($"{nameof(EzScoreScanStampTest)}-{nameof(MismatchWhenScoreCountChanges)}");
            EzScoreScanStamp.Write(storage, new EzScoreScanStamp.Snapshot(10, 0));

            Assert.That(EzScoreScanStamp.Matches(storage, new EzScoreScanStamp.Snapshot(11, 0)), Is.False);
        }

        [Test]
        public void InvalidateClearsStamp()
        {
            using var storage = new TemporaryNativeStorage($"{nameof(EzScoreScanStampTest)}-{nameof(InvalidateClearsStamp)}");
            var snapshot = new EzScoreScanStamp.Snapshot(3, 0);
            EzScoreScanStamp.Write(storage, snapshot);

            EzScoreScanStamp.Invalidate(storage);

            Assert.That(EzScoreScanStamp.Matches(storage, snapshot), Is.False);
        }

        [Test]
        public void GateVersionsMatchEncoderLatest()
        {
            Assert.That(EzScoreScanStamp.MOD_MULTIPLIER_GATE_VERSION, Is.LessThanOrEqualTo(LegacyScoreEncoder.LATEST_VERSION));
            Assert.That(EzScoreScanStamp.RANK_GATE_VERSION, Is.LessThanOrEqualTo(LegacyScoreEncoder.LATEST_VERSION));
        }
    }
}
