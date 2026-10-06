// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Fonts;

namespace osu.Game.Tests.EzOsuGame.Fonts
{
    [TestFixture]
    public class EzSystemFontNameCacheTest
    {
        [Test]
        public void TestRoundTrip()
        {
            using var host = new TemporaryNativeStorage("ez-font-name-cache-roundtrip");
            var storage = host.GetStorageForDirectory(".");

            var cache = EzSystemFontNameCache.Load(storage);
            cache.Store(@"C:\Fonts\a.ttf", 100, 1234, "Alpha");
            cache.Store(@"C:\Fonts\b.ttf", 200, 5678, "Beta");
            cache.Save();

            var reloaded = EzSystemFontNameCache.Load(storage);

            Assert.That(reloaded.TryGet(@"C:\Fonts\a.ttf", 100, 1234, out string alpha), Is.True);
            Assert.That(alpha, Is.EqualTo("Alpha"));
            Assert.That(reloaded.TryGet(@"C:\Fonts\b.ttf", 200, 5678, out string beta), Is.True);
            Assert.That(beta, Is.EqualTo("Beta"));
        }

        [Test]
        public void TestFingerprintMiss()
        {
            using var host = new TemporaryNativeStorage("ez-font-name-cache-fingerprint");
            var storage = host.GetStorageForDirectory(".");

            var cache = EzSystemFontNameCache.Load(storage);
            cache.Store(@"C:\Fonts\a.ttf", 100, 1234, "Alpha");
            cache.Save();

            var reloaded = EzSystemFontNameCache.Load(storage);

            Assert.That(reloaded.TryGet(@"C:\Fonts\a.ttf", 101, 1234, out _), Is.False, "changed length");
            Assert.That(reloaded.TryGet(@"C:\Fonts\a.ttf", 100, 9999, out _), Is.False, "changed write time");
            Assert.That(reloaded.TryGet(@"C:\Fonts\other.ttf", 100, 1234, out _), Is.False, "unknown file");
        }

        [Test]
        public void TestMissingAndCorruptFileStartEmpty()
        {
            using var host = new TemporaryNativeStorage("ez-font-name-cache-corrupt");
            var storage = host.GetStorageForDirectory(".");

            Assert.That(EzSystemFontNameCache.Load(storage).TryGet(@"C:\Fonts\a.ttf", 1, 1, out _), Is.False);

            File.WriteAllText(storage.GetFullPath(EzSystemFontNameCache.FILENAME), "{ not json");

            var corrupt = EzSystemFontNameCache.Load(storage);

            Assert.That(corrupt.TryGet(@"C:\Fonts\a.ttf", 1, 1, out _), Is.False);
            Assert.DoesNotThrow(corrupt.Save);
        }

        [Test]
        public void TestSaveWithoutChangesWritesNothing()
        {
            using var host = new TemporaryNativeStorage("ez-font-name-cache-clean");
            var storage = host.GetStorageForDirectory(".");

            EzSystemFontNameCache.Load(storage).Save();

            Assert.That(storage.Exists(EzSystemFontNameCache.FILENAME), Is.False);
        }
    }
}
