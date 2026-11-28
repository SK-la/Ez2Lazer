// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzModLayoutStoreTest
    {
        [Test]
        public void TestMissingFileIsNotCreated()
        {
            using var storage = new TemporaryNativeStorage("ez-mod-layout-missing");
            var store = new EzConfigStore(storage);

            Assert.That(store.GetModLayout("osu"), Is.Null);
            Assert.That(store.ModSelectLayoutMode, Is.EqualTo(EzModSelectLayoutMode.Horizontal));
            Assert.That(File.Exists(path(storage, EzModifyPath.CONFIG_ROOT_PATH, EzModLayoutDocument.FILENAME)), Is.False);
            Assert.That(Directory.Exists(full(storage, EzModifyPath.CONFIG_LAYOUT_PATH)), Is.False);
        }

        [Test]
        public void TestSaveWritesDocumentWithRulesetsAndGlobalShear()
        {
            using var storage = new TemporaryNativeStorage("ez-mod-layout-save");
            var store = new EzConfigStore(storage);
            store.SaveModLayout("osu", new EzRulesetModLayout
            {
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout
                    {
                        Id = "reduction",
                        Name = "Easy",
                        SeedType = "DifficultyReduction",
                        Acronyms = new List<string> { "EZ" },
                    },
                },
                Hidden = new List<string> { "FL" },
                ColumnShear = 0.1f,
            });

            string configFile = path(storage, EzModifyPath.CONFIG_ROOT_PATH, EzModLayoutDocument.FILENAME);
            string layoutFile = path(storage, EzModifyPath.CONFIG_LAYOUT_PATH, EzModLayoutDocument.FILENAME);

            Assert.That(File.Exists(configFile), Is.True);
            Assert.That(File.Exists(layoutFile), Is.False);

            var doc = JsonConvert.DeserializeObject<EzModLayoutDocument>(File.ReadAllText(configFile));
            Assert.That(doc, Is.Not.Null);
            Assert.That(doc!.Rulesets, Is.Not.Null);
            Assert.That(doc.Rulesets!.ContainsKey("osu"), Is.True);
            Assert.That(doc.ColumnShear, Is.EqualTo(0.1f));

            var loaded = new EzConfigStore(storage).GetModLayout("osu");

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Groups![0].Acronyms!, Is.EqualTo(new[] { "EZ" }));
            Assert.That(loaded.Hidden!, Is.EqualTo(new[] { "FL" }));
            Assert.That(loaded.Groups![0].Name, Is.EqualTo("Easy"));
            Assert.That(loaded.ColumnShear, Is.EqualTo(0.1f));
        }

        [Test]
        public void TestPerRulesetListsShareGlobalShear()
        {
            using var storage = new TemporaryNativeStorage("ez-mod-layout-rulesets");
            var store = new EzConfigStore(storage);

            store.SaveModLayout("osu", new EzRulesetModLayout
            {
                ColumnShear = 0.12f,
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout { Id = "a", Name = "Osu", Acronyms = new List<string> { "HD" } },
                },
            });

            store.SaveModLayout("mania", new EzRulesetModLayout
            {
                ColumnShear = 0.12f,
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout { Id = "b", Name = "Mania", Acronyms = new List<string> { "MR" } },
                },
            });

            var osu = new EzConfigStore(storage).GetModLayout("osu");
            var mania = new EzConfigStore(storage).GetModLayout("mania");

            Assert.That(osu!.Groups![0].Acronyms!, Is.EqualTo(new[] { "HD" }));
            Assert.That(mania!.Groups![0].Acronyms!, Is.EqualTo(new[] { "MR" }));
            Assert.That(osu.ColumnShear, Is.EqualTo(0.12f));
            Assert.That(mania.ColumnShear, Is.EqualTo(0.12f));
        }

        [Test]
        public void TestModeIsGlobalAndDoesNotSaveDraft()
        {
            using var storage = new TemporaryNativeStorage("ez-mod-layout-mode");
            var store = new EzConfigStore(storage);
            store.SaveModLayout("osu", new EzRulesetModLayout
            {
                ColumnShear = 0.12f,
                Groups = new List<EzModGroupLayout>
                {
                    new EzModGroupLayout { Id = "a", Name = "Saved", Acronyms = new List<string> { "HD" } },
                },
                Hidden = new List<string> { "FL" },
            });

            var draft = EzModLayoutResolver.Resolve(store.GetModLayout("osu"), new[]
            {
                new EzModLayoutMod("HD", ModType.DifficultyIncrease),
                new EzModLayoutMod("FL", ModType.DifficultyIncrease),
            }, preserveUnavailable: true);
            draft.Groups![0].Name = "Draft";
            store.SaveModSelectLayoutMode(EzModSelectLayoutMode.Vertical);

            var reloaded = new EzConfigStore(storage);
            Assert.That(reloaded.ModSelectLayoutMode, Is.EqualTo(EzModSelectLayoutMode.Vertical));
            Assert.That(reloaded.GetModLayout("osu")!.Groups![0].Name, Is.EqualTo("Saved"));
            Assert.That(reloaded.GetModLayout("osu")!.Hidden, Is.EqualTo(new[] { "FL" }));
            Assert.That(reloaded.GetModLayout("osu")!.ColumnShear, Is.EqualTo(0.12f));

            reloaded.SaveModLayout("mania", new EzRulesetModLayout { ColumnShear = 0.12f });
            Assert.That(new EzConfigStore(storage).ModSelectLayoutMode, Is.EqualTo(EzModSelectLayoutMode.Vertical));

            string json = File.ReadAllText(path(storage, EzModifyPath.CONFIG_ROOT_PATH, EzModLayoutDocument.FILENAME));
            Assert.That(JObject.Parse(json)["LayoutMode"]!.ToString(), Is.EqualTo("Vertical"));
        }

        [Test]
        public void TestLegacyReadDoesNotRewriteFile()
        {
            using var storage = new TemporaryNativeStorage("ez-mod-layout-legacy");
            var store = new EzConfigStore(storage);
            store.SaveModLayout("osu", new EzRulesetModLayout { ColumnShear = 0.1f });
            string configFile = path(storage, EzModifyPath.CONFIG_ROOT_PATH, EzModLayoutDocument.FILENAME);
            const string legacy = "{\"ColumnShear\":0.1,\"Rulesets\":{}}";
            File.WriteAllText(configFile, legacy);

            Assert.That(new EzConfigStore(storage).ModSelectLayoutMode, Is.EqualTo(EzModSelectLayoutMode.Horizontal));
            Assert.That(File.ReadAllText(configFile), Is.EqualTo(legacy));
        }

        [Test]
        public void TestLeaderboardImpactExceptionConfigIsCreatedAndUserEditable()
        {
            using var storage = new TemporaryNativeStorage("leaderboard-impact-exceptions");
            string configFile = path(storage, EzModifyPath.CONFIG_ROOT_PATH, LeaderboardModImpactUnit.FILENAME);

            _ = new EzConfigStore(storage);

            Assert.That(File.Exists(configFile), Is.True);
            var defaultDocument = JsonConvert.DeserializeObject<LeaderboardModImpactExceptionsDocument>(File.ReadAllText(configFile))!;
            Assert.That(defaultDocument.Version, Is.EqualTo(1));
            Assert.That(defaultDocument.Rate, Does.Contain("osu.Game.Rulesets.Mods.ModScoreV2"));
            Assert.That(defaultDocument.Rate, Does.Contain("osu.Game.Rulesets.Mania.Mods.ManiaModScoreV2"));
            Assert.That(defaultDocument.Difficulty, Does.Contain("osu.Game.Rulesets.Mods.ModScoreV2"));
            Assert.That(defaultDocument.Difficulty, Does.Contain("osu.Game.Rulesets.Mania.Mods.ManiaModScoreV2"));
            Assert.That(defaultDocument.Conversion, Does.Contain("osu.Game.Rulesets.Mods.ModScoreV2"));
            Assert.That(defaultDocument.Conversion, Does.Contain("osu.Game.Rulesets.Mania.Mods.ManiaModScoreV2"));
            Assert.That(defaultDocument.Conversion, Does.Contain("osu.Game.Rulesets.Mania.Mods.ManiaModConstantSpeed"));

            const string custom_json = """
                                      {
                                        "Version": 1,
                                        "Rate": [],
                                        "Difficulty": [],
                                        "Conversion": []
                                      }
                                      """;
            File.WriteAllText(configFile, custom_json);

            var customStore = new EzConfigStore(storage);
            Assert.That(customStore.ModImpactUnit.GetRelevantMods(new Mod[] { new ManiaModConstantSpeed() }, LeaderboardModFilter.All), Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(configFile), Is.EqualTo(custom_json));
        }

        [Test]
        public void TestInvalidLeaderboardImpactExceptionConfigFallsBackWithoutOverwrite()
        {
            using var storage = new TemporaryNativeStorage("leaderboard-impact-exceptions-invalid");
            string configFile = path(storage, EzModifyPath.CONFIG_ROOT_PATH, LeaderboardModImpactUnit.FILENAME);

            _ = new EzConfigStore(storage);
            const string invalid_json = "{ invalid json";
            File.WriteAllText(configFile, invalid_json);

            var loadedStore = new EzConfigStore(storage);
            Assert.That(loadedStore.ModImpactUnit.GetRelevantMods(new Mod[] { new ManiaModConstantSpeed() }, LeaderboardModFilter.All), Is.Empty);
            Assert.That(File.ReadAllText(configFile), Is.EqualTo(invalid_json));
        }

        private static string path(TemporaryNativeStorage storage, string directory, string filename) =>
            Path.Combine(full(storage, directory), filename);

        private static string full(TemporaryNativeStorage storage, string relative) =>
            storage.GetFullPath(relative);
    }
}
