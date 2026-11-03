// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Rulesets.BMS.Audio;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.BMS.Tests{
    /// <summary>
    /// Guards the contract that makes BMS playable without stalling: <see cref="BmsKeysoundManager.Prepare"/>
    /// hands sample decoding to a background task instead of blocking the calling (update) thread.
    /// </summary>
    [TestFixture]
    public class BmsKeysoundPreloadTest
    {
        [Test]
        public void TestPrepareDoesNotScanOrDecodeOnTheCallingThread()
        {
            string folder = createTempFolderWithFiles(2000);

            try
            {
                var manager = createManager(folder);

                manager.Prepare(new[] { createKeysoundObject("kick.wav") });

                // A synchronous implementation would have scanned the folder and decoded every sample before
                // returning; both must still be pending here.
                Assert.That(TestReflectionHelpers.GetField<object?>(manager, "folderIndex"), Is.Null,
                    "the folder scan must not run on the calling thread");
                Assert.That(manager.IsPrepared, Is.True, "prepare must be marked so a later call does not restart it");

                Task? preload = manager.PreloadTask;
                Assert.That(preload, Is.Not.Null, "prepare should expose the background decode");

                Assert.That(waitFor(preload!), Is.True, "the background decode should complete");
                Assert.That(TestReflectionHelpers.GetField<object?>(manager, "folderIndex"), Is.Not.Null,
                    "the background decode should build the folder index");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestPrepareWithoutKeysoundsDoesNotStartBackgroundWork()
        {
            string folder = createTempFolderWithFiles(1);

            try
            {
                var manager = createManager(folder);

                manager.Prepare(Array.Empty<HitObject>());

                Assert.That(manager.PreloadTask, Is.Null);
                Assert.That(manager.IsPrepared, Is.True);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestCancelledPreloadCanBeRestarted()
        {
            string folder = createTempFolderWithFiles(50);

            try
            {
                var manager = createManager(folder);

                // Swap in a task that never completes so the assertion cannot race the background scan.
                manager.Prepare(new[] { createKeysoundObject("kick.wav") });
                var pending = new TaskCompletionSource().Task;
                TestReflectionHelpers.SetField(manager, "preloadTask", pending);

                manager.CancelPreload();

                Assert.That(manager.IsPrepared, Is.False, "a cancelled preload must not look completed");
                Assert.That(manager.PreloadTask, Is.Null, "the cancelled task must be dropped so a restart is immediate");

                manager.Prepare(new[] { createKeysoundObject("kick.wav") });

                Task? restarted = manager.PreloadTask;
                Assert.That(restarted, Is.Not.Null, "prepare must be able to restart a cancelled preload");
                Assert.That(waitFor(restarted!), Is.True);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestDisposedManagerDoesNotPreload()
        {
            string folder = createTempFolderWithFiles(1);

            try
            {
                var manager = createManager(folder);
                manager.Dispose();

                Assert.DoesNotThrow(() => manager.Prepare(new[] { createKeysoundObject("kick.wav") }));
                Assert.That(manager.PreloadTask, Is.Null);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static BmsKeysoundManager createManager(string folder)
        {
            var manager = TestReflectionHelpers.CreateUninitialisedBmsKeysoundManager();

            // No real audio manager in a plain unit test: sample decoding itself is covered elsewhere, and the
            // preload contract under test here is about which thread the work happens on.
            TestReflectionHelpers.SetField(manager, "bmsFolder", folder);

            return manager;
        }

        private static HitObject createKeysoundObject(string filename)
        {
            var keysoundObject = new TestKeysoundObject();
            keysoundObject.KeysoundSamples.Add(new ConvertHitObjectParser.FileHitSampleInfo(filename, 100));
            return keysoundObject;
        }

        private static bool waitFor(Task task) => task.Wait(TimeSpan.FromSeconds(30));

        private static string createTempFolderWithFiles(int count)
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-preload-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            for (int i = 0; i < count; i++)
                File.WriteAllBytes(Path.Combine(folder, $"sample{i}.wav"), new byte[] { 0 });

            // The chart's referenced file, so resolution has something to find.
            File.WriteAllBytes(Path.Combine(folder, "kick.wav"), new byte[] { 0 });

            return folder;
        }

        private class TestKeysoundObject : HitObject, IBmsKeysoundProvider
        {
            public List<HitSampleInfo> KeysoundSamples { get; } = new List<HitSampleInfo>();

            IReadOnlyList<HitSampleInfo> IBmsKeysoundProvider.KeysoundSamples => KeysoundSamples;
        }
    }
}
