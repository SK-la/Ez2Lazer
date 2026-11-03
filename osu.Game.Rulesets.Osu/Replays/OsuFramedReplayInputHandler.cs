// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Framework.Utils;
using osu.Game.Replays;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays
{
    public class OsuFramedReplayInputHandler : FramedReplayInputHandler<OsuReplayFrame>
    {
        public OsuFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(OsuReplayFrame frame) => frame.Actions.Any();

        /// <summary>
        /// 跨越保持相同按键的跟踪帧。按下或抬起仍会停下时钟。
        /// </summary>
        /// <remarks>
        /// Aspire 滑条在每个 tick 放一帧。每更新步进一帧会卡住双向 seek。
        /// 不改变按住键的帧可在本调用中跨过；判定内核在 tick 时刻读 <see cref="PositionAt"/>，
        /// 不需要时钟落在那里。
        /// </remarks>
        public override double? SetFrameFromTime(double time)
        {
            double? stepped = base.SetFrameFromTime(time);

            if (stepped == null || !HasFrames)
                return stepped;

            while (stepped.Value != time)
            {
                OsuReplayFrame? current = CurrentFrame;
                OsuReplayFrame? next = NextFrame;

                if (current == null || next == null || next.Time > time || !sameHold(current, next))
                    break;

                double? further = base.SetFrameFromTime(time);

                if (further == null || further.Value == stepped.Value)
                    break;

                stepped = further;
            }

            return stepped;
        }

        /// <summary><paramref name="time"/> 处的光标位置，含未绘制的采样。</summary>
        public Vector2 PositionAt(double time)
        {
            if (!HasFrames)
                return Vector2.Zero;

            if (time <= Frames[0].Time)
                return ((OsuReplayFrame)Frames[0]).Position;

            int index = frameIndexAt(time);
            var start = (OsuReplayFrame)Frames[index];
            var end = (OsuReplayFrame)Frames[Math.Min(index + 1, Frames.Count - 1)];

            if (end.Time <= start.Time)
                return start.Position;

            return Interpolation.ValueAt(time, start.Position, end.Position, start.Time, end.Time);
        }

        public bool IsPressedAt(OsuAction action, double time)
            => HasFrames && time >= Frames[0].Time && pressedAt(frameIndexAt(time), action);

        public bool WasPressedAt(OsuAction action, double time)
        {
            int index = frameIndexAt(time);

            if (index < 0 || Frames[index].Time >= time)
                index--;

            return pressedAt(index, action);
        }

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            Vector2 position = Interpolation.ValueAt(CurrentTime, StartFrame.Position, EndFrame.Position, StartFrame.Time, EndFrame.Time);

            // 回放保留真实跟踪位置，屏外 tick 仍能判定。
            // 光标本身留在视口内；1e8 量级坐标会卡住 GPU。
            if (!StableViewportBody.CanPlaceFieldPosition(position))
            {
                position = new Vector2(
                    Math.Clamp(position.X, -StableViewportBody.MAX_VIEWPORT_DIMENSION, StableViewportBody.MAX_VIEWPORT_DIMENSION),
                    Math.Clamp(position.Y, -StableViewportBody.MAX_VIEWPORT_DIMENSION, StableViewportBody.MAX_VIEWPORT_DIMENSION));
            }

            inputs.Add(new MousePositionAbsoluteInput { Position = GamefieldToScreenSpace(position) });
            inputs.Add(new ReplayState<OsuAction> { PressedActions = CurrentFrame?.Actions ?? new List<OsuAction>() });
        }

        private int frameIndexAt(double time)
        {
            int low = 0;
            int high = Frames.Count - 1;

            while (low < high)
            {
                int middle = (low + high + 1) / 2;

                if (Frames[middle].Time <= time)
                    low = middle;
                else
                    high = middle - 1;
            }

            return low;
        }

        private bool pressedAt(int index, OsuAction action)
            => index >= 0 && index < Frames.Count && ((OsuReplayFrame)Frames[index]).Actions.Contains(action);

        private static bool sameHold(OsuReplayFrame current, OsuReplayFrame next)
        {
            if (current.Actions.Count != next.Actions.Count)
                return false;

            foreach (var action in current.Actions)
            {
                if (!next.Actions.Contains(action))
                    return false;
            }

            return true;
        }
    }
}
