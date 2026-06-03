// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSExternalSampleSkinTest
    {
        [TearDown]
        public void TearDown()
        {
            BmsRuntimeAudioContext.Clear();
        }

        [Test]
        public void TestFileHitSampleDoesNotQueryInnerSkin()
        {
            var inner = new TrackingSkin();
            var skin = new BMSExternalSampleSkin(inner);

            skin.GetSample(new ConvertHitObjectParser.FileHitSampleInfo("kick.wav", 100));

            Assert.That(inner.SampleLookups, Is.Empty);
        }

        [Test]
        public void TestNonFileSampleUsesInnerSkinOnly()
        {
            var inner = new TrackingSkin();
            var skin = new BMSExternalSampleSkin(inner);

            skin.GetSample(new TestSampleInfo(new[] { "hitnormal" }));

            Assert.That(inner.SampleLookups, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestFileHitSampleReadsFromRuntimeManagerCache()
        {
            var inner = new TrackingSkin();
            var skin = new BMSExternalSampleSkin(inner);
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();
            var cache = TestReflectionHelpers.GetField<Dictionary<string, ISample>>(manager, "keysoundCache");
            cache["kick.wav"] = new SampleVirtual();

            BmsRuntimeAudioContext.RegisterKeysoundManager(manager);

            var sample = skin.GetSample(new ConvertHitObjectParser.FileHitSampleInfo("kick.wav", 100));

            Assert.That(inner.SampleLookups, Is.Empty);
            Assert.That(sample, Is.Not.Null);
        }

        [Test]
        public void TestPassThroughTextureAndConfig()
        {
            var inner = new TrackingSkin();
            var skin = new BMSExternalSampleSkin(inner);

            Assert.That(skin.GetTexture("anything", default, default), Is.Null);
            Assert.That(skin.GetConfig<string, string>("any"), Is.Null);
            Assert.That(inner.TextureLookups, Has.Count.EqualTo(1));
            Assert.That(inner.ConfigLookups, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestNullInnerThrows()
        {
            Assert.That(() => new BMSExternalSampleSkin(null!), Throws.ArgumentNullException);
        }

        private static string createTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-skin-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private class TestSampleInfo : ISampleInfo
        {
            public TestSampleInfo(IEnumerable<string> lookupNames) => LookupNames = lookupNames;

            public IEnumerable<string> LookupNames { get; }
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
