// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class BeatmapSetInfoExtensionsTest : RealmTest
    {
        [Test]
        public void TestGetFileToleratesDuplicateFilenames()
        {
            RunTestWithRealm((_, _) =>
            {
                var set = new BeatmapSetInfo
                {
                    Files =
                    {
                        new RealmNamedFileUsage(new RealmFile { Hash = "hash-a" }, "bg.jpg"),
                        new RealmNamedFileUsage(new RealmFile { Hash = "hash-b" }, "BG.jpg"),
                    }
                };

                Assert.DoesNotThrow(() => _ = set.GetFile("bg.jpg"));
                ClassicAssert.AreEqual("hash-a", set.GetFile("bg.jpg")!.File.Hash);
                ClassicAssert.AreEqual(2, set.GetFiles("bg.jpg").Count());
            });
        }

        [Test]
        public void TestAddFileCollapsesDuplicateFilenames()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                var manager = new ModelManager<BeatmapSetInfo>(storage, realmAccess);
                BeatmapSetInfo? set = null;

                realmAccess.Write(r =>
                {
                    set = new BeatmapSetInfo();
                    r.Add(set);
                    set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "old-a" }, "audio.mp3"));
                    set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "old-b" }, "AUDIO.MP3"));
                });

                set = set!.Detach();

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes("new audio"));
                manager.AddFile(set, stream, "audio.mp3");

                realmAccess.Write(r =>
                {
                    var managedSet = r.Find<BeatmapSetInfo>(set.ID)!;
                    ClassicAssert.AreEqual(1, managedSet.Files.Count(f => string.Equals(f.Filename, "audio.mp3", System.StringComparison.OrdinalIgnoreCase)));
                    ClassicAssert.AreNotEqual("old-a", managedSet.GetFile("audio.mp3")!.File.Hash);
                    ClassicAssert.AreNotEqual("old-b", managedSet.GetFile("audio.mp3")!.File.Hash);
                });
            });
        }
    }
}
