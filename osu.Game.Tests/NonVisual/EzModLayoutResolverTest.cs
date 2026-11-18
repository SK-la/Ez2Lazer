// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzModLayoutResolverTest
    {
        [Test]
        public void TestMissingSaveUsesTypeOrder()
        {
            var layout = EzModLayoutResolver.Resolve(null, new[]
            {
                mod("FL", ModType.Fun),
                mod("NF", ModType.DifficultyReduction),
                mod("EZ", ModType.DifficultyReduction),
            });

            Assert.That(layout.Hidden!, Is.Empty);
            Assert.That(layout.Groups!.Select(group => group.SeedType).ToArray(), Is.EqualTo(new[]
            {
                nameof(ModType.DifficultyReduction),
                nameof(ModType.Fun),
            }));
            Assert.That(layout.Groups![0].Name, Is.EqualTo(EzModLayoutStrings.SeedGroupName(ModType.DifficultyReduction)));
            Assert.That(layout.Groups![0].Acronyms!, Is.EqualTo(new[] { "NF", "EZ" }));
            Assert.That(layout.Groups![1].Acronyms!, Is.EqualTo(new[] { "FL" }));
        }

        [Test]
        public void TestUnknownAcronymsAreDropped()
        {
            var saved = new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout
                    {
                        Id = "reduction",
                        Name = "Easy",
                        SeedType = nameof(ModType.DifficultyReduction),
                        Acronyms = new List<string> { "NOPE", "EZ" },
                    },
                },
                Hidden = new List<string> { "GONE", "NF" },
            };

            var layout = EzModLayoutResolver.Resolve(saved, new[]
            {
                mod("EZ", ModType.DifficultyReduction),
                mod("NF", ModType.DifficultyReduction),
            });

            Assert.That(layout.Groups![0].Acronyms!, Is.EqualTo(new[] { "EZ" }));
            Assert.That(layout.Hidden!, Is.EqualTo(new[] { "NF" }));
        }

        [Test]
        public void TestUnavailableAcronymsCanBePreserved()
        {
            var saved = new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout
                    {
                        Id = "reduction",
                        Name = "Easy",
                        SeedType = nameof(ModType.DifficultyReduction),
                        Acronyms = new List<string> { "OTHER", "EZ" },
                    },
                },
                Hidden = new List<string> { "OTHER-HIDDEN", "NF" },
            };

            var layout = EzModLayoutResolver.Resolve(saved, new[]
            {
                mod("EZ", ModType.DifficultyReduction),
                mod("NF", ModType.DifficultyReduction),
            }, preserveUnavailable: true);

            Assert.That(layout.Groups![0].Acronyms!, Is.EqualTo(new[] { "OTHER", "EZ" }));
            Assert.That(layout.Hidden!, Is.EqualTo(new[] { "OTHER-HIDDEN", "NF" }));
        }

        [Test]
        public void TestNewModJoinsMatchingSeedGroup()
        {
            var saved = new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout
                    {
                        Id = "reduction",
                        Name = "Easy",
                        SeedType = nameof(ModType.DifficultyReduction),
                        Acronyms = new List<string> { "EZ" },
                    },
                },
            };

            var layout = EzModLayoutResolver.Resolve(saved, new[]
            {
                mod("EZ", ModType.DifficultyReduction),
                mod("NF", ModType.DifficultyReduction),
            });

            Assert.That(layout.Groups!, Has.Count.EqualTo(1));
            Assert.That(layout.Groups![0].Acronyms!, Is.EqualTo(new[] { "EZ", "NF" }));
            Assert.That(layout.Hidden!, Is.Empty);
        }

        [Test]
        public void TestNewModStaysVisibleWhenSeedGroupIsGone()
        {
            var saved = new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout
                    {
                        Id = "fun",
                        Name = "Party",
                        SeedType = nameof(ModType.Fun),
                        Acronyms = new List<string> { "FL" },
                    },
                },
            };

            var layout = EzModLayoutResolver.Resolve(saved, new[]
            {
                mod("FL", ModType.Fun),
                mod("NF", ModType.DifficultyReduction),
            });

            Assert.That(layout.Hidden!, Is.Empty);
            Assert.That(layout.Groups!, Has.Count.EqualTo(1));
            Assert.That(layout.Groups![0].Acronyms!, Is.EqualTo(new[] { "FL", "NF" }));
        }

        [Test]
        public void TestHideDoesNotChangeSelection()
        {
            var selected = new HashSet<string> { "HD" };
            var layout = EzModLayoutResolver.Resolve(null, new[] { mod("HD", ModType.DifficultyIncrease) });

            EzModLayoutResolver.MoveMod(layout, "HD", null, 0);

            Assert.That(layout.Hidden!, Does.Contain("HD"));
            Assert.That(layout.Groups!.SelectMany(group => group.Acronyms!), Does.Not.Contain("HD"));
            Assert.That(selected, Does.Contain("HD"));
        }

        [Test]
        public void TestRemoveGroupMovesModsToHidden()
        {
            var layout = EzModLayoutResolver.Resolve(null, new[]
            {
                mod("EZ", ModType.DifficultyReduction),
                mod("HR", ModType.DifficultyIncrease),
            });

            string reductionId = layout.Groups!.Single(group => group.SeedType == nameof(ModType.DifficultyReduction)).Id;
            EzModLayoutResolver.RemoveGroup(layout, reductionId);

            Assert.That(layout.Groups!.SelectMany(group => group.Acronyms!), Is.EqualTo(new[] { "HR" }));
            Assert.That(layout.Hidden!, Is.EqualTo(new[] { "EZ" }));
        }

        [Test]
        public void TestNullListsAndStaleAcronymsDoNotThrow()
        {
            var saved = new EzRulesetModLayout
            {
                Groups = null!,
                Hidden = null!,
            };

            Assert.DoesNotThrow(() => EzModLayoutResolver.Resolve(saved, new[] { mod("EZ", ModType.DifficultyReduction) }));
        }

        [Test]
        public void TestRemovedModDroppedAndNewModPlaced()
        {
            var saved = new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout { Id = "g1", Name = "G", Acronyms = new List<string> { "OLD", "EZ" } },
                },
                Hidden = new List<string> { "REMOVED" },
            };

            var layout = EzModLayoutResolver.Resolve(saved, new[] { mod("EZ", ModType.DifficultyReduction), mod("NF", ModType.DifficultyReduction) });

            Assert.That(layout.Groups!.SelectMany(g => g.Acronyms!), Does.Not.Contain("OLD"));
            Assert.That(layout.Hidden!, Does.Not.Contain("REMOVED"));
            Assert.That(layout.Groups!.SelectMany(g => g.Acronyms!), Does.Contain("EZ"));
            Assert.That(layout.Groups!.SelectMany(g => g.Acronyms!), Does.Contain("NF"));
        }

        [Test]
        public void TestShearOnlySaveUsesTypeDefaultGroups()
        {
            var saved = new EzRulesetModLayout
            {
                ColumnShear = 0.1f,
            };

            var layout = EzModLayoutResolver.Resolve(saved, new[]
            {
                mod("FL", ModType.Fun),
                mod("NF", ModType.DifficultyReduction),
                mod("EZ", ModType.DifficultyReduction),
            });

            Assert.That(layout.Groups!.Select(group => group.SeedType).ToArray(), Is.EqualTo(new[]
            {
                nameof(ModType.DifficultyReduction),
                nameof(ModType.Fun),
            }));
            Assert.That(layout.ColumnShear, Is.EqualTo(0.1f));
        }

        [Test]
        public void TestColumnShearRoundTrip()
        {
            var saved = new EzRulesetModLayout
            {
                ColumnShear = 0.1f,
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout { Id = "fun", Name = "Fun", SeedType = nameof(ModType.Fun), Acronyms = new List<string> { "FL" } },
                },
            };

            var layout = EzModLayoutResolver.Resolve(saved, new[] { mod("FL", ModType.Fun) });

            Assert.That(layout.ColumnShear, Is.EqualTo(0.1f));
            Assert.That(EzModLayoutResolver.NormaliseColumnShear(null), Is.EqualTo(EzModLayoutResolver.DEFAULT_COLUMN_SHEAR));
            Assert.That(EzModLayoutResolver.NormaliseColumnShear(0.05f), Is.EqualTo(0.05f));
        }

        private static EzModLayoutMod mod(string acronym, ModType type) => new EzModLayoutMod(acronym, type);
    }
}
