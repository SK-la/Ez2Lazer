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
        private static string fixtureDirectory => Path.Combine(findRepositoryRoot(), "docs", "TestScriptedSkin");

        private static string findRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "docs", "TestScriptedSkin")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate docs/TestScriptedSkin fixture directory.");
        }

        [Test]
        public void TestFixtureDirectoryExists()
        {
            Assert.That(Directory.Exists(fixtureDirectory), Is.True, $"Fixture directory not found: {fixtureDirectory}");
            Assert.That(File.Exists(Path.Combine(fixtureDirectory, "TestScriptedSkin.csx")), Is.True);
            Assert.That(File.Exists(Path.Combine(fixtureDirectory, "skin.ini")), Is.True);
        }

        [Test]
        public async Task TestMainScriptCompiles()
        {
            var runner = new SandboxedScriptRunner();
            string scriptPath = Path.Combine(fixtureDirectory, "TestScriptedSkin.csx");

            SkinInfo? info = await runner.LoadScriptInfoAsync(scriptPath);

            Assert.That(info, Is.Not.Null);
            Assert.That(info!.Name, Is.EqualTo("TestScriptedSkin"));
        }

        [Test]
        public void TestMainScriptDoesNotEmitNullableAnnotationWarnings()
        {
            string scriptPath = Path.Combine(fixtureDirectory, "TestScriptedSkin.csx");
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
            string scriptPath = Path.Combine(fixtureDirectory, "TestScriptedSkin.csx");

            var resources = createMockResourceProvider();
            var skinInfo = new SkinInfo { Name = "TestScriptedSkin", Creator = "Test" };

            IScriptedSkin skin = await runner.LoadScriptAsync<IScriptedSkin>(scriptPath, skinInfo, resources.Object);

            Assert.That(skin, Is.Not.Null);
            Assert.That(skin.Name, Is.EqualTo("TestScriptedSkin"));

            var combo = skin.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours);
            Assert.That(combo?.Value, Is.Not.Null);
            Assert.That(combo!.Value.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task TestManiaTransformerCompiles()
        {
            var runner = new SandboxedScriptRunner();
            string scriptPath = Path.Combine(fixtureDirectory, "ManiaTestScriptedSkinTransformer.csx");

            var beatmap = new ManiaBeatmap(new StageDefinition(4));
            var mockSkin = new Mock<ISkin>();

            SkinTransformer transformer = await runner.LoadScriptAsync<SkinTransformer>(scriptPath, mockSkin.Object, beatmap);

            Assert.That(transformer, Is.Not.Null);
            Assert.That(transformer.GetType().Name, Is.EqualTo("ManiaTestScriptedSkinTransformer"));
        }

        private static Mock<IStorageResourceProvider> createMockResourceProvider()
        {
            var mock = new Mock<IStorageResourceProvider>();
            mock.Setup(m => m.Renderer).Returns(new DummyRenderer());
            return mock;
        }
    }
}
