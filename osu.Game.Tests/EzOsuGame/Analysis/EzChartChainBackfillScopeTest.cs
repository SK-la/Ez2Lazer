// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzChartChainBackfillScopeTest
    {
        [Test]
        public void Mina_also_runs_csi_and_dan()
        {
            EzChartChainBackfillScope.Resolve(EzAnalysisSystem.Mina, out bool msd, out bool csi, out bool dan, out _, out bool needs);

            Assert.That(msd, Is.True);
            Assert.That(csi, Is.True);
            Assert.That(dan, Is.True);
            Assert.That(needs, Is.True);
        }

        [Test]
        public void Sunny_also_runs_dan()
        {
            EzChartChainBackfillScope.Resolve(EzAnalysisSystem.SSR, out bool msd, out bool csi, out bool dan, out _, out bool needs);

            Assert.That(msd, Is.False);
            Assert.That(csi, Is.False);
            Assert.That(dan, Is.True);
            Assert.That(needs, Is.True);
        }

        [Test]
        public void Mina_and_sunny_run_full_chain()
        {
            var scope = EzAnalysisSystem.Mina | EzAnalysisSystem.SSR;
            EzChartChainBackfillScope.Resolve(scope, out bool msd, out bool csi, out bool dan, out _, out _);

            Assert.That(msd, Is.True);
            Assert.That(csi, Is.True);
            Assert.That(dan, Is.True);
        }
    }
}