// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Osu.EzOsu.Judgement;
using osu.Game.Rulesets.Osu.Replays;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session
{
    /// <summary>
    /// Replay 帧的时间可寻址采样：位置插值对齐 <see cref="OsuFramedReplayInputHandler"/>，按键边沿按帧时刻取值。
    /// </summary>
    internal sealed class OsuReplaySampleSource : IOsuJudgementSampleSource
    {
        private readonly IReadOnlyList<OsuReplayFrame> frames;

        private int cursor;
        private double cachedTime = double.NaN;
        private bool cachedLeft;
        private bool cachedRight;

        public OsuReplaySampleSource(IReadOnlyList<OsuReplayFrame> frames)
        {
            this.frames = frames;
        }

        public bool IsRewinding => false;

        public Vector2 PositionAt(double time)
        {
            if (frames.Count == 0)
                return Vector2.Zero;

            if (time < frames[0].Time)
                return frames[0].Position;

            advanceCursor(time);

            var startFrame = frames[cursor];
            var endFrame = frames[Math.Min(cursor + 1, frames.Count - 1)];

            return startFrame.Time == endFrame.Time
                ? startFrame.Position
                : interpolate(time, startFrame, endFrame);
        }

        public bool IsPressedAt(OsuAction action, double time)
        {
            if (action != OsuAction.LeftButton && action != OsuAction.RightButton)
                return false;

            refreshPressed(time);
            return action == OsuAction.LeftButton ? cachedLeft : cachedRight;
        }

        public bool WasPressedAt(OsuAction action, double time)
        {
            if (action != OsuAction.LeftButton && action != OsuAction.RightButton)
                return false;

            int index = frameIndexBefore(time);

            if (index < 0)
                return false;

            foreach (var frameAction in frames[index].Actions)
            {
                if (frameAction == action)
                    return true;
            }

            return false;
        }

        /// <summary>严格早于 <paramref name="time"/> 的最后一帧下标。</summary>
        private int frameIndexBefore(double time)
        {
            int low = 0;
            int high = frames.Count - 1;

            if (high < 0 || frames[0].Time >= time)
                return -1;

            while (low < high)
            {
                int middle = (low + high + 1) / 2;

                if (frames[middle].Time < time)
                    low = middle;
                else
                    high = middle - 1;
            }

            return low;
        }

        private void refreshPressed(double time)
        {
            if (time == cachedTime)
                return;

            cachedTime = time;
            cachedLeft = false;
            cachedRight = false;

            if (frames.Count == 0 || time < frames[0].Time)
                return;

            advanceCursor(time);

            foreach (var action in frames[cursor].Actions)
            {
                if (action == OsuAction.LeftButton)
                    cachedLeft = true;
                else if (action == OsuAction.RightButton)
                    cachedRight = true;
            }
        }

        private void advanceCursor(double time)
        {
            if (cursor >= frames.Count)
                cursor = frames.Count - 1;

            if (cursor < 0)
            {
                cursor = 0;
                return;
            }

            if (frames[cursor].Time > time)
                cursor = 0;

            while (cursor + 1 < frames.Count && frames[cursor + 1].Time <= time)
                cursor++;
        }

        private static Vector2 interpolate(double time, OsuReplayFrame start, OsuReplayFrame end)
        {
            double duration = end.Time - start.Time;

            if (duration <= 0)
                return start.Position;

            double progress = Math.Clamp((time - start.Time) / duration, 0, 1);
            return start.Position + (end.Position - start.Position) * (float)progress;
        }
    }
}
