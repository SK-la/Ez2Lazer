// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Aspire 自动播放。移动、按键、转盘与检查点来自 <see cref="OsuAutoplayCore"/>。
    /// </summary>
    /// <remarks>
    /// 身位采样对齐 osu!stable 滑条 follower：每个绘制线段一个位置，距上一帧不足一帧间隔的线段丢弃。
    /// 若像 <see cref="OsuAutoGenerator"/> 那样按时长走，持续数天的 aspire 滑条会分配数亿帧。
    /// 只走绘制几何与作者设定长度中较短者，因此声明长度可以截短路经，但不会延伸成 lazer 会追加的直线。
    /// </remarks>
    internal class OsuAspireAutoGenerator : OsuAutoplayCore
    {
        /// <summary>
        /// follower 帧间隔，整段回放只解析一次，与 osu!stable 一致。
        /// </summary>
        private double frameDelay;

        private readonly List<ReplayFrame> sliderFrameBatch = new List<ReplayFrame>();

        private List<ReplayFrame> mergeScratch = new List<ReplayFrame>();

        public OsuAspireAutoGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            : base(beatmap, mods)
        {
        }

        /// <summary>
        /// osu!stable 的自动转盘加速到 0.05 rad/ms，而非一开始就用该速度。
        /// </summary>
        protected override bool AccelerateSpinners => true;

        protected override bool AcceptCursorPosition(Vector2 position) => StableViewportBody.CanPlaceFieldPosition(position);

        public override Replay Generate()
        {
            frameDelay = GetFrameDelay(0);
            return base.Generate();
        }

        protected override void AddSliderBodyFrames(Slider slider, OsuAction action)
        {
            double spanDuration = slider.SpanDuration;

            if (spanDuration <= 0)
                return;

            IReadOnlyList<Vector2> vertices = slider.Path.CalculatedPath;

            if (vertices.Count < 2)
                return;

            // Distance 是延伸路径后作者设定的长度。CalculatedDistance 是控制点实际描出的长度。
            // stable 只走后者，并截短到前者。
            double pathLength = slider.Path.Distance;
            double geometryLength = Math.Min(pathLength, slider.Path.CalculatedDistance);

            if (pathLength <= 0 || geometryLength <= 0)
                return;

            int spanCount = slider.SpanCount();
            double timePerUnit = spanDuration / geometryLength;
            double progressScale = 1.0 / (spanCount * pathLength);
            double lastFrameTime = double.NegativeInfinity;

            sliderFrameBatch.Clear();

            for (int span = 0; span < spanCount; span++)
            {
                double spanOffset = span * geometryLength;
                double travelled = 0;

                for (int i = 0; i < vertices.Count; i++)
                {
                    if (i > 0)
                    {
                        travelled += Vector2.Distance(vertices[i - 1], vertices[i]);

                        if (travelled >= geometryLength)
                            travelled = geometryLength;
                    }

                    double alongSpan = spanOffset + travelled;
                    double time = slider.StartTime + alongSpan * timePerUnit;

                    if (time - lastFrameTime >= frameDelay)
                    {
                        Vector2 pos = slider.StackedPositionAt(alongSpan * progressScale);

                        if (AcceptCursorPosition(pos))
                            sliderFrameBatch.Add(new OsuReplayFrame(time, pos, action));

                        lastFrameTime = time;
                    }

                    if (travelled >= geometryLength)
                        break;
                }
            }

            mergeSliderFrames();
        }

        /// <summary>
        /// 一次遍历把完成的滑条帧并入回放。
        /// </summary>
        /// <remarks>
        /// 逐帧插入会移动尾部。Aspire 谱面长滑条够多，那种写法会主导加载时间。
        /// </remarks>
        private void mergeSliderFrames()
        {
            if (sliderFrameBatch.Count == 0)
                return;

            if (Frames.Count == 0 || sliderFrameBatch[0].Time >= Frames[^1].Time)
                Frames.AddRange(sliderFrameBatch);
            else
            {
                mergeScratch.Clear();

                int existing = 0;
                int incoming = 0;

                while (existing < Frames.Count && incoming < sliderFrameBatch.Count)
                {
                    mergeScratch.Add(Frames[existing].Time <= sliderFrameBatch[incoming].Time
                        ? Frames[existing++]
                        : sliderFrameBatch[incoming++]);
                }

                while (existing < Frames.Count)
                    mergeScratch.Add(Frames[existing++]);

                while (incoming < sliderFrameBatch.Count)
                    mergeScratch.Add(sliderFrameBatch[incoming++]);

                List<ReplayFrame> previous = Replay.Frames;
                Replay.Frames = mergeScratch;
                mergeScratch = previous;
            }

            sliderFrameBatch.Clear();
        }
    }
}
