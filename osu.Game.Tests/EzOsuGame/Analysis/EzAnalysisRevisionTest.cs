// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// One revision covers the whole analysis layer: every row of every system is stamped with it, so a bump
    /// retires all of them together and nothing has to be reasoned about per system.
    /// </summary>
    [TestFixture]
    public class EzAnalysisRevisionTest
    {
        [Test]
        public void Every_stage_shares_the_single_revision()
        {
            Assert.That(EzAnalysisRevision.Msd, Is.EqualTo(EzAnalysisRevision.VERSION));
            Assert.That(EzAnalysisRevision.ChartSkillInfo, Is.EqualTo(EzAnalysisRevision.VERSION));
            Assert.That(EzAnalysisRevision.ChartDan, Is.EqualTo(EzAnalysisRevision.VERSION));
            Assert.That(EzAnalysisRevision.Ppp, Is.EqualTo(EzAnalysisRevision.VERSION));
        }

        [Test]
        public void The_revision_is_positive()
        {
            Assert.That(EzAnalysisRevision.VERSION, Is.GreaterThan(0));
        }

        [Test]
        public void Descriptions_name_the_single_revision()
        {
            Assert.That(EzAnalysisRevision.DescribeChartSkillInfo(), Is.EqualTo($"v{EzAnalysisRevision.VERSION}"));
            Assert.That(EzAnalysisRevision.DescribeChartDan(), Is.EqualTo($"v{EzAnalysisRevision.VERSION}"));
        }
    }
}
