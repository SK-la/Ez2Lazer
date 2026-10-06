// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Conversion
{
    /// <summary>
    /// 转换产物必须独占它的 <see cref="HitObject"/> 与 samples 列表，源谱面不能被转换管线改写。
    /// </summary>
    /// <remarks>
    /// 「源谱面已是 mania」这条路径原先直接把源对象放进产物里，于是 post-conversion mod、判定/窗口绑定与
    /// <see cref="HitObject.ApplyDefaults"/> 的就地写入会原样落回源谱面（以及之后每一次转换的产物）。
    /// 这条不变量是 playable 谱面缓存的前提：只有产物独占它的对象，一次转换才可能被多个消费者共用。
    /// </remarks>
    [TestFixture]
    public class EzPlayableBeatmapOwnershipTest
    {
        [Test]
        public void TestConvertedObjectsAreDetachedFromSource()
        {
            ManiaBeatmap source = createSourceBeatmap();
            IBeatmap converted = convert(source);

            Assert.That(converted.HitObjects.Count, Is.EqualTo(source.HitObjects.Count), "前置条件：转换应保留对象数量");

            foreach (ManiaHitObject original in source.HitObjects)
            {
                HitObject copy = converted.HitObjects.Single(h => h.StartTime == original.StartTime);

                Assert.That(copy, Is.Not.SameAs(original), $"@{original.StartTime} 产物与源共享同一实例");
                Assert.That(copy.SamplesBindable, Is.Not.SameAs(original.SamplesBindable), $"@{original.StartTime} 产物与源共享 SamplesBindable");
            }
        }

        [Test]
        public void TestConvertedHoldNoteOwnsNodeSamplesAndNestedObjects()
        {
            ManiaBeatmap source = createSourceBeatmap();
            applyDefaults(source);

            HoldNote sourceHold = source.HitObjects.OfType<HoldNote>().Single();
            IBeatmap converted = convert(source);
            HoldNote copyHold = converted.HitObjects.OfType<HoldNote>().Single();

            Assert.That(copyHold.NodeSamples, Is.Not.SameAs(sourceHold.NodeSamples), "产物的 NodeSamples 外层列表与源共享");

            for (int node = 0; node < sourceHold.NodeSamples.Count; node++)
                Assert.That(copyHold.NodeSamples[node], Is.Not.SameAs(sourceHold.NodeSamples[node]), $"产物的 NodeSamples[{node}] 与源共享");

            // 嵌套对象由产物自己的 ApplyDefaults 重建；带着源的嵌套实例会让 Column/StartTime 的写入穿透回源。
            Assert.That(copyHold.NestedHitObjects, Is.Empty, "产物应清空嵌套对象，交由 ApplyDefaults 重建");

            copyHold.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

            Assert.That(copyHold.NestedHitObjects, Is.Not.Empty, "前置条件：ApplyDefaults 应重建嵌套对象");
            Assert.That(copyHold.Head, Is.Not.SameAs(sourceHold.Head), "产物的头与源共享");
            Assert.That(copyHold.Body, Is.Not.SameAs(sourceHold.Body), "产物的 body 与源共享");
            Assert.That(copyHold.Ticks, Is.Not.SameAs(sourceHold.Ticks), "产物的 tick 列表与源共享");
        }

        [Test]
        public void TestWritingToConvertedObjectDoesNotReachSource()
        {
            ManiaBeatmap source = createSourceBeatmap();
            applyDefaults(source);

            Note sourceNote = source.HitObjects.OfType<Note>().Single();
            double sourceStartTime = sourceNote.StartTime;
            int sourceColumn = sourceNote.Column;
            int sourceSampleCount = sourceNote.Samples.Count;

            IBeatmap converted = convert(source);
            Note copyNote = converted.HitObjects.OfType<Note>().Single();

            copyNote.StartTime = 999999;
            copyNote.Column = 3;
            copyNote.Samples.Clear();

            Assert.That(sourceNote.StartTime, Is.EqualTo(sourceStartTime), "产物改写 StartTime 穿透到了源");
            Assert.That(sourceNote.Column, Is.EqualTo(sourceColumn), "产物改写 Column 穿透到了源");
            Assert.That(sourceNote.Samples.Count, Is.EqualTo(sourceSampleCount), "产物清空 Samples 穿透到了源");
        }

        [Test]
        public void TestRepeatedConversionProducesIndependentProducts()
        {
            ManiaBeatmap source = createSourceBeatmap();

            IBeatmap first = convert(source);
            IBeatmap second = convert(source);

            for (int i = 0; i < source.HitObjects.Count; i++)
            {
                Assert.That(second.HitObjects[i], Is.Not.SameAs(first.HitObjects[i]), $"#{i} 两次转换共享同一实例");
                Assert.That(second.HitObjects[i].SamplesBindable, Is.Not.SameAs(first.HitObjects[i].SamplesBindable), $"#{i} 两次转换共享 SamplesBindable");
            }
        }

        /// <summary>
        /// 源谱面自身是 mania（`.osu` mania 解码结果），即产物与源同型、走 <c>ConvertCompatibleHitObject</c> 那条路径。
        /// </summary>
        private static ManiaBeatmap createSourceBeatmap()
        {
            return new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects =
                {
                    new Note
                    {
                        StartTime = 1000,
                        Column = 0,
                        Samples = { new HitSampleInfo("hitnormal") },
                    },
                    new HoldNote
                    {
                        StartTime = 2000,
                        Duration = 500,
                        Column = 2,
                        Samples = { new HitSampleInfo("hitnormal") },
                        NodeSamples = new List<IList<HitSampleInfo>>
                        {
                            new List<HitSampleInfo> { new HitSampleInfo("hitnormal") },
                            new List<HitSampleInfo> { new HitSampleInfo("hitnormal") },
                        },
                    },
                },
                ControlPointInfo = createControlPoints(),
            };
        }

        private static void applyDefaults(IBeatmap beatmap)
        {
            foreach (HitObject hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
        }

        private static IBeatmap convert(ManiaBeatmap source)
            => new ManiaBeatmapConverter(source, new ManiaRuleset()).Convert();

        private static ControlPointInfo createControlPoints()
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 500 });

            return controlPoints;
        }
    }
}
