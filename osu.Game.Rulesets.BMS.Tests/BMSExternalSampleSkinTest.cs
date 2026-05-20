// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSExternalSampleSkinTest
    {
        [Test]
        public void TestInnerSkinIsAlwaysAskedFirst()
        {
            var inner = new TrackingSkin();
            string folder = createTempFolder();

            try
            {
                var skin = new BMSExternalSampleSkin(inner, folder, () => null);
                skin.GetSample(new TestSampleInfo(new[] { "kick.wav" }));

                Assert.That(inner.SampleLookups, Has.Count.EqualTo(1));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestNullAudioProviderDegradesGracefully()
        {
            var inner = new TrackingSkin();
            string folder = createTempFolder();

            try
            {
                File.WriteAllText(Path.Combine(folder, "kick.wav"), "x");

                var skin = new BMSExternalSampleSkin(inner, folder, () => null);
                Assert.That(skin.GetSample(new TestSampleInfo(new[] { "kick.wav" })), Is.Null);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestMissingFolderDegradesGracefully()
        {
            var inner = new TrackingSkin();
            string nonExistent = Path.Combine(Path.GetTempPath(), "bms-skin-missing-" + Guid.NewGuid().ToString("N"));

            var skin = new BMSExternalSampleSkin(inner, nonExistent, () => null);
            Assert.DoesNotThrow(() => skin.GetSample(new TestSampleInfo(new[] { "missing" })));
        }

        [Test]
        public void TestEmptyLookupNamesReturnsNullWithoutThrowing()
        {
            var inner = new TrackingSkin();
            string folder = createTempFolder();

            try
            {
                var skin = new BMSExternalSampleSkin(inner, folder, () => null);

                Assert.DoesNotThrow(() => skin.GetSample(new TestSampleInfo(Array.Empty<string>())));
                Assert.DoesNotThrow(() => skin.GetSample(new TestSampleInfo(new[] { string.Empty, "  ", null! })));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestPassThroughTextureAndConfig()
        {
            var inner = new TrackingSkin();
            string folder = createTempFolder();

            try
            {
                var skin = new BMSExternalSampleSkin(inner, folder, () => null);

                Assert.That(skin.GetTexture("anything", default, default), Is.Null);
                Assert.That(skin.GetConfig<string, string>("any"), Is.Null);
                Assert.That(inner.TextureLookups, Has.Count.EqualTo(1));
                Assert.That(inner.ConfigLookups, Has.Count.EqualTo(1));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestNullInnerThrows()
        {
            string folder = createTempFolder();

            try
            {
                Assert.That(() => new BMSExternalSampleSkin(null!, folder, () => null), Throws.ArgumentNullException);
                Assert.That(() => new BMSExternalSampleSkin(new TrackingSkin(), folder, null!), Throws.ArgumentNullException);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static string createTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-skin-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private class TestSampleInfo : ISampleInfo
        {
            private readonly string[] lookupNames;

            public TestSampleInfo(IEnumerable<string?> lookupNames)
            {
                this.lookupNames = lookupNames.Select(name => name ?? string.Empty).ToArray();
            }

            public IEnumerable<string> LookupNames => lookupNames;
            public int Volume => 100;
        }

        private class TrackingSkin : ISkin
        {
            public List<ISampleInfo> SampleLookups { get; } = new List<ISampleInfo>();
            public List<string> TextureLookups { get; } = new List<string>();
            public List<object> ConfigLookups { get; } = new List<object>();

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
            {
                TextureLookups.Add(componentName);
                return null;
            }

            public ISample? GetSample(ISampleInfo sampleInfo)
            {
                SampleLookups.Add(sampleInfo);
                return null;
            }

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
            {
                ConfigLookups.Add(lookup);
                return null;
            }
        }
    }
}
