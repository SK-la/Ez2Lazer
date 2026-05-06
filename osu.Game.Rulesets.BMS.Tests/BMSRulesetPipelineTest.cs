// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Scoring;
using osu.Game.Rulesets.BMS.UI;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSRulesetPipelineTest
    {
        [Test]
        public void TestCreateDrawableRulesetUsesBmsDrawablePipeline()
        {
            var ruleset = new BMSRuleset();
            var beatmap = ruleset.CreateBeatmapConverter(new Beatmap()).Convert();

            var drawableRuleset = ruleset.CreateDrawableRulesetWith(beatmap);

            Assert.That(drawableRuleset, Is.TypeOf<DrawableBMSRuleset>());
        }

        [Test]
        public void TestCreateProcessorsReturnsBmsSpecificTypes()
        {
            var ruleset = new BMSRuleset();

            Assert.That(ruleset.CreateScoreProcessor(), Is.TypeOf<BMSScoreProcessor>());
            Assert.That(ruleset.CreateHealthProcessor(0), Is.TypeOf<DrainingHealthProcessor>());
        }

        [Test]
        public void TestSevenKeyVariantContainsScratchBinding()
        {
            var ruleset = new BMSRuleset();

            var bindings = ruleset.GetDefaultKeyBindings(7).ToList();

            Assert.That(bindings.Count, Is.EqualTo(8));
            Assert.That(bindings.Any(b => b.Action.ToString() == "Scratch1"), Is.True);
        }
    }
}
