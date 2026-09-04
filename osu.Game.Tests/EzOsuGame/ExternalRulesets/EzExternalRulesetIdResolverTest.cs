// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.ExternalRulesets;

namespace osu.Game.Tests.EzOsuGame.ExternalRulesets
{
    [TestFixture]
    public class EzExternalRulesetIdResolverTest
    {
        [Test]
        public void TestExplicitIdConflictRemapsLaterRuleset()
        {
            var inputs = new List<ExternalRulesetMappingInput>
            {
                new ExternalRulesetMappingInput("first", 5, true, 5, 0, 0),
                new ExternalRulesetMappingInput("second", 5, true, 5, 1, 1),
            };

            var outputs = EzExternalRulesetIdResolver.Resolve(inputs, new HashSet<int> { 0, 1, 2, 3 });

            Assert.That(outputs[0].ResolvedOnlineId, Is.EqualTo(5));
            Assert.That(outputs[0].WasRemapped, Is.False);
            Assert.That(outputs[1].ResolvedOnlineId, Is.EqualTo(4));
            Assert.That(outputs[1].WasRemapped, Is.True);
        }

        [Test]
        public void TestUndefinedIdRulesetsStayUntouched()
        {
            var inputs = new List<ExternalRulesetMappingInput>
            {
                new ExternalRulesetMappingInput("a", -1, true, null, int.MaxValue, 0),
                new ExternalRulesetMappingInput("b", -1, true, null, int.MaxValue, 1),
            };

            var outputs = EzExternalRulesetIdResolver.Resolve(inputs, new HashSet<int> { 0, 1, 2, 3 });

            Assert.That(outputs.All(o => o.ResolvedOnlineId == -1));
            Assert.That(outputs.All(o => !o.ParticipatesInMapping));
            Assert.That(outputs.All(o => !o.WasRemapped));
        }

        [Test]
        public void TestUndefinedIdDoesNotConflictWithExplicitId()
        {
            var inputs = new List<ExternalRulesetMappingInput>
            {
                new ExternalRulesetMappingInput("undefined", -1, true, null, int.MaxValue, 0),
                new ExternalRulesetMappingInput("explicit", 5, true, 5, 0, 1),
            };

            var outputs = EzExternalRulesetIdResolver.Resolve(inputs, new HashSet<int> { 0, 1, 2, 3 });

            Assert.That(outputs.Single(o => o.ShortName == "undefined").ResolvedOnlineId, Is.EqualTo(-1));
            Assert.That(outputs.Single(o => o.ShortName == "explicit").ResolvedOnlineId, Is.EqualTo(5));
        }

        [Test]
        public void TestDisabledRulesetSkipped()
        {
            var inputs = new List<ExternalRulesetMappingInput>
            {
                new ExternalRulesetMappingInput("disabled", 5, false, 5, 0, 0),
            };

            var outputs = EzExternalRulesetIdResolver.Resolve(inputs, new HashSet<int> { 0, 1, 2, 3 });

            Assert.That(outputs[0].Enabled, Is.False);
            Assert.That(outputs[0].ParticipatesInMapping, Is.False);
        }
    }
}
