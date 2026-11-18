// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json;
using osu.Framework.Testing;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Mods;

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

        private static string path(TemporaryNativeStorage storage, string directory, string filename) =>
            Path.Combine(full(storage, directory), filename);

        private static string full(TemporaryNativeStorage storage, string relative) =>
            storage.GetFullPath(relative);
    }
}