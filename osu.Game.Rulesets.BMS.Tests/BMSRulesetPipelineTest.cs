// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Scoring;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSRulesetPipelineTest
    {
        [Test]
        public void TestCreateProcessorsReturnsBmsSpecificTypes()
        {
            var ruleset = new BMSRuleset();

            Assert.That(ruleset.CreateScoreProcessor(), Is.TypeOf<BMSScoreProcessor>());
            Assert.That(ruleset.CreateHealthProcessor(0), Is.TypeOf<BMSNoFailHealthProcessor>());
        }

        [Test]
        public void TestVariantUsesGivenLaneCount()
        {
            var ruleset = new BMSRuleset();

            var bindings = ruleset.GetDefaultKeyBindings(12).ToList();

            Assert.That(bindings.Count, Is.EqualTo(12));
            Assert.That(bindings.Any(b => b.Action.ToString() == "Key12"), Is.True);
        }
    }
}
