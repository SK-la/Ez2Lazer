// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzRulesetReadoutLiveSkillStarTest
    {
        [Test]
        public void LiveSkillStar_reads_ssr_scalar_rating()
        {
            var analysis = new EzAnalysisResult(
                new KpsSummary(0, 0, null),
                ratings: new Dictionary<EzAnalysisSystem, EzScalarRating>
                {
                    [EzAnalysisSystem.SSR] = new EzScalarRating(6.2, null),
                });

            Assert.That(EzRulesetReadout.LiveSkillStar(EzAnalysisSystem.SSR, analysis), Is.EqualTo(6.2));
        }

        [Test]
        public void LiveSkillStar_reads_ppp_scalar_rating()
        {
            var analysis = new EzAnalysisResult(
                new KpsSummary(0, 0, null),
                ratings: new Dictionary<EzAnalysisSystem, EzScalarRating>
                {
                    [EzAnalysisSystem.PPP] = new EzScalarRating(4.8, null),
                });

            Assert.That(EzRulesetReadout.LiveSkillStar(EzAnalysisSystem.PPP, analysis), Is.EqualTo(4.8));
        }

        [Test]
        public void LiveSkillStar_reads_mina_overall_axis()
        {
            var analysis = new EzAnalysisResult(
                new KpsSummary(0, 0, null),
                axes: new Dictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>>
                {
                    [EzAnalysisSystem.Mina] = new Dictionary<string, double>
                    {
                        [EzMinaSkillAxis.Overall.ToId()] = 20,
                    },
                });

            double? star = EzRulesetReadout.LiveSkillStar(EzAnalysisSystem.Mina, analysis);

            Assert.That(star, Is.Not.Null);
            Assert.That(star!.Value, Is.GreaterThan(0));
        }
    }
}
