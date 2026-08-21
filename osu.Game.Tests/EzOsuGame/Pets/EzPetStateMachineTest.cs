// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Pets;

namespace osu.Game.Tests.EzOsuGame.Pets
{
    [TestFixture]
    public class EzPetStateMachineTest
    {
        private EzPetStateMachine machine = null!;
        private readonly List<string> clipsPlayed = new List<string>();

        [SetUp]
        public void SetUp()
        {
            clipsPlayed.Clear();
            machine = new EzPetStateMachine();
            machine.ClipChanged += (_, clip) => clipsPlayed.Add(clip);

            var definition = EzPetPackDefinition.Parse(EzDefaultPetPack.PET_JSON);
            machine.ApplyPack(definition, definition.Clips.Keys);
            clipsPlayed.Clear();
        }

        [Test]
        public void TestHoverAndHoverEnd()
        {
            Assert.That(machine.HandleHover(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("hover"));

            Assert.That(machine.HandleHoverEnd(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
        }

        [Test]
        public void TestHoverEndIgnoredWhenNotHovering()
        {
            machine.HandleClick();
            Assert.That(machine.CurrentState, Is.EqualTo("poke"));
            Assert.That(machine.HandleHoverEnd(), Is.False);
            Assert.That(machine.CurrentState, Is.EqualTo("poke"));
        }

        [Test]
        public void TestClickInterruptThenNextFallsBackToIdle()
        {
            Assert.That(machine.HandleClick(), Is.True);
            Assert.That(machine.CurrentClip, Is.EqualTo("poke"));

            machine.NotifyClipFinished();
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
        }

        [Test]
        public void TestMissingClipDoesNotTrigger()
        {
            var definition = EzPetPackDefinition.Parse(EzDefaultPetPack.PET_JSON);
            machine.ApplyPack(definition, new[] { "idle" });
            clipsPlayed.Clear();

            Assert.That(machine.HandleClick(), Is.False);
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
            Assert.That(clipsPlayed, Is.Empty);
        }

        [Test]
        public void TestStarBandAndUnmatched()
        {
            Assert.That(machine.HandleStarRating(1.2), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("starEasy"));

            machine.NotifyClipFinished();
            Assert.That(machine.HandleStarRating(3.0), Is.False);
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
        }

        [Test]
        public void TestComboFiresOncePerThreshold()
        {
            Assert.That(machine.HandleCombo(50), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("combo50"));
            Assert.That(machine.HandleCombo(51), Is.False);

            machine.NotifyClipFinished();
            Assert.That(machine.HandleCombo(300), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("combo300"));
            Assert.That(machine.HandleCombo(300), Is.False);

            machine.ResetPlaySession();
            machine.NotifyClipFinished();
            Assert.That(machine.HandleCombo(50), Is.True);
        }

        [Test]
        public void TestComboDoesNotInterruptOneShot()
        {
            machine.HandleClick();
            Assert.That(machine.HandleCombo(50), Is.False);
            Assert.That(machine.CurrentState, Is.EqualTo("poke"));
        }

        [Test]
        public void TestMissInterrupts()
        {
            machine.HandleClick();
            Assert.That(machine.HandleMiss(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("miss"));
        }

        [Test]
        public void TestIdleClipsDoNotResetTimer()
        {
            machine.UpdateIdle(300);
            Assert.That(machine.CurrentState, Is.EqualTo("idlePlay"));
            Assert.That(machine.IdleSeconds, Is.EqualTo(300).Within(0.001));

            double idleAtClip = machine.IdleSeconds;
            machine.NotifyClipFinished();
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
            Assert.That(machine.IdleSeconds, Is.EqualTo(idleAtClip).Within(0.001));

            machine.UpdateIdle(0.1);
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));

            machine.UpdateIdle(300);
            Assert.That(machine.CurrentState, Is.EqualTo("idleYawn"));
        }

        [Test]
        public void TestUserEventResetsIdleTimer()
        {
            machine.UpdateIdle(299);
            machine.HandleHover();
            Assert.That(machine.IdleSeconds, Is.EqualTo(0).Within(0.001));

            machine.HandleHoverEnd();
            machine.UpdateIdle(300);
            Assert.That(machine.CurrentState, Is.EqualTo("idlePlay"));
        }

        [Test]
        public void TestNotifyUserActivityLeavesIdleSleep()
        {
            machine.UpdateIdle(900);
            Assert.That(machine.CurrentState, Is.EqualTo("idleSleep"));

            machine.NotifyUserActivity();
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
            Assert.That(machine.IdleSeconds, Is.EqualTo(0).Within(0.001));
        }

        [Test]
        public void TestEnterThenGameplayThenLeave()
        {
            Assert.That(machine.HandleGameplayEnter(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("enter"));

            machine.NotifyClipFinished();
            Assert.That(machine.CurrentState, Is.EqualTo("gameplay"));

            Assert.That(machine.HandleGameplayLeave(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
        }

        [Test]
        public void TestComboReturnsToGameplay()
        {
            machine.HandleGameplayEnter();
            machine.NotifyClipFinished();
            Assert.That(machine.CurrentState, Is.EqualTo("gameplay"));

            Assert.That(machine.HandleCombo(50), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("combo50"));

            machine.NotifyClipFinished();
            Assert.That(machine.CurrentState, Is.EqualTo("gameplay"));
        }

        [Test]
        public void TestGameplayEnterHideDoesNotNeedClip()
        {
            var definition = EzPetPackDefinition.Parse("""
                {
                  "defaultState": "idle",
                  "clips": { "idle": { "fps": 12, "loop": true } },
                  "states": { "idle": { "clip": "idle" } },
                  "rules": [ { "when": "gameplayEnter", "action": "hide" } ]
                }
                """);

            var actions = new List<EzPetVisibilityAction>();
            machine.VisibilityAction += actions.Add;
            machine.ApplyPack(definition, definition.Clips.Keys);
            actions.Clear();

            Assert.That(machine.HandleGameplayEnter(), Is.True);
            Assert.That(actions, Is.EqualTo(new[] { EzPetVisibilityAction.Hide }));
            Assert.That(machine.CurrentState, Is.EqualTo("idle"));
        }

        [Test]
        public void TestComboShowThenHide()
        {
            var definition = EzPetPackDefinition.Parse("""
                {
                  "defaultState": "idle",
                  "clips": {
                    "idle": { "fps": 12, "loop": true },
                    "combo200": { "fps": 12, "loop": false }
                  },
                  "states": {
                    "idle": { "clip": "idle" },
                    "combo200": { "clip": "combo200", "next": "idle" }
                  },
                  "rules": [
                    { "when": "combo", "at": 200, "action": "show", "goto": "combo200", "interrupt": true },
                    { "when": "combo", "at": 500, "action": "hide" }
                  ]
                }
                """);

            var actions = new List<EzPetVisibilityAction>();
            machine.VisibilityAction += actions.Add;
            machine.ApplyPack(definition, definition.Clips.Keys);
            actions.Clear();

            Assert.That(machine.HandleCombo(200), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("combo200"));
            Assert.That(actions, Is.EqualTo(new[] { EzPetVisibilityAction.Show }));

            Assert.That(machine.HandleCombo(500), Is.True);
            Assert.That(actions, Is.EqualTo(new[] { EzPetVisibilityAction.Show, EzPetVisibilityAction.Hide }));
        }
    }
}
