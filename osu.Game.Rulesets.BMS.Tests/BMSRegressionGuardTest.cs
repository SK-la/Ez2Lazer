// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.BMS.Objects.Drawables;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSRegressionGuardTest
    {
        [Test]
        public void TestBeatmapManagerBuildTagsIncludesAllFeatureFlags()
        {
            var chart = new BMSChartCache
            {
                KeyCount = 7,
                HasScratch = true,
                HasLongNotes = true,
                HasStopSequence = true,
                HasScrollChanges = true,
                HasBgaLayer = true
            };

            string tags = invokeBuildTags(chart);

            Assert.That(tags, Does.Contain("bms"));
            Assert.That(tags, Does.Contain("key7"));
            Assert.That(tags, Does.Contain("scratch"));
            Assert.That(tags, Does.Contain("ln"));
            Assert.That(tags, Does.Contain("stop"));
            Assert.That(tags, Does.Contain("scroll"));
            Assert.That(tags, Does.Contain("bga"));
        }

        [Test]
        public void TestDrawableBMSNoteLoadDoesNotThrowInvalidOperation()
        {
            var drawable = new DrawableBMSNote(new BMSNote());
            MethodInfo loadMethod = typeof(DrawableBMSNote).GetMethod("load", BindingFlags.Instance | BindingFlags.NonPublic)!;

            TestDelegate action = () => loadMethod.Invoke(drawable, null);

            Assert.That(action, Throws.Nothing);
        }

        [Test]
        public void TestManiaSkinBeatmapAdapterUsesBeatmapColumnCount()
        {
            var beatmap = new Beatmap<BMSHitObject>
            {
                Difficulty =
                {
                    CircleSize = 14
                }
            };
            beatmap.HitObjects.Add(new BMSNote
            {
                StartTime = 1000,
                Column = 0,
                IsScratch = true
            });

            MethodInfo adapter = typeof(BMSRuleset).GetMethod("createManiaSkinBeatmap", BindingFlags.Static | BindingFlags.NonPublic)!;
            var maniaBeatmap = (ManiaBeatmap)adapter.Invoke(null, new object[] { beatmap })!;

            TestDelegate action = () => maniaBeatmap.GetStageForColumnIndex(13);

            Assert.That(action, Throws.Nothing);
        }

        private static string invokeBuildTags(BMSChartCache chart)
        {
            MethodInfo buildTags = typeof(BMSBeatmapManager).GetMethod("buildTags", BindingFlags.Static | BindingFlags.NonPublic)!;
            return (string)buildTags.Invoke(null, new object[] { chart })!;
        }
    }
}
