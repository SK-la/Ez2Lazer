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
        public void TestGameplayEnterInterrupts()
        {
            machine.HandleHover();
            Assert.That(machine.HandleGameplayEnter(), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo("enter"));
        }
    }
}
