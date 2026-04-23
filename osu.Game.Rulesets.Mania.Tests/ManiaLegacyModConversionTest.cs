// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaLegacyModConversionTest : LegacyModConversionTest
    {
        private static readonly object[][] mania_mod_mapping =
        {
            new object[] { LegacyMods.NoFail, new[] { typeof(ManiaModNoFail) } },
            new object[] { LegacyMods.Easy, new[] { typeof(ManiaModEasy) } },
            new object[] { LegacyMods.Hidden, new[] { typeof(ManiaModHidden) } },
            new object[] { LegacyMods.HardRock, new[] { typeof(ManiaModHardRock) } },
            new object[] { LegacyMods.SuddenDeath, new[] { typeof(ManiaModSuddenDeath) } },
            new object[] { LegacyMods.DoubleTime, new[] { typeof(ManiaModDoubleTime) } },
            new object[] { LegacyMods.HalfTime, new[] { typeof(ManiaModHalfTime) } },
            new object[] { LegacyMods.Flashlight, new[] { typeof(ManiaModFlashlight) } },
            new object[] { LegacyMods.Autoplay, new[] { typeof(ManiaModAutoplay) } },
            new object[] { LegacyMods.Key4, new[] { typeof(ManiaModKey4) } },
            new object[] { LegacyMods.Key5, new[] { typeof(ManiaModKey5) } },
            new object[] { LegacyMods.Key6, new[] { typeof(ManiaModKey6) } },
            new object[] { LegacyMods.Key7, new[] { typeof(ManiaModKey7) } },
            new object[] { LegacyMods.Key8, new[] { typeof(ManiaModKey8) } },
            new object[] { LegacyMods.FadeIn, new[] { typeof(ManiaModFadeIn) } },
            new object[] { LegacyMods.Random, new[] { typeof(ManiaModRandom) } },
            new object[] { LegacyMods.Key9, new[] { typeof(ManiaModKey9) } },
            new object[] { LegacyMods.KeyCoop, new[] { typeof(ManiaModDualStages) } },
            new object[] { LegacyMods.Key1, new[] { typeof(ManiaModKey1) } },
            new object[] { LegacyMods.Key3, new[] { typeof(ManiaModKey3) } },
            new object[] { LegacyMods.Key2, new[] { typeof(ManiaModKey2) } },
            new object[] { LegacyMods.Mirror, new[] { typeof(ManiaModMirror) } },
            new object[] { LegacyMods.HardRock | LegacyMods.DoubleTime, new[] { typeof(ManiaModHardRock), typeof(ManiaModDoubleTime) } },
            new object[] { LegacyMods.ScoreV2, new[] { typeof(ManiaModScoreV2) } },
        };

        [TestCaseSource(nameof(mania_mod_mapping))]
        [TestCase(LegacyMods.Cinema, new[] { typeof(ManiaModCinema) })]
        [TestCase(LegacyMods.Cinema | LegacyMods.Autoplay, new[] { typeof(ManiaModCinema) })]
        [TestCase(LegacyMods.Nightcore, new[] { typeof(ManiaModNightcore) })]
        [TestCase(LegacyMods.Nightcore | LegacyMods.DoubleTime, new[] { typeof(ManiaModNightcore) })]
        [TestCase(LegacyMods.Perfect, new[] { typeof(ManiaModPerfect) })]
        [TestCase(LegacyMods.Perfect | LegacyMods.SuddenDeath, new[] { typeof(ManiaModPerfect) })]
        public new void TestFromLegacy(LegacyMods legacyMods, Type[] expectedMods) => base.TestFromLegacy(legacyMods, expectedMods);

        [TestCaseSource(nameof(mania_mod_mapping))]
        [TestCase(LegacyMods.Cinema | LegacyMods.Autoplay, new[] { typeof(ManiaModCinema) })]
        [TestCase(LegacyMods.Nightcore | LegacyMods.DoubleTime, new[] { typeof(ManiaModNightcore) })]
        [TestCase(LegacyMods.Perfect | LegacyMods.SuddenDeath, new[] { typeof(ManiaModPerfect) })]
        public new void TestToLegacy(LegacyMods legacyMods, Type[] givenMods) => base.TestToLegacy(legacyMods, givenMods);

        [Test]
        public void TestConversionModsPreferSingleStageKeyRange()
        {
            var ruleset = (ManiaRuleset)CreateRuleset();
            var conversionMods = ruleset.GetModsFor(ModType.Conversion).ToList();

            Assert.That(conversionMods.Any(mod => mod is ManiaModDualStages), Is.False);

            var keyModGroup = conversionMods.OfType<MultiMod>().Single(mod => mod.Mods.OfType<ManiaKeyMod>().Any());

            Assert.That(keyModGroup.Mods.OfType<ManiaKeyMod>().Select(mod => mod.KeyCount), Is.EqualTo(Enumerable.Range(1, ManiaRuleset.MAX_STAGE_KEYS)));
        }

        [TestCase(12)]
        [TestCase(14)]
        [TestCase(16)]
        [TestCase(18)]
        public void TestHigherSingleStageKeyModsAdjustConvertedKeyCount(int keyCount)
        {
            var ruleset = (ManiaRuleset)CreateRuleset();
            var beatmapInfo = new BeatmapInfo(new RulesetInfo { ShortName = "osu" }, new BeatmapDifficulty { CircleSize = 4, OverallDifficulty = 4 });

            var keyMod = ruleset.GetModsFor(ModType.Conversion)
                                .OfType<MultiMod>()
                                .SelectMany(mod => mod.Mods)
                                .OfType<ManiaKeyMod>()
                                .Single(mod => mod.KeyCount == keyCount);

            Assert.That(ruleset.GetKeyCount(beatmapInfo, new[] { keyMod }), Is.EqualTo(keyCount));
        }

        protected override Ruleset CreateRuleset() => new ManiaRuleset();
    }
}
