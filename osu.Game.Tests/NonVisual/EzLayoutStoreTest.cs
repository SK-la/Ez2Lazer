// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
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
                var store = new EzLayoutStore(storage);

                var layout = new SkinLayoutInfo();
                layout.Update(null, new[]
                {
                    new SerialisedDrawableInfo(new BoxElement())
                });

                store.Save(GlobalSkinnableContainers.SongSelect, layout);

                var loaded = store.Load(GlobalSkinnableContainers.SongSelect);
                Assert.That(loaded.TryGetDrawableInfo(null, out var infos), Is.True);
                Assert.That(infos!.Length, Is.EqualTo(1));
                Assert.That(infos[0].Type, Is.EqualTo(typeof(BoxElement)));

                store.Reset(GlobalSkinnableContainers.SongSelect);
                var empty = store.Load(GlobalSkinnableContainers.SongSelect);
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
                var store = new EzLayoutStore(storage);

                var globalLayout = new SkinLayoutInfo();
                globalLayout.Update(null, new[] { new SerialisedDrawableInfo(new BoxElement()) });
                store.Save(GlobalSkinnableContainers.MainHUDComponents, globalLayout);

                var merged = store.Load(GlobalSkinnableContainers.MainHUDComponents);
                merged.Update(mania, new[] { new SerialisedDrawableInfo(new BigBlackBox()) });
                store.Save(GlobalSkinnableContainers.MainHUDComponents, merged);

                var loaded = store.Load(GlobalSkinnableContainers.MainHUDComponents);
                Assert.That(loaded.TryGetDrawableInfo(null, out var global), Is.True);
                Assert.That(global![0].Type, Is.EqualTo(typeof(BoxElement)));
                Assert.That(loaded.TryGetDrawableInfo(mania, out var ruleset), Is.True);
                Assert.That(ruleset![0].Type, Is.EqualTo(typeof(BigBlackBox)));

                store.Reset(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, mania));
                var afterReset = store.Load(GlobalSkinnableContainers.MainHUDComponents);
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
        public void TestToolboxBlacklistExcludesSpecifiedTypesOnly()
        {
            var filter = EzLayoutToolboxBlacklist.INSTANCE;

            Assert.That(filter.IsExcluded(typeof(BigBlackBox)), Is.True);
            Assert.That(filter.IsExcluded(typeof(BoxElement)), Is.False);
        }
    }
}
