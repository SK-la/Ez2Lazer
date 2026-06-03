// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// Regression guards for external BMS sample loading via <see cref="BmsKeysoundManager"/> + <see cref="BMSSkin"/>.
    /// </summary>
    [TestFixture]
    public class BMSSkinSandboxTest
    {
        [Test]
        public void TestBmsKeysoundManagerSourceDoesNotUseGlobalSampleStoreDirectly()
        {
            string source = File.ReadAllText(locateSource("BmsKeysoundManager.cs"));

            string stripped = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            stripped = Regex.Replace(stripped, @"//.*?$", string.Empty, RegexOptions.Multiline);

            var match = Regex.Match(stripped, @"\b[Aa]udio[Mm]anager\.(?:Samples|Tracks)\b");

            Assert.That(match.Success, Is.False,
                $"BmsKeysoundManager must not access AudioManager.Samples or AudioManager.Tracks directly. " +
                $"Use AudioManager.GetSampleStore(new StorageBackedResourceStore(new NativeStorage(folder))) instead. " +
                $"Offending fragment: '{match.Value}'.");
        }

        [Test]
        public void TestUnpreparedSampleReturnsNull()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();
            var skin = new BMSSkin(manager);

            Assert.That(skin.GetSample(new ConvertHitObjectParser.FileHitSampleInfo("kick.wav", 100)), Is.Null);
        }

        [Test]
        public void TestNonFileSampleReturnsNull()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();
            var skin = new BMSSkin(manager);

            Assert.That(skin.GetSample(new TestSampleInfo(new[] { "hitnormal" })), Is.Null);
        }

        [Test]
        public void TestEmptyFilenameIgnored()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();
            var skin = new BMSSkin(manager);

            Assert.DoesNotThrow(() => skin.GetSample(new ConvertHitObjectParser.FileHitSampleInfo(string.Empty, 100)));
            Assert.DoesNotThrow(() => skin.GetSample(new ConvertHitObjectParser.FileHitSampleInfo("   ", 100)));
        }

        [Test]
        public void TestExternalSampleSkinFallsBackToInnerWhenRuntimeContextMissing()
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();
            var inner = new BMSSkin(manager);
            var wrapper = new BMSExternalSampleSkin(inner);

            BmsRuntimeAudioContext.Clear();

            Assert.That(wrapper.GetSample(new ConvertHitObjectParser.FileHitSampleInfo("kick.wav", 100)), Is.Null);
        }

        private static string locateSource(string fileName)
        {
            string assemblyLocation = typeof(BMSSkinSandboxTest).Assembly.Location;
            DirectoryInfo? cursor = new FileInfo(assemblyLocation).Directory;

            while (cursor != null)
            {
                string candidate = Path.Combine(cursor.FullName, "osu.Game.Rulesets.BMS", "Audio", fileName);

                if (File.Exists(candidate))
                    return candidate;

                candidate = Path.Combine(cursor.FullName, "osu.Game.Rulesets.BMS", "Beatmaps", fileName);

                if (File.Exists(candidate))
                    return candidate;

                cursor = cursor.Parent;
            }

            throw new FileNotFoundException($"Could not locate {fileName} for source-level inspection.");
        }

        private class TestSampleInfo : ISampleInfo
        {
            public TestSampleInfo(IEnumerable<string> lookupNames) => LookupNames = lookupNames;

            public IEnumerable<string> LookupNames { get; }
            public int Volume => 100;
        }
    }
}
