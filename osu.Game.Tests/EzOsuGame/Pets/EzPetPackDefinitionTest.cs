// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Pets;

namespace osu.Game.Tests.EzOsuGame.Pets
{
    [TestFixture]
    public class EzPetPackDefinitionTest
    {
        [Test]
        public void TestParseDefaultPack()
        {
            var pack = EzPetPackDefinition.Parse(EzDefaultPetPack.PET_JSON);

            Assert.That(pack.DefaultState, Is.EqualTo("idle"));
            Assert.That(pack.Clips.ContainsKey("idle"), Is.True);
            Assert.That(pack.Clips["idle"].Loop, Is.True);
            Assert.That(pack.Clips["poke"].Loop, Is.False);
            Assert.That(pack.States["poke"].Next, Is.EqualTo("idle"));
            Assert.That(pack.StarBands, Has.Count.EqualTo(2));
            Assert.That(pack.Rules, Has.Count.EqualTo(10));
        }

        [Test]
        public void TestStarBandHalfOpen()
        {
            var pack = EzPetPackDefinition.Parse(EzDefaultPetPack.PET_JSON);

            Assert.That(pack.MatchStarBand(0), Is.EqualTo("starEasy"));
            Assert.That(pack.MatchStarBand(1.99), Is.EqualTo("starEasy"));
            Assert.That(pack.MatchStarBand(2), Is.Null);
            Assert.That(pack.MatchStarBand(3), Is.Null);
            Assert.That(pack.MatchStarBand(4.5), Is.EqualTo("starHard"));
            Assert.That(pack.MatchStarBand(10), Is.EqualTo("starHard"));
            Assert.That(pack.MatchStarBand(99), Is.Null);
        }

        [Test]
        public void TestCollectIndexedFramesIgnoresPrefix()
        {
            string[] files = new[] { "guga_02.png", "foo_00.png", "bar_01.jpg", "readme.txt", "idle.png" };
            var names = EzPetFramePath.CollectIndexedFrameNames(files);

            Assert.That(names, Is.EqualTo(new[] { "foo_00", "bar_01", "guga_02" }));
        }

        [Test]
        public void TestCollectIndexedFramesSortsNumerically()
        {
            string[] files = new[] { "a_10.png", "a_2.png", "a_00.png" };
            var names = EzPetFramePath.CollectIndexedFrameNames(files);

            Assert.That(names, Is.EqualTo(new[] { "a_00", "a_2", "a_10" }));
        }

        [Test]
        public void TestSnakeCaseAliases()
        {
            Assert.That(EzPetFramePath.ToSnakeCase("starEasy"), Is.EqualTo("star_easy"));
            Assert.That(EzPetFramePath.ToSnakeCase("idlePlay"), Is.EqualTo("idle_play"));
            Assert.That(EzPetFramePath.ToSnakeCase("idle"), Is.EqualTo("idle"));
        }

        [Test]
        public void TestLoaderWritesDefaultPack()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-pet-test-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);

            try
            {
                var loader = new EzPetPackLoader(storage);
                var names = loader.ListPackNames();

                Assert.That(names, Does.Contain(EzDefaultPetPack.NAME));

                var pack = loader.Load(EzDefaultPetPack.NAME);
                Assert.That(pack, Is.Not.Null);
                Assert.That(pack!.IsDefault, Is.True);
                Assert.That(pack.AvailableClips.Contains("idle"), Is.True);
                Assert.That(pack.AvailableClips.Contains("miss"), Is.True);
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
        public void TestUserPackMissingFramesAreUnavailable()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-pet-test-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);

            try
            {
                var pets = storage.GetStorageForDirectory(osu.Game.EzOsuGame.EzModifyPath.PETS_PATH);
                var custom = pets.GetStorageForDirectory("Custom");

                using (var stream = custom.CreateFileSafely("pet.json"))
                using (var writer = new StreamWriter(stream))
                    writer.Write(EzDefaultPetPack.PET_JSON);

                var loader = new EzPetPackLoader(storage);
                var pack = loader.Load("Custom");

                Assert.That(pack, Is.Not.Null);
                Assert.That(pack!.AvailableClips, Is.Empty);
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
        public void TestLoaderReadsArbitraryNamesInActionFolder()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-pet-test-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);

            try
            {
                var pets = storage.GetStorageForDirectory(osu.Game.EzOsuGame.EzModifyPath.PETS_PATH);
                var custom = pets.GetStorageForDirectory("Custom");
                var idle = custom.GetStorageForDirectory("idle");
                var star = custom.GetStorageForDirectory("star_easy");

                using (var stream = custom.CreateFileSafely("pet.json"))
                using (var writer = new StreamWriter(stream))
                    writer.Write(EzDefaultPetPack.PET_JSON);

                File.WriteAllBytes(idle.GetFullPath("random_00.png"), [0]);
                File.WriteAllBytes(idle.GetFullPath("zzz_01.png"), [0]);
                File.WriteAllBytes(star.GetFullPath("guga_00.png"), [0]);

                var loader = new EzPetPackLoader(storage);
                var pack = loader.Load("Custom");

                Assert.That(pack, Is.Not.Null);
                Assert.That(pack!.AvailableClips.Contains("idle"), Is.True);
                Assert.That(pack.AvailableClips.Contains("starEasy"), Is.True);
                Assert.That(pack.AvailableClips.Contains("hover"), Is.False);

                var idleFrames = loader.GetClipFrameNames("Custom", "idle", pack.Definition.Clips["idle"]);
                Assert.That(idleFrames, Is.EqualTo(new[] { "idle/random_00", "idle/zzz_01" }));

                var starFrames = loader.GetClipFrameNames("Custom", "starEasy", pack.Definition.Clips["starEasy"]);
                Assert.That(starFrames, Is.EqualTo(new[] { "star_easy/guga_00" }));
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
    }
}
