using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Pets;

namespace osu.Game.Tests.EzOsuGame.Pets
{
    [TestFixture]
    public class EzPetCubismExpressionTest
    {
        [Test]
        public void TestDefaultExpressionsForClipMapsRanks()
        {
            Assert.That(EzPetCubismExpressionLibrary.DefaultExpressionsForClip("rankSS"), Is.EqualTo(new[] { "smile", "wave", "jump" }));
            Assert.That(EzPetCubismExpressionLibrary.DefaultExpressionsForClip("fail"), Is.EqualTo(new[] { "shake" }));
            Assert.That(EzPetCubismExpressionLibrary.DefaultExpressionsForClip("clear"), Is.EqualTo(new[] { "nod" }));
            Assert.That(EzPetCubismExpressionLibrary.DefaultExpressionsForClip("idle"), Is.Empty);
        }

        [Test]
        public void TestMergeOverridesRecipe()
        {
            var defaults = EzPetCubismExpressionLibrary.CreateDefaults();
            var overrides = new Dictionary<string, EzPetExpressionRecipe>
            {
                ["smile"] = new EzPetExpressionRecipe
                {
                    Id = "smile",
                    Params = { new EzPetExpressionParam { Id = "ParamMouthForm", Value = 0.5f } },
                },
            };

            var merged = EzPetCubismExpressionLibrary.Merge(defaults, overrides);
            Assert.That(merged["smile"].Params, Has.Count.EqualTo(1));
            Assert.That(merged["nod"].Params, Is.Not.Empty);
        }

        [Test]
        public void TestPackParsesClipExpressions()
        {
            const string json = """
                                {
                                  "renderer": "live2d",
                                  "live2d": {
                                    "clipExpressions": { "rankSS": ["smile", "jump"] },
                                    "lipSync": { "minOpen": 0.2 }
                                  },
                                  "clips": { "idle": { "loop": true } },
                                  "rules": []
                                }
                                """;

            var def = EzPetPackDefinition.Parse(json);
            Assert.That(def.Live2D, Is.Not.Null);
            Assert.That(def.Live2D!.ClipExpressions["rankSS"], Is.EqualTo(new[] { "smile", "jump" }));
            Assert.That(def.Live2D.LipSync!.MinOpen, Is.EqualTo(0.2f).Within(0.001f));
        }
    }

    [TestFixture]
    public class EzPetFailClearRuleTest
    {
        [Test]
        public void TestFailAndClearRulesFire()
        {
            var def = EzPetPackDefinition.Parse("""
                                                {
                                                  "defaultState": "idle",
                                                  "clips": {
                                                    "idle": { "loop": true },
                                                    "fail": { "loop": false },
                                                    "clear": { "loop": false }
                                                  },
                                                  "states": {
                                                    "idle": { "clip": "idle" },
                                                    "fail": { "clip": "fail", "next": "idle" },
                                                    "clear": { "clip": "clear", "next": "idle" }
                                                  },
                                                  "rules": [
                                                    { "when": "fail", "goto": "fail", "interrupt": true },
                                                    { "when": "clear", "goto": "clear", "interrupt": true }
                                                  ]
                                                }
                                                """);

            var sm = new EzPetStateMachine();
            sm.ApplyPack(def, new[] { "idle", "fail", "clear" });

            Assert.That(sm.HandleFail(), Is.True);
            Assert.That(sm.CurrentClip, Is.EqualTo("fail"));

            Assert.That(sm.HandleClear(), Is.True);
            Assert.That(sm.CurrentClip, Is.EqualTo("clear"));
        }
    }
}
