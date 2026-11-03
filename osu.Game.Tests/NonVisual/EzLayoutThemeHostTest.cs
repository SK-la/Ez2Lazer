// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
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
            var store = new EzConfigLayoutStore(storage);

            store.Save(EzLayoutFiles.SONG_SELECT, new SkinLayoutInfo());

            store.UseSet("layout-alt");

            Assert.That(store.LayoutSetName, Is.EqualTo("layout-alt"));
        }
    }
}
