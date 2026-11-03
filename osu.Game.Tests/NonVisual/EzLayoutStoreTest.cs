// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzLayoutStoreTest
    {
        [Test]
        public void TestSaveAndLoadRoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-layout-test-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);

            try
            {
                var store = new EzConfigLayoutStore(storage);

                var layout = new SkinLayoutInfo();
                layout.Update(null, new[]
                {
                    new SerialisedDrawableInfo(new BoxElement())
                });

                store.Save(EzLayoutFiles.SONG_SELECT, layout);

                var loaded = store.Load(EzLayoutFiles.SONG_SELECT);
                Assert.That(loaded.TryGetDrawableInfo(null, out var infos), Is.True);
                Assert.That(infos!.Length, Is.EqualTo(1));
                Assert.That(infos[0].Type, Is.EqualTo(typeof(BoxElement)));

                store.Reset(EzLayoutFiles.SONG_SELECT);
                var empty = store.Load(EzLayoutFiles.SONG_SELECT);
                Assert.That(empty.TryGetDrawableInfo(null, out _), Is.False);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                }
            }
        }

        [Test]
        public void TestSavePreservesOtherRulesetLayer()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-layout-test-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);
            var mania = new RulesetInfo("mania", "osu!mania", string.Empty, 3);

            try
            {
                var store = new EzConfigLayoutStore(storage);

                var globalLayout = new SkinLayoutInfo();
                globalLayout.Update(null, new[] { new SerialisedDrawableInfo(new BoxElement()) });
                store.Save(EzLayoutFiles.MAIN_HUD, globalLayout);

                var merged = store.Load(EzLayoutFiles.MAIN_HUD);
                merged.Update(mania, new[] { new SerialisedDrawableInfo(new BigBlackBox()) });
                store.Save(EzLayoutFiles.MAIN_HUD, merged);

                var loaded = store.Load(EzLayoutFiles.MAIN_HUD);
                Assert.That(loaded.TryGetDrawableInfo(null, out var global), Is.True);
                Assert.That(global![0].Type, Is.EqualTo(typeof(BoxElement)));
                Assert.That(loaded.TryGetDrawableInfo(mania, out var ruleset), Is.True);
                Assert.That(ruleset![0].Type, Is.EqualTo(typeof(BigBlackBox)));

                store.Reset(EzLayoutFiles.MAIN_HUD, mania);
                var afterReset = store.Load(EzLayoutFiles.MAIN_HUD);
                Assert.That(afterReset.TryGetDrawableInfo(mania, out _), Is.False);
                Assert.That(afterReset.TryGetDrawableInfo(null, out var stillGlobal), Is.True);
                Assert.That(stillGlobal![0].Type, Is.EqualTo(typeof(BoxElement)));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                }
            }
        }

        [Test]
        public void TestLayoutFileNameMatchesContainerName()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-layout-test-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);

            try
            {
                var store = new EzConfigLayoutStore(storage);

                var layout = new SkinLayoutInfo();
                layout.Update(null, new[] { new SerialisedDrawableInfo(new BoxElement()) });
                store.Save(EzLayoutFiles.MAIN_HUD, layout);

                var layoutStorage = storage.GetStorageForDirectory(EzModifyPath.LayoutSetPath(EzModifyPath.LAYOUT_SET_DEFAULT));

                // Follows the official convention: the file is named after the container enum member.
                Assert.That(layoutStorage.Exists("MainHUDComponents.json"), Is.True);
                Assert.That(layoutStorage.Exists("Gameplay.json"), Is.False);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                }
            }
        }

        [Test]
        public void TestToolboxBlacklistExcludesSpecifiedTypesOnly()
        {
            var filter = EzLayoutToolboxBlacklist.INSTANCE;

            Assert.That(filter.IsExcluded(typeof(BigBlackBox)), Is.True);
            Assert.That(filter.IsExcluded(typeof(BoxElement)), Is.False);
        }
    }
}
