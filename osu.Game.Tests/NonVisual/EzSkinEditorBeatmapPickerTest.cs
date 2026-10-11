// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.Rulesets;
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

            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(withObjects, ruleset), Is.True);
            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(empty, ruleset), Is.False);
            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(unknown, ruleset), Is.True);
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

            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(protectedBeatmap, ruleset), Is.False);
        }

        [Test]
        public void TestMetadataCandidateMatchesRulesetShortName()
        {
            var active = new RulesetInfo { ShortName = @"custom", OnlineID = -1 };
            var other = new RulesetInfo { ShortName = @"other", OnlineID = -1 };

            var beatmap = new BeatmapInfo
            {
                Ruleset = other,
                TotalObjectCount = 10,
                BeatmapSet = new BeatmapSetInfo(),
            };

            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(beatmap, active), Is.False);
            Assert.That(EzSkinEditorBeatmapPicker.IsMetadataCandidate(beatmap, other), Is.True);
        }

        [Test]
        public void TestPreviewModesForManiaAndStandard()
        {
            var mania = new ManiaRuleset().RulesetInfo;

            Assert.That(EzBeatmapPreviewModes.GetAvailableModes(mania), Has.Count.EqualTo(4));
            Assert.That(EzBeatmapPreviewModes.ValidateMode(EzBeatmapPreviewMode.StaticScroll, mania), Is.EqualTo(EzBeatmapPreviewMode.StaticScroll));
            Assert.That(EzBeatmapPreviewModes.ValidateMode(EzBeatmapPreviewMode.StaticFullMap, mania), Is.EqualTo(EzBeatmapPreviewMode.StaticFullMap));
        }
    }
}
