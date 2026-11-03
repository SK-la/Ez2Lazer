// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Osu.Replays;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session
{
    /// <summary>
    /// Replay 光标与按键状态；位置插值对齐 <see cref="OsuFramedReplayInputHandler"/>，按键边沿在帧时刻检测。
    /// </summary>
    internal sealed class OsuReplayCursor
    {
        private readonly IReadOnlyList<OsuReplayFrame> frames;
        private int currentFrameIndex = -1;

        public double Time { get; private set; }

        public Vector2 Position { get; private set; }

        public OsuReplayCursor(IReadOnlyList<OsuReplayFrame> frames)
        {
            this.frames = frames;
        }

        public void Seek(double time)
        {
            Time = time;
            advanceFrameIndex(time);

            if (frames.Count == 0)
                return;

            if (currentFrameIndex < 0)
            {
                Position = frames[0].Position;
                return;
            }

            var startFrame = frames[currentFrameIndex];
            var endFrame = frames[Math.Min(currentFrameIndex + 1, frames.Count - 1)];

            Position = startFrame.Time == endFrame.Time
                ? startFrame.Position
                : Interpolation.ValueAt(time, startFrame.Position, endFrame.Position, startFrame.Time, endFrame.Time);
        }

        /// <summary>
        /// 按 replay 帧顺序收集 press 边沿（与旧 Generator press 列表同语义）。
        /// </summary>
        public static IReadOnlyList<PressEdge> CollectPressEdges(IReadOnlyList<OsuReplayFrame> frames)
        {
            var edges = new List<PressEdge>();
            var previousActions = new HashSet<OsuAction>();

            foreach (var frame in frames)
            {
                var currentActions = new HashSet<OsuAction>(getPressedActions(frame));

                foreach (var action in currentActions)
                {
                    if (!previousActions.Contains(action))
                        edges.Add(new PressEdge(frame.Time, frame.Position, action));
                }

                previousActions = currentActions;
            }

            return edges;
        }

        public static IReadOnlyList<double> CollectSimulationTimes(
            IReadOnlyList<OsuReplayFrame> frames,
            IEnumerable<double> missDeadlines,
            IEnumerable<IEnumerable<double>> additionalTimes)
        {
            var times = new SortedSet<double>();

            foreach (var frame in frames)
                times.Add(frame.Time);

            foreach (double deadline in missDeadlines)
                times.Add(deadline);

            foreach (var group in additionalTimes)
            {
                foreach (double time in group)
                    times.Add(time);
            }

            return times.ToList();
        }

        public static Vector2 InterpolatePosition(IReadOnlyList<OsuReplayFrame> frames, double time)
        {
            if (frames.Count == 0)
                return Vector2.Zero;

            if (time < frames[0].Time)
                return frames[0].Position;

            int frameIndex = 0;

            while (frameIndex + 1 < frames.Count && frames[frameIndex + 1].Time <= time)
                frameIndex++;

            var startFrame = frames[frameIndex];
            var endFrame = frames[Math.Min(frameIndex + 1, frames.Count - 1)];

            return startFrame.Time == endFrame.Time
                ? startFrame.Position
                : Interpolation.ValueAt(time, startFrame.Position, endFrame.Position, startFrame.Time, endFrame.Time);
        }

        public static IReadOnlyList<OsuAction> GetPressedActionsAt(IReadOnlyList<OsuReplayFrame> frames, double time)
        {
            if (frames.Count == 0 || time < frames[0].Time)
                return Array.Empty<OsuAction>();

            int frameIndex = 0;

            while (frameIndex + 1 < frames.Count && frames[frameIndex + 1].Time <= time)
                frameIndex++;

            return getPressedActions(frames[frameIndex]).ToList();
        }

        public IReadOnlyList<OsuAction> GetPressedActions() => GetPressedActionsAt(frames, Time);

        private void advanceFrameIndex(double time)
        {
            if (frames.Count == 0)
            {
                currentFrameIndex = -1;
                return;
            }

            if (time < frames[0].Time)
            {
                currentFrameIndex = -1;
                return;
            }

            while (currentFrameIndex + 1 < frames.Count && frames[currentFrameIndex + 1].Time <= time)
                currentFrameIndex++;

            if (currentFrameIndex < 0)
                currentFrameIndex = 0;
        }

        private static IEnumerable<OsuAction> getPressedActions(OsuReplayFrame frame)
        {
            foreach (var action in frame.Actions)
            {
                if (action == OsuAction.LeftButton || action == OsuAction.RightButton)
                    yield return action;
            }
        }

        internal readonly struct PressEdge
        {
            public readonly double Time;
            public readonly Vector2 Position;
            public readonly OsuAction Action;

            public PressEdge(double time, Vector2 position, OsuAction action)
            {
                Time = time;
                Position = position;
                Action = action;
            }
        }
    }
}
