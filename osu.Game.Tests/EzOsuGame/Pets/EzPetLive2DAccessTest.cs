// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        }

        [TearDown]
        public void TearDown()
        {
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
        public void TestUserMadeLive2DPackAuthorisedWhenModelPresent()
        {
            writePack("UserMadeL2D", """{ "renderer": "live2d", "defaultState": "idle", "clips": { "idle": { "loop": true } } }""", "fake-model-bytes");

            var def = EzPetPackDefinition.Parse(readPetJson("UserMadeL2D"));
            Assert.That(EzPetLive2DAccess.TryAuthorize("UserMadeL2D", def, petsStorage, out _), Is.True);

            var pack = new EzPetPackLoader(gameStorage).Load("UserMadeL2D");
            Assert.That(pack, Is.Not.Null);
            Assert.That(pack!.Live2DAuthorized, Is.True);
            Assert.That(pack.Live2DModelEntryPath, Does.Contain("model.model3.json"));
        }

        [Test]
        public void TestLive2DWithoutModelDenied()
        {
            string packDir = Path.Combine(tempRoot, "EzResources", "Pets", "NoModel");
            Directory.CreateDirectory(packDir);
            File.WriteAllText(
                Path.Combine(packDir, "pet.json"),
                """{ "renderer": "live2d", "defaultState": "idle", "clips": { "idle": { "loop": true } } }""",
                Encoding.UTF8);

            var def = EzPetPackDefinition.Parse(readPetJson("NoModel"));
            Assert.That(EzPetLive2DAccess.TryAuthorize("NoModel", def, petsStorage, out string? reason), Is.False);
            Assert.That(reason, Does.Contain("missing live2d model"));
        }

        [Test]
        public void TestLive2DAuthorisedWithoutPngFrames()
        {
            writePack("Live2DPet", """{ "renderer": "live2d", "defaultState": "idle", "clips": { "idle": { "loop": true }, "poke": { "loop": false } } }""", "model-payload");

            var pack = new EzPetPackLoader(gameStorage).Load("Live2DPet");
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

        [Test]
        public void TestHasCubismCoreOnDisk()
        {
            Assert.That(EzPetLive2DAccess.HasCubismCoreOnDisk(petsStorage), Is.False);

            string coreDir = Path.Combine(tempRoot, "EzResources", "Pets", "_cubism");
            Directory.CreateDirectory(coreDir);
            File.WriteAllBytes(Path.Combine(coreDir, EzPetCubismNative.CORE_DLL_WINDOWS), [0x00]);

            Assert.That(EzPetLive2DAccess.HasCubismCoreOnDisk(petsStorage), Is.True);
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
