// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Moq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.IO;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class ScriptedSkinLoaderTest
    {
        private const string resource_directory_suffix = "Resources.EzProScriptedSkin.";

        private static readonly Lazy<string> fixture_directory = new Lazy<string>(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ScriptedSkinLoaderTest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            extractEmbeddedResourceToFile("EzProSkin.csx", tempDir);
            extractEmbeddedResourceToFile("ManiaEzProSkinTransformer.csx", tempDir);

            return tempDir;
        });

        private static string fixtureDirectoryValue => fixture_directory.Value;

        private static void extractEmbeddedResourceToFile(string fileName, string targetDirectory)
        {
            var assembly = typeof(ScriptedSkinLoaderTest).Assembly;
            string resourceName = assembly.GetManifestResourceNames()
                                          .Single(n => n.EndsWith(resource_directory_suffix + fileName, StringComparison.Ordinal));

            using var resourceStream = assembly.GetManifestResourceStream(resourceName);

            if (resourceStream == null)
                throw new InvalidOperationException($"Embedded resource missing: {resourceName}");

            string targetPath = Path.Combine(targetDirectory, fileName);
            using var fileStream = File.Create(targetPath);
            resourceStream.CopyTo(fileStream);
        }

        [Test]
        public void TestFixtureDirectoryExists()
        {
            string directory = fixtureDirectoryValue;
            Assert.That(Directory.Exists(directory), Is.True, $"Fixture directory not found: {directory}");
            Assert.That(File.Exists(Path.Combine(directory, "EzProSkin.csx")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "ManiaEzProSkinTransformer.csx")), Is.True);
        }

        [Test]
        public async Task TestMainScriptCompiles()
        {
            var runner = new SandboxedScriptRunner();
            string scriptPath = Path.Combine(fixtureDirectoryValue, "EzProSkin.csx");

            SkinInfo? info = await runner.LoadScriptInfoAsync(scriptPath);

            Assert.That(info, Is.Not.Null);
            Assert.That(info!.Name, Is.EqualTo("Ez Pro"));
        }

        [Test]
        public void TestMainScriptDoesNotEmitNullableAnnotationWarnings()
        {
            string scriptPath = Path.Combine(fixtureDirectoryValue, "EzProSkin.csx");
            string scriptCode = File.ReadAllText(scriptPath);

            var script = ScriptedSkinCompilation.CreateScript(scriptCode);
            var compilation = ScriptedSkinCompilation.ApplyCompilationDefaults(script.GetCompilation());

            var nullableWarnings = compilation.GetDiagnostics()
                                              .Where(d => d.Severity == DiagnosticSeverity.Warning && d.Id == "CS8632")
                                              .ToList();

            Assert.That(nullableWarnings, Is.Empty, () => string.Join(Environment.NewLine, nullableWarnings.Select(w => w.GetMessage())));
        }

        [Test]
        public async Task TestMainScriptLoadsWithSkinIni()
        {
            var runner = new SandboxedScriptRunner();
            string scriptPath = Path.Combine(fixtureDirectoryValue, "EzProSkin.csx");

            var resources = createMockResourceProvider();
            var skinInfo = new SkinInfo { Name = "Ez Pro", Creator = "Test" };

            IScriptedSkin skin = await runner.LoadScriptAsync<IScriptedSkin>(scriptPath, skinInfo, resources.Object);

            Assert.That(skin, Is.Not.Null);
            Assert.That(skin.Name, Is.EqualTo("Ez Pro"));

            var combo = skin.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours);
            Assert.That(combo?.Value, Is.Not.Null);
            Assert.That(combo!.Value.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task TestManiaTransformerCompiles()
        {
            var runner = new SandboxedScriptRunner();
            string scriptPath = Path.Combine(fixtureDirectoryValue, "ManiaEzProSkinTransformer.csx");

            var beatmap = new ManiaBeatmap(new StageDefinition(4));
            var mockSkin = new Mock<ISkin>();

            SkinTransformer transformer = await runner.LoadScriptAsync<SkinTransformer>(scriptPath, mockSkin.Object, beatmap);

            Assert.That(transformer, Is.Not.Null);
            Assert.That(transformer.GetType().Name, Is.EqualTo("ManiaEzProSkinTransformer"));
        }

        private static Mock<IStorageResourceProvider> createMockResourceProvider()
        {
            var mock = new Mock<IStorageResourceProvider>();
            mock.Setup(m => m.Renderer).Returns(new DummyRenderer());
            return mock;
        }
    }
}
