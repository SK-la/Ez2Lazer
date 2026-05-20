// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// Regression guards for <see cref="BMSSkin"/>'s sandbox-bypass behaviour.
    ///
    /// The previous implementation called <c>AudioManager.Samples.Get(absolutePath)</c>, which is silently
    /// rejected by osu-framework's <c>NativeStorage</c> when the path lives outside the game data folder
    /// ("traverses outside of …"). That manifested as song-select preview being silent and in-game keysounds
    /// not playing for external BMS folders. The fix mounts the BMS folder as its own sample store via
    /// <c>NativeStorage</c> + <c>StorageBackedResourceStore</c> + <c>AudioManager.GetSampleStore</c>.
    ///
    /// We can't easily exercise the audio pipeline end-to-end without a host, so these tests assert the
    /// invariants statically: behaviour for graceful degradation, and a textual scan of the source file
    /// to catch anyone re-introducing the absolute-path call.
    /// </summary>
    [TestFixture]
    public class BMSSkinSandboxTest
    {
        [Test]
        public void TestBMSSkinSourceDoesNotUseGlobalSampleStoreDirectly()
        {
            string sourcePath = locateBmsSkinSource();
            string source = File.ReadAllText(sourcePath);

            // Strip block comments and line comments so commentary explaining the previous bug doesn't trip the regex.
            string stripped = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            stripped = Regex.Replace(stripped, @"//.*?$", string.Empty, RegexOptions.Multiline);

            // Match calls like `audioManager.Samples` or `AudioManager.Samples` (case-sensitive — these are the
            // global stores rooted at NativeStorage(gameDataFolder)). Mounted-store usage looks like
            // `audioManager.GetSampleStore(...)`, which won't match this pattern.
            var match = Regex.Match(stripped, @"\b[Aa]udio[Mm]anager\.(?:Samples|Tracks)\b");

            Assert.That(match.Success, Is.False,
                $"BMSSkin must not access AudioManager.Samples or AudioManager.Tracks directly — those are " +
                $"sandbox-rooted to the game data folder and reject external BMS paths. " +
                $"Use AudioManager.GetSampleStore(new StorageBackedResourceStore(new NativeStorage(folder))) instead. " +
                $"Offending fragment: '{match.Value}'.");
        }

        [Test]
        public void TestMissingFolderDegradesGracefully()
        {
            // Constructing a BMSSkin against a non-existent folder must not throw, and GetSample should return null.
            string missingFolder = Path.Combine(Path.GetTempPath(), "bms-skin-missing-" + Guid.NewGuid().ToString("N"));

            // Reflectively construct so we don't pull in a real AudioManager. The implementation accepts null AudioManager.
            object skin = constructSkin(missingFolder, audioManager: null);

            Assert.DoesNotThrow(() => callGetSample(skin, new TestSampleInfo(new[] { "kick.wav" })));
            Assert.That(callGetSample(skin, new TestSampleInfo(new[] { "kick.wav" })), Is.Null);
        }

        [Test]
        public void TestEmptyLookupNamesIgnored()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-skin-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                object skin = constructSkin(folder, audioManager: null);

                Assert.DoesNotThrow(() => callGetSample(skin, new TestSampleInfo(Array.Empty<string>())));
                Assert.DoesNotThrow(() => callGetSample(skin, new TestSampleInfo(new[] { string.Empty, "   ", null! })));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static object constructSkin(string folderPath, object? audioManager)
        {
            ConstructorInfo ctor = typeof(BMSSkin).GetConstructors().Single();
            return ctor.Invoke(new[] { folderPath, audioManager });
        }

        private static object? callGetSample(object skin, ISampleInfo info)
        {
            MethodInfo? method = skin.GetType().GetMethod("GetSample", new[] { typeof(ISampleInfo) });
            return method!.Invoke(skin, new object[] { info });
        }

        private static string locateBmsSkinSource()
        {
            // Tests run with cwd = test bin output. Walk up to repository root and locate the source by relative path.
            string assemblyLocation = typeof(BMSSkinSandboxTest).Assembly.Location;
            DirectoryInfo? cursor = new FileInfo(assemblyLocation).Directory;

            while (cursor != null)
            {
                string candidate = Path.Combine(cursor.FullName, "osu.Game.Rulesets.BMS", "Beatmaps", "BMSSkin.cs");

                if (File.Exists(candidate))
                    return candidate;

                cursor = cursor.Parent;
            }

            throw new FileNotFoundException("Could not locate BMSSkin.cs for source-level inspection.");
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
    }
}
