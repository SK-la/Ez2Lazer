// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Pets;

namespace osu.Game.Tests.EzOsuGame.Pets
{
    [TestFixture]
    public class EzPetLive2DAccessTest
    {
        private string tempRoot = null!;
        private NativeStorage gameStorage = null!;
        private Storage petsStorage = null!;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "ez-pet-l2d-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            gameStorage = new NativeStorage(tempRoot);
            petsStorage = gameStorage.GetStorageForDirectory("EzResources/Pets");
            EzPetLive2DAccess.SetPresetHashesForTests(new Dictionary<string, string>());
        }

        [TearDown]
        public void TearDown()
        {
            EzPetLive2DAccess.SetPresetHashesForTests(new Dictionary<string, string>());

            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }

        [Test]
        public void TestParseRenderer()
        {
            Assert.That(EzPetLive2DAccess.ParseRenderer("live2d"), Is.EqualTo(EzPetRendererKind.Live2D));
            Assert.That(EzPetLive2DAccess.ParseRenderer("frames"), Is.EqualTo(EzPetRendererKind.Frames));
            Assert.That(EzPetLive2DAccess.ParseRenderer(null), Is.EqualTo(EzPetRendererKind.Frames));
        }

        [Test]
        public void TestUserMadeLive2DPackDenied()
        {
            writePack("UserMadeL2D", """{ "renderer": "live2d", "defaultState": "idle", "clips": { "idle": { "loop": true } } }""", "fake-model-bytes");

            var def = EzPetPackDefinition.Parse(readPetJson("UserMadeL2D"));
            Assert.That(EzPetLive2DAccess.TryAuthorize("UserMadeL2D", def, petsStorage, out string? reason), Is.False);
            Assert.That(reason, Does.Contain("not an official"));

            var pack = new EzPetPackLoader(gameStorage).Load("UserMadeL2D");
            Assert.That(pack, Is.Not.Null);
            Assert.That(pack!.Live2DAuthorized, Is.False);
        }

        [Test]
        public void TestWhitelistHashMismatchDenied()
        {
            writePack("OfficialPet", """{ "renderer": "live2d", "defaultState": "idle", "clips": { "idle": { "loop": true } } }""", "model-content-a");

            EzPetLive2DAccess.SetPresetHashesForTests(new Dictionary<string, string>
            {
                ["OfficialPet"] = "deadbeef",
            });

            var def = EzPetPackDefinition.Parse(readPetJson("OfficialPet"));
            Assert.That(EzPetLive2DAccess.TryAuthorize("OfficialPet", def, petsStorage, out string? reason), Is.False);
            Assert.That(reason, Does.Contain("hash mismatch"));
        }

        [Test]
        public void TestWhitelistHashMatchAuthorisedWithoutPngFrames()
        {
            const string payload = "official-model-payload";
            writePack("OfficialPet", """{ "renderer": "live2d", "defaultState": "idle", "clips": { "idle": { "loop": true }, "poke": { "loop": false } } }""", payload);

            string? hash = EzPetLive2DAccess.ComputeFileSha256Hex(petsStorage, "OfficialPet/live2d/model.model3.json");
            Assert.That(hash, Is.Not.Null.And.Not.Empty);

            EzPetLive2DAccess.SetPresetHashesForTests(new Dictionary<string, string>
            {
                ["OfficialPet"] = hash!,
            });

            var pack = new EzPetPackLoader(gameStorage).Load("OfficialPet");
            Assert.That(pack, Is.Not.Null);
            Assert.That(pack!.Live2DAuthorized, Is.True);
            Assert.That(pack.Live2DModelEntryPath, Does.Contain("model.model3.json"));
            Assert.That(pack.AvailableClips.Contains("idle"), Is.True);
            Assert.That(pack.AvailableClips.Contains("poke"), Is.True);
        }

        [Test]
        public void TestFramesRendererNeverAuthorisesEvenWithMoc3Present()
        {
            writePack("PngPack", """{ "renderer": "frames", "defaultState": "idle", "clips": { "idle": { "loop": true } } }""", "should-not-matter");

            var def = EzPetPackDefinition.Parse(readPetJson("PngPack"));
            Assert.That(EzPetLive2DAccess.TryAuthorize("PngPack", def, petsStorage, out _), Is.False);
        }

        private void writePack(string name, string petJson, string modelPayload)
        {
            string dir = Path.Combine(tempRoot, "EzResources", "Pets", name, "live2d");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(tempRoot, "EzResources", "Pets", name, "pet.json"), petJson, Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "model.model3.json"), modelPayload, Encoding.UTF8);
        }

        private string readPetJson(string name)
            => File.ReadAllText(Path.Combine(tempRoot, "EzResources", "Pets", name, "pet.json"), Encoding.UTF8);
    }
}
