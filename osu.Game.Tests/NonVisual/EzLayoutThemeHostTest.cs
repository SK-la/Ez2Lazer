// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzLayoutThemeHostTest
    {
        [Test]
        public void TestUseSetRebindsStoreWithoutReplacingInstance()
        {
            using var storage = new TemporaryNativeStorage("ez-layout-theme-host");
            var config = new Ez2ConfigManager(storage);
            var host = new EzLayoutThemeHost(storage, config);
            var store = host.Store;

            store.Save(EzLayoutFiles.SONG_SELECT, new SkinLayoutInfo());

            host.SelectSet("layout-alt");

            Assert.That(store.LayoutSetName, Is.EqualTo("layout-alt"));
            Assert.That(config.GetBindable<string>(Ez2Setting.LayoutSetName).Value, Is.EqualTo("layout-alt"));
        }

        [Test]
        public void TestClientRootStoreIgnoresUseSet()
        {
            using var storage = new TemporaryNativeStorage("ez-layout-client-root");
            var store = EzConfigLayoutStore.CreateClientRoot(storage);

            store.UseSet("layout-alt");

            Assert.That(store.IsClientRoot, Is.True);
            Assert.That(store.LayoutSetName, Is.EqualTo(string.Empty));
        }
    }
}
