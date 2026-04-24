// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzAnalysisPersistentStoreTest
    {
        [Test]
        public void TestMissingDataRequiresWholeAnalysisWhenStoredResultIsMissing()
        {
            var missing = EzAnalysisPersistentStore.GetMissingData(null, 3, requireTagData: true);

            Assert.That(missing & EzAnalysisPersistentStore.MissingDataKind.Common, Is.Not.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(missing & EzAnalysisPersistentStore.MissingDataKind.Pp, Is.Not.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(missing & EzAnalysisPersistentStore.MissingDataKind.Tag, Is.Not.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(missing & EzAnalysisPersistentStore.MissingDataKind.Mania, Is.Not.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(EzAnalysisPersistentStore.RequiresAnalysisComputation(missing), Is.True);
        }

        [Test]
        public void TestMissingDataTracksPpAndTagIndependently()
        {
            var stored = new EzAnalysisResult(new KpsSummary(1, 2, Array.Empty<double>()));

            var missing = EzAnalysisPersistentStore.GetMissingData(stored, 0, requireTagData: true);

            Assert.That(missing & EzAnalysisPersistentStore.MissingDataKind.Pp, Is.Not.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(missing & EzAnalysisPersistentStore.MissingDataKind.Tag, Is.Not.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(missing & EzAnalysisPersistentStore.MissingDataKind.Mania, Is.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(EzAnalysisPersistentStore.RequiresAnalysisComputation(missing), Is.True);
        }

        [Test]
        public void TestMissingDataIgnoresTagWhenNotRequested()
        {
            var stored = new EzAnalysisResult(new KpsSummary(1, 2, Array.Empty<double>()), 123.45);

            var missing = EzAnalysisPersistentStore.GetMissingData(stored, 0, requireTagData: false);

            Assert.That(missing, Is.EqualTo(EzAnalysisPersistentStore.MissingDataKind.None));
            Assert.That(EzAnalysisPersistentStore.RequiresAnalysisComputation(missing), Is.False);
        }

        [Test]
        public void TestMissingDataRequiresManiaSliceOnlyForManiaRulesets()
        {
            var stored = new EzAnalysisResult(new KpsSummary(1, 2, Array.Empty<double>()), 123.45, tagSummary: EzBeatmapTagSummary.EMPTY);

            var missing = EzAnalysisPersistentStore.GetMissingData(stored, 3, requireTagData: true);

            Assert.That(missing, Is.EqualTo(EzAnalysisPersistentStore.MissingDataKind.Mania));
            Assert.That(EzAnalysisPersistentStore.RequiresAnalysisComputation(missing), Is.True);
        }
    }
}
