// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Replays;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Mania ReplayFrame → press/release 边沿。Session / 诊断共用；与 Drawable 侧
    /// <see cref="Mania.Replays.ManiaFramedReplayInputHandler"/> 分工：本类产边沿供中央机，后者把帧态喂给按键系统。
    /// </summary>
    internal static class ManiaReplayFrameEdgeParser
    {
        internal static bool IsManiaReplay(Replay? replay)
        {
            if (replay == null || replay.Frames.Count == 0)
                return false;

            return replay.Frames.All(f => f is ManiaReplayFrame);
        }

        /// <summary>
        /// <see cref="osu.Game.EzOsuGame.Scoring.EzReplayFeedMode.BatchAllEvents"/>：一次物化全部边沿。
        /// </summary>
        internal static ManiaReplayInputData ParseAll(Replay replay, double frameTimeOffset = 0)
            => CreateCursor(replay, frameTimeOffset).DrainAll();

        internal static ManiaReplayFrameEdgeCursor CreateCursor(Replay replay, double frameTimeOffset = 0)
            => new ManiaReplayFrameEdgeCursor(replay, frameTimeOffset);
    }

    /// <summary>
    /// <see cref="osu.Game.EzOsuGame.Scoring.EzReplayFeedMode.StreamByClock"/> 游标：按时间推进产出边沿。
    /// </summary>
    internal sealed class ManiaReplayFrameEdgeCursor
    {
        private readonly List<ManiaReplayFrame> frames;
        private readonly double frameTimeOffset;
        private readonly List<ManiaAction> lastActions = new List<ManiaAction>();
        private readonly Dictionary<int, List<double>> pressTimes = new Dictionary<int, List<double>>();
        private readonly List<ManiaReplayInputEvent> emitted = new List<ManiaReplayInputEvent>();
        private readonly List<double> frameTimes = new List<double>();

        private int frameIndex;
        private bool finalized;

        internal ManiaReplayFrameEdgeCursor(Replay replay, double frameTimeOffset)
        {
            this.frameTimeOffset = frameTimeOffset;
            frames = replay.Frames.OfType<ManiaReplayFrame>().OrderBy(f => f.Time).ToList();
        }

        /// <summary>
        /// 推进到（含）<paramref name="time"/> 的所有帧，把新边沿追加到 <paramref name="destination"/>。
        /// </summary>
        internal void DrainUntil(double time, List<ManiaReplayInputEvent> destination)
        {
            while (frameIndex < frames.Count && frames[frameIndex].Time + frameTimeOffset <= time)
                emitFrame(frames[frameIndex++], destination);

            if (frameIndex >= frames.Count)
                finalizeIfNeeded(destination);
        }

        internal ManiaReplayInputData DrainAll()
        {
            var events = new List<ManiaReplayInputEvent>(frames.Count * 2);
            DrainUntil(double.PositiveInfinity, events);
            sortEvents(events);

            foreach (var list in pressTimes.Values)
                list.Sort();

            return new ManiaReplayInputData(events, clonePressTimes(), new List<double>(frameTimes));
        }

        private void emitFrame(ManiaReplayFrame frame, List<ManiaReplayInputEvent> destination)
        {
            frameTimes.Add(frame.Time + frameTimeOffset);

            var current = frame.Actions.ToList();

            foreach (var action in current)
            {
                if (lastActions.Contains(action))
                    continue;

                int column = (int)action;

                if (column < 0)
                    continue;

                double inputTime = frame.Time + frameTimeOffset;
                var edge = new ManiaReplayInputEvent(inputTime, column, true);
                destination.Add(edge);
                emitted.Add(edge);

                if (!pressTimes.TryGetValue(column, out var list))
                {
                    list = new List<double>();
                    pressTimes[column] = list;
                }

                list.Add(inputTime);
            }

            foreach (var action in lastActions)
            {
                if (current.Contains(action))
                    continue;

                int column = (int)action;

                if (column < 0)
                    continue;

                var edge = new ManiaReplayInputEvent(frame.Time + frameTimeOffset, column, false);
                destination.Add(edge);
                emitted.Add(edge);
            }

            lastActions.Clear();
            lastActions.AddRange(current);
        }

        private void finalizeIfNeeded(List<ManiaReplayInputEvent> destination)
        {
            if (finalized || lastActions.Count == 0 || frames.Count == 0)
            {
                finalized = true;
                return;
            }

            double endTime = frames[^1].Time;

            foreach (var action in lastActions)
            {
                int column = (int)action;

                if (column < 0)
                    continue;

                var edge = new ManiaReplayInputEvent(endTime, column, false);
                destination.Add(edge);
                emitted.Add(edge);
            }

            lastActions.Clear();
            finalized = true;
        }

        private Dictionary<int, List<double>> clonePressTimes()
        {
            var copy = new Dictionary<int, List<double>>(pressTimes.Count);

            foreach (var (column, list) in pressTimes)
                copy[column] = new List<double>(list);

            return copy;
        }

        private static void sortEvents(List<ManiaReplayInputEvent> inputEvents)
        {
            inputEvents.Sort(static (a, b) =>
            {
                int timeComparison = a.Time.CompareTo(b.Time);
                if (timeComparison != 0)
                    return timeComparison;

                if (a.IsPress != b.IsPress)
                    return a.IsPress ? 1 : -1;

                return a.Column.CompareTo(b.Column);
            });
        }
    }
}
