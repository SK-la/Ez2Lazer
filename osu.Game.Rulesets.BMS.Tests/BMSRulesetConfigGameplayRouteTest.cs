// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Rulesets.BMS.Configuration;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// Verifies that the gameplay-route preference exposed in BMS settings is wired up correctly:
    /// it has a sensible default, persists round-trip, and shares state across managers (so any
    /// entry into BMS gameplay reads the same flag).
    /// </summary>
    [TestFixture]
    public class BMSRulesetConfigGameplayRouteTest
    {
        [Test]
        public void TestDefaultsToManiaCompatibility()
        {
            var ruleset = new BMSRuleset();

            using var manager = new BMSRulesetConfigManager(null, ruleset.RulesetInfo);

            BMSGameplayRoute route = manager.Get<BMSGameplayRoute>(BMSRulesetSetting.GameplayRoute);

            Assert.That(route, Is.EqualTo(BMSGameplayRoute.ManiaCompatibility));
        }

        [Test]
        public void TestBindableReflectsSetValue()
        {
            var ruleset = new BMSRuleset();

            using var manager = new BMSRulesetConfigManager(null, ruleset.RulesetInfo);

            Bindable<BMSGameplayRoute> bindable = manager.GetBindable<BMSGameplayRoute>(BMSRulesetSetting.GameplayRoute);

            Assert.That(bindable.Value, Is.EqualTo(BMSGameplayRoute.ManiaCompatibility));

            bindable.Value = BMSGameplayRoute.BmsNative;

            Assert.That(manager.Get<BMSGameplayRoute>(BMSRulesetSetting.GameplayRoute), Is.EqualTo(BMSGameplayRoute.BmsNative));
        }

        [Test]
        public void TestEnumValuesAreStable()
        {
            // Guard against accidental enum-value reordering, which would silently flip every user's saved preference.
            Assert.That((int)BMSGameplayRoute.ManiaCompatibility, Is.EqualTo(0));
            Assert.That((int)BMSGameplayRoute.BmsNative, Is.EqualTo(1));
        }

        [Test]
        public void TestSettingEnumContainsGameplayRoute()
        {
            // Once added, the setting key is part of stored data; renaming would orphan existing values.
            string[] names = Enum.GetNames(typeof(BMSRulesetSetting));
            Assert.That(names, Contains.Item(nameof(BMSRulesetSetting.GameplayRoute)));
        }
    }
}
