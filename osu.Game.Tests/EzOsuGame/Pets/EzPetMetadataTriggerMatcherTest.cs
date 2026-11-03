// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Pets;

namespace osu.Game.Tests.EzOsuGame.Pets
{
    [TestFixture]
    public class EzPetMetadataTriggerMatcherTest
    {
        private static readonly List<EzPetMetadataTriggerDefinition> lip_sync_triggers =
        [
            new EzPetMetadataTriggerDefinition
            {
                Action = "lipSync",
                Words = ["miku", "初音"],
            },
        ];

        [Test]
        public void NoTriggers_PassesGate()
        {
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(null, "lipSync", "foo", null, null, null), Is.True);
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate([], "lipSync", "foo", null, null, null), Is.True);
        }

        [Test]
        public void OtherActionOnly_PassesLipSyncGate()
        {
            var triggers = new List<EzPetMetadataTriggerDefinition>
            {
                new EzPetMetadataTriggerDefinition { Action = "wave", Words = ["miku"] },
            };

            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(triggers, "lipSync", "artist", null, null, null), Is.True);
        }

        [Test]
        public void ArtistWholeToken_MatchIgnoreCase()
        {
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "Hatsune Miku", null, null, null), Is.True);
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "hatsune miku", null, null, null), Is.True);
        }

        [Test]
        public void ArtistSubstring_DoesNotMatch()
        {
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "mikurun", null, null, null), Is.False);
        }

        [Test]
        public void ArtistUnicode_Match()
        {
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "x", "初音 ミク", null, null), Is.True);
        }

        [Test]
        public void TagsWholeToken_Match()
        {
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "x", null, "vocaloid miku anime", null), Is.True);
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "x", null, "vocaloid mikurun", null), Is.False);
        }

        [Test]
        public void UserTags_Match()
        {
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "x", null, null, ["Miku"]), Is.True);
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "x", null, null, ["vocaloid"]), Is.False);
        }

        [Test]
        public void TriggersPresent_Unmatched_FailsGate()
        {
            Assert.That(EzPetMetadataTriggerMatcher.PassesGate(lip_sync_triggers, "lipSync", "Camellia", null, "electronic", null), Is.False);
        }
    }
}
