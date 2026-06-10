// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinEditorBeatmapPickerTest
    {
        [Test]
        public void TestMetadataCandidateRequiresObjects()
        {
            var ruleset = new ManiaRuleset().RulesetInfo;

            var withObjects = new BeatmapInfo
            {
                Ruleset = ruleset,
                TotalObjectCount = 42,
                BeatmapSet = new BeatmapSetInfo(),
            };

            var empty = new BeatmapInfo
            {
                Ruleset = ruleset,
                TotalObjectCount = 0,
                BeatmapSet = new BeatmapSetInfo(),
            };

            var unknown = new BeatmapInfo
            {
                Ruleset = ruleset,
                TotalObjectCount = -1,
                BeatmapSet = new BeatmapSetInfo(),
            };

            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(withObjects, ruleset.OnlineID), Is.True);
            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(empty, ruleset.OnlineID), Is.False);
            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(unknown, ruleset.OnlineID), Is.True);
        }

        [Test]
        public void TestMetadataCandidateRejectsProtectedSet()
        {
            var ruleset = new ManiaRuleset().RulesetInfo;

            var protectedBeatmap = new BeatmapInfo
            {
                Ruleset = ruleset,
                TotalObjectCount = 10,
                BeatmapSet = new BeatmapSetInfo { Protected = true },
            };

            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(protectedBeatmap, ruleset.OnlineID), Is.False);
        }

        [Test]
        public void TestPreviewModesForManiaAndStandard()
        {
            var mania = new ManiaRuleset().RulesetInfo;

            Assert.That(EzSkinEditorPreviewModes.GetAvailableModes(mania), Has.Count.EqualTo(4));
            Assert.That(EzSkinEditorPreviewModes.ValidateMode(EzBeatmapPreviewMode.StaticScroll, mania), Is.EqualTo(EzBeatmapPreviewMode.StaticScroll));
            Assert.That(EzSkinEditorPreviewModes.ValidateMode(EzBeatmapPreviewMode.StaticFullMap, mania), Is.EqualTo(EzBeatmapPreviewMode.StaticFullMap));
        }
    }
}
