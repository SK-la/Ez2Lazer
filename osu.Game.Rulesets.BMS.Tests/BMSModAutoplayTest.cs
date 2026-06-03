// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Mods;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.BMS.Replays;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Replays;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSModAutoplayTest
    {
        [TearDown]
        public void TearDown() => BmsRuntimeAudioContext.Clear();

        [Test]
        public void TestRulesetExposesBmsAutoplayNotManiaAutoplay()
        {
            var ruleset = new BMSRuleset();
            Assert.That(ruleset.GetAutoplayMod(), Is.TypeOf<BMSModAutoplay>());
            Assert.That(ruleset.GetAutoplayMod(), Is.Not.TypeOf<ManiaModAutoplay>());
        }

        [Test]
        public void TestCreateReplayDataForBmsBeatmapUsesManiaFrames()
        {
            var beatmap = new BMSBeatmap();
            beatmap.HitObjects.Add(new BMSNote { Column = 0, StartTime = 1000 });

            var replay = new BMSModAutoplay().CreateReplayData(beatmap, Array.Empty<osu.Game.Rulesets.Mods.Mod>()).Replay;

            Assert.That(replay.Frames, Is.Not.Empty);
            Assert.That(replay.Frames.All(f => f is ManiaReplayFrame), Is.True);
        }

        [Test]
        public void TestCreateReplayDataForBmsBeatmapDoesNotThrow()
        {
            var beatmap = new BMSBeatmap();
            beatmap.HitObjects.Add(new BMSNote { Column = 1, StartTime = 500 });

            Assert.DoesNotThrow(() => new BMSModAutoplay().CreateReplayData(beatmap, Array.Empty<osu.Game.Rulesets.Mods.Mod>()));
        }

        [Test]
        public void TestNativeReplayFlagProducesBmsFrames()
        {
            var beatmap = new BMSBeatmap();
            beatmap.HitObjects.Add(new BMSNote { Column = 0, StartTime = 1000 });

            BmsRuntimeAudioContext.PreferNativeAutoplayReplay = true;

            var replay = new BMSModAutoplay().CreateReplayData(beatmap, Array.Empty<osu.Game.Rulesets.Mods.Mod>()).Replay;

            Assert.That(replay.Frames, Is.Not.Empty);
            Assert.That(replay.Frames.All(f => f is BMSReplayFrame), Is.True);
        }

        [Test]
        public void TestManiaCompatibleDrawableRulesetClearsNativeReplayFlag()
        {
            BmsRuntimeAudioContext.PreferNativeAutoplayReplay = true;

            var beatmap = new BMSBeatmap();
            beatmap.HitObjects.Add(new BMSNote { Column = 0, StartTime = 1000 });

            try
            {
                _ = new BMSRuleset().CreateDrawableRulesetWith(beatmap);
            }
            catch
            {
                // Drawable construction requires a host; flag is set before that and is what we assert.
            }

            Assert.That(BmsRuntimeAudioContext.PreferNativeAutoplayReplay, Is.False);
        }
    }
}
