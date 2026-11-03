// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Pets;
using osuTK;

namespace osu.Game.Tests.EzOsuGame.Pets
{
    [TestFixture]
    public class EzPetMotionDriverTest
    {
        [Test]
        public void TestTeleportToTarget()
        {
            var driver = new EzPetMotionDriver();
            driver.Start(new EzPetMotionDefinition
            {
                Mode = "teleportTo",
                Target = new[] { 0.2f, 0.8f },
            }, new Vector2(0.5f, 0.5f), null);

            var pos = driver.Update(16, new Vector2(0.5f, 0.5f));
            Assert.That(pos, Is.EqualTo(new Vector2(0.2f, 0.8f)));
            Assert.That(driver.IsActive, Is.False);
        }

        [Test]
        public void TestMoveToCompletes()
        {
            var driver = new EzPetMotionDriver();
            driver.Start(new EzPetMotionDefinition
            {
                Mode = "moveTo",
                Target = new[] { 1f, 1f },
                DurationMs = 100,
                Easing = "None",
            }, Vector2.Zero, null);

            Assert.That(driver.Update(50, Vector2.Zero)!.Value.X, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(driver.IsActive, Is.True);

            var end = driver.Update(50, new Vector2(0.5f, 0.5f));
            Assert.That(end!.Value.X, Is.EqualTo(1f).Within(0.001f));
            Assert.That(end.Value.Y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(driver.IsActive, Is.False);
        }

        [Test]
        public void TestMoveToUsesAnchor()
        {
            var driver = new EzPetMotionDriver();
            driver.Start(new EzPetMotionDefinition
            {
                Mode = "moveTo",
                Anchor = "results.rank",
                DurationMs = 10,
            }, Vector2.Zero, anchor => anchor == "results.rank" ? new Vector2(0.5f, 0.38f) : null);

            var end = driver.Update(10, Vector2.Zero);
            Assert.That(end, Is.EqualTo(new Vector2(0.5f, 0.38f)));
        }
    }

    [TestFixture]
    public class EzPetStateMachineMotionTest
    {
        [Test]
        public void TestDragAndResultsRankRequestMotion()
        {
            var definition = EzPetPackDefinition.Parse("""
                {
                  "defaultState": "idle",
                  "clips": {
                    "idle": { "loop": true },
                    "grabbed": { "loop": true },
                    "proud": { "loop": false }
                  },
                  "states": {
                    "idle": { "clip": "idle" },
                    "grabbed": { "clip": "grabbed" },
                    "proud": { "clip": "proud", "next": "idle" }
                  },
                  "motions": {
                    "toRank": { "mode": "moveTo", "anchor": "results.rank", "durationMs": 100 }
                  },
                  "rules": [
                    { "when": "drag", "goto": "grabbed", "interrupt": true },
                    { "when": "resultsRank", "rank": "S", "goto": "proud", "motion": "toRank", "interrupt": true }
                  ]
                }
                """);

            var machine = new EzPetStateMachine();
            string? lastMotion = "unset";
            machine.MotionRequested += id => lastMotion = id;
            machine.ApplyPack(definition, definition.Clips.Keys);
            lastMotion = "unset";

            Assert.That(machine.HandleDrag(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("grabbed"));
            Assert.That(machine.HandleDragEnd(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));

            Assert.That(machine.HandleResultsRank("A"), Is.False);
            Assert.That(machine.HandleResultsRank("S"), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("proud"));
            Assert.That(lastMotion, Is.EqualTo("toRank"));
        }
    }
}
