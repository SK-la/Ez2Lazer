// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays
{
    /// <summary>
    /// 共用的 osu! 自动播放：移动、交替按键、转盘与滑条检查点。
    /// </summary>
    /// <remarks>
    /// 子类只决定如何采样滑条身位。普通自动播放按时长走，整段滑条每个间隔一帧，aspire 谱面承担不起。
    /// Aspire 改走绘制几何。两者仍共用转盘重叠处理与检查点恢复，否则后续物件会删掉滑条跟踪中的帧。
    /// </remarks>
    public abstract class OsuAutoplayCore : OsuAutoGeneratorBase
    {
        public const double MIN_FRAME_SEPARATION_FOR_ALTERNATING = 266;

        public new OsuBeatmap Beatmap => (OsuBeatmap)base.Beatmap;

        /// <summary>
        /// 是否使用延迟移动，使光标在每个物件上停留尽可能久。主要用于 Autopilot。
        /// </summary>
        public bool DelayedMovements;

        private readonly HitWindows defaultHitWindows;

        private Easing preferredEasing => DelayedMovements ? Easing.InOutCubic : Easing.Out;

        /// <summary>
        /// 偶数用左键，奇数用右键。跟踪上一次选择以便交替。
        /// </summary>
        private int buttonIndex;

        protected OsuAutoplayCore(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            : base(beatmap, mods)
        {
            defaultHitWindows = new OsuHitWindows();
            defaultHitWindows.SetDifficulty(Beatmap.Difficulty.OverallDifficulty);
        }

        /// <summary>
        /// 转盘自动播放是否按 osu!stable 加速，而非立刻以目标转速旋转。
        /// </summary>
        protected virtual bool AccelerateSpinners => false;

        /// <summary>
        /// 该光标位置是否可写入回放。
        /// </summary>
        /// <remarks>
        /// 普通谱面留在游玩区域内。Aspire 路径会跑到渲染器无法绘制的坐标；
        /// 提交那些帧会把游玩光标移过去并卡住 GPU。
        /// </remarks>
        protected virtual bool AcceptCursorPosition(Vector2 position)
            => float.IsFinite(position.X) && float.IsFinite(position.Y);

        /// <summary>
        /// 在按住 <paramref name="action"/> 时跟随 <paramref name="slider"/> 身位的帧。
        /// </summary>
        /// <remarks>
        /// 不含头按下与抬键；那些由 core 添加。
        /// </remarks>
        protected abstract void AddSliderBodyFrames(Slider slider, OsuAction action);

        public override Replay Generate()
        {
            if (Beatmap.HitObjects.Count == 0)
                return Replay;

            buttonIndex = 0;
            var spinnerFrames = new List<(double Start, double End, List<OsuReplayFrame> Frames)>();

            AddFrameToReplay(new OsuReplayFrame(Beatmap.HitObjects[0].StartTime - 1500, new Vector2(256, 500)));

            for (int i = 0; i < Beatmap.HitObjects.Count; i++)
            {
                OsuHitObject h = Beatmap.HitObjects[i];

                if (DelayedMovements && i > 0)
                    addDelayedMovements(h, Beatmap.HitObjects[i - 1]);

                addHitObjectReplay(h);

                if (h is Spinner spinner)
                {
                    spinnerFrames.Add((spinner.StartTime, spinner.EndTime, Frames.Cast<OsuReplayFrame>()
                        .Where(frame => frame.Time >= spinner.StartTime && frame.Time <= spinner.EndTime)
                        .Select(frame => new OsuReplayFrame(frame.Time, frame.Position, frame.Actions.ToArray()))
                        .ToList()));
                }
            }

            // 落在转盘内的头按下必须保留。重叠转盘共用一个光标，
            // 因此下一个转盘延续上一角度，而非对着它重新开始。
            List<OsuReplayFrame>? previousRotation = null;
            double previousSpinnerEnd = double.NegativeInfinity;

            foreach (var spinner in spinnerFrames)
            {
                if (previousRotation != null && spinner.Start < previousSpinnerEnd && spinner.Frames.Count > 1)
                    alignSpinnerContinuation(previousRotation, spinner.Frames, spinner.Start);

                previousRotation = spinner.Frames;
                previousSpinnerEnd = spinner.End;

                double[] headTimes = Beatmap.HitObjects
                    .Where(obj => obj is not Spinner && obj.StartTime >= spinner.Start && obj.StartTime <= spinner.End)
                    .Select(obj => obj.StartTime)
                    .ToArray();

                bool nearHead(double time) => headTimes.Any(head => Math.Abs(head - time) <= KEY_UP_DELAY + 1);

                Frames.RemoveAll(frame => frame.Time >= spinner.Start && frame.Time <= spinner.End && !nearHead(frame.Time));

                foreach (var frame in spinner.Frames)
                {
                    if (nearHead(frame.Time))
                        continue;

                    int previous = FindInsertionIndex(frame) - 1;

                    if (previous >= 0 && ((OsuReplayFrame)Frames[previous]).Actions.Count > 0)
                        frame.Actions = new List<OsuAction>(((OsuReplayFrame)Frames[previous]).Actions);

                    AddFrameToReplay(frame);
                }
            }

            restoreSliderCheckpoints();
            return Replay;
        }

        /// <summary>
        /// 让 <paramref name="next"/> 与 <paramref name="previous"/> 同相位、同方向。
        /// </summary>
        private static void alignSpinnerContinuation(List<OsuReplayFrame> previous, List<OsuReplayFrame> next, double start)
        {
            int preceding = previous.FindLastIndex(frame => frame.Time <= start);

            if (preceding < 0)
                return;

            Vector2 position = previous[preceding].Position;

            if (preceding + 1 < previous.Count)
            {
                OsuReplayFrame following = previous[preceding + 1];

                if (following.Time > previous[preceding].Time)
                {
                    float progress = (float)((start - previous[preceding].Time) / (following.Time - previous[preceding].Time));
                    position = Vector2.Lerp(position, following.Position, progress);
                }
            }

            float direction = turnDirection(previous) * turnDirection(next) < 0 ? -1 : 1;
            float sourceAngle = angleOf(next[0].Position);
            float targetAngle = angleOf(position);

            foreach (var frame in next)
                frame.Position = SPINNER_CENTRE + CirclePosition(targetAngle + direction * (angleOf(frame.Position) - sourceAngle), SPIN_RADIUS);
        }

        private static float angleOf(Vector2 position)
            => MathF.Atan2(position.Y - SPINNER_CENTRE.Y, position.X - SPINNER_CENTRE.X);

        private static float turn(Vector2 from, Vector2 to)
        {
            float delta = angleOf(to) - angleOf(from);
            return MathF.Atan2(MathF.Sin(delta), MathF.Cos(delta));
        }

        private static float turnDirection(List<OsuReplayFrame> frames)
            => frames.Zip(frames.Skip(1), (a, b) => turn(a.Position, b.Position)).LastOrDefault(delta => Math.Abs(delta) > 0.001f);

        /// <summary>
        /// 在后续物件替换跟踪帧之后，回访滑条 tick、折返与尾。
        /// </summary>
        /// <remarks>
        /// 滑条进行中开始的物件会删掉直到该物件结束的所有帧。那段里的检查点随后会离开曲线被插值并 miss。
        /// 本方法在那些时刻把光标放回曲线上，且不发明判定窗给不出的判定。
        /// </remarks>
        private void restoreSliderCheckpoints()
        {
            var visits = new List<(double Time, Slider Slider)>();
            var objects = Beatmap.HitObjects;

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] is not Slider slider || slider.Duration <= 0)
                    continue;

                foreach (var nested in slider.NestedHitObjects)
                {
                    if (nested is not SliderTick and not SliderRepeat and not SliderTailCircle)
                        continue;

                    double time = nested.StartTime;

                    if (nested is SliderTailCircle)
                    {
                        time = slider.ClassicSliderBehaviour
                            ? Math.Max(slider.StartTime + Math.Floor(slider.Duration / 2), Math.Floor(slider.EndTime) + SliderEventGenerator.TAIL_LENIENCY)
                            : Math.Max(slider.StartTime, slider.EndTime + SliderEventGenerator.TAIL_LENIENCY);
                    }

                    visits.Add((time, slider));
                }
            }

            foreach (var visit in visits.OrderBy(visit => visit.Time))
            {
                double arrival = Math.Max(visit.Slider.StartTime, visit.Time - (visit.Slider.ClassicSliderBehaviour ? 5 : 1));

                double previousEvent = objects.Where(obj => obj is not Spinner && obj.StartTime < visit.Time)
                                              .Select(obj => obj.StartTime)
                                              .Concat(visits.Where(other => other.Time < visit.Time).Select(other => other.Time))
                                              .DefaultIfEmpty(double.NegativeInfinity)
                                              .Max();

                if (previousEvent >= arrival)
                    arrival = (previousEvent + visit.Time) / 2;

                bool headConflict = objects.Any(obj => obj is not Spinner && Math.Abs(obj.StartTime - visit.Time) < 0.001);
                bool checkpointConflict = visits.Any(other => !ReferenceEquals(other.Slider, visit.Slider) && Math.Abs(other.Time - visit.Time) < 0.001);

                if (headConflict && !checkpointConflict && tryDelayHeadForCheckpoint(visit, arrival, objects, visits))
                    continue;

                if (headConflict || checkpointConflict)
                    continue;

                placeCheckpoint(visit.Slider, arrival, visit.Time);
            }
        }

        /// <summary>
        /// 先按住检查点，再在数毫秒后于头上按下另一键。
        /// </summary>
        /// <remarks>
        /// 复用已按下的键不会产生新 hit。仅当判定窗仍接受该偏移且中间没有其它到期物件时才延迟头。
        /// </remarks>
        private bool tryDelayHeadForCheckpoint((double Time, Slider Slider) visit, double arrival, IReadOnlyList<OsuHitObject> objects, List<(double Time, Slider Slider)> visits)
        {
            const double head_delay = 5;

            OsuHitObject[] heads = objects.Where(obj => obj is not Spinner && Math.Abs(obj.StartTime - visit.Time) < 0.001).ToArray();
            OsuHitObject? head = heads.Length == 1 ? heads[0] : null;
            HitWindows? windows = head is Slider nextSlider ? nextSlider.HeadCircle?.HitWindows : head?.HitWindows;
            double delayedTime = visit.Time + head_delay;
            bool occupied = objects.Any(obj => obj.StartTime > visit.Time && obj.StartTime <= delayedTime)
                            || visits.Any(other => other.Time > visit.Time && other.Time <= delayedTime);
            int before = Frames.FindLastIndex(frame => frame.Time < arrival);

            if (head == null || windows == null || windows.ResultFor(head_delay) == HitResult.None || occupied || before < 0)
                return false;

            if (!tryCursorPosition(visit.Slider, arrival, out Vector2 atArrival) || !tryCursorPosition(visit.Slider, visit.Time, out Vector2 atCheckpoint))
                return false;

            var previous = (OsuReplayFrame)Frames[before];
            var hold = previous.Actions.Contains(OsuAction.LeftButton) ? OsuAction.LeftButton
                : previous.Actions.Contains(OsuAction.RightButton) ? OsuAction.RightButton
                : OsuAction.LeftButton;
            var press = hold == OsuAction.LeftButton ? OsuAction.RightButton : OsuAction.LeftButton;

            if (!AcceptCursorPosition(head.StackedPosition))
                return false;

            Frames.RemoveAll(frame => frame.Time >= arrival && frame.Time <= delayedTime);
            AddFrameToReplay(new OsuReplayFrame(arrival, atArrival, hold));
            AddFrameToReplay(new OsuReplayFrame(visit.Time, atCheckpoint, hold));
            AddFrameToReplay(new OsuReplayFrame(delayedTime, head.StackedPosition, press));

            double nextHead = objects.Where(obj => obj.StartTime > visit.Time).Select(obj => obj.StartTime).DefaultIfEmpty(double.PositiveInfinity).Min();

            foreach (var following in Frames.Cast<OsuReplayFrame>().Where(frame => frame.Time > delayedTime && frame.Time < nextHead))
            {
                if (following.Actions.Count == 0)
                    break;

                following.Actions.Clear();
                following.Actions.Add(press);
            }

            return true;
        }

        private void placeCheckpoint(Slider slider, double arrival, double time)
        {
            if (!tryCursorPosition(slider, arrival, out Vector2 atArrival) || !tryCursorPosition(slider, time, out Vector2 atTime))
                return;

            int index = FindInsertionIndex(new OsuReplayFrame(arrival, Vector2.Zero)) - 1;

            if (index < 0)
                return;

            var frame = (OsuReplayFrame)Frames[index];
            int endIndex = FindInsertionIndex(new OsuReplayFrame(time, Vector2.Zero));
            var endFrame = (OsuReplayFrame)Frames[endIndex - 1];
            OsuAction[] originalEndActions = endFrame.Actions.ToArray();

            // 先前抬键可能让滑条没有按住。补回一键以便跟踪检查点。
            OsuAction[] actions = frame.Actions.Count == 0 ? new[] { OsuAction.LeftButton } : frame.Actions.ToArray();
            bool suppliedHold = frame.Actions.Count == 0;

            foreach (var intermediate in Frames.Skip(index + 1).Take(endIndex - index - 1).Cast<OsuReplayFrame>())
            {
                if (!tryCursorPosition(slider, intermediate.Time, out Vector2 atIntermediate))
                    continue;

                intermediate.Position = atIntermediate;

                if (intermediate.Actions.Count == 0)
                {
                    intermediate.Actions.Add(actions[0]);
                    suppliedHold = true;
                }
            }

            AddFrameToReplay(new OsuReplayFrame(arrival, atArrival, actions));
            AddFrameToReplay(new OsuReplayFrame(time, atTime, originalEndActions.Length == 0 ? actions : originalEndActions));

            if (suppliedHold && originalEndActions.Length == 0)
            {
                double releaseTime = time + 0.5;
                int nextIndex = FindInsertionIndex(new OsuReplayFrame(time, Vector2.Zero));

                if ((nextIndex == Frames.Count || Frames[nextIndex].Time > releaseTime)
                    && tryCursorPosition(slider, releaseTime, out Vector2 atRelease))
                    AddFrameToReplay(new OsuReplayFrame(releaseTime, atRelease));
            }
        }

        private bool tryCursorPosition(Slider slider, double time, out Vector2 position)
        {
            position = slider.StackedPositionAt(Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1));
            // 即使无法绘制也保留。回放时钟用它读 tick；光标 drawable 会钳制。
            // 丢掉采样才会让长路径上的每个检查点都 miss。
            return float.IsFinite(position.X) && float.IsFinite(position.Y);
        }

        private void addDelayedMovements(OsuHitObject h, OsuHitObject prev)
        {
            double endTime = prev.GetEndTime();

            HitWindows? hitWindows = h switch
            {
                HitCircle hitCircle => hitCircle.HitWindows,
                Slider slider => slider.TailCircle.HitWindows,
                Spinner => defaultHitWindows,
                _ => null,
            };

            Debug.Assert(hitWindows != null);

            if (h.StartTime - hitWindows.WindowFor(HitResult.Miss) > endTime + hitWindows.WindowFor(HitResult.Meh) + 50)
                addDelayedPair(h, prev, endTime, hitWindows.WindowFor(HitResult.Meh), hitWindows.WindowFor(HitResult.Miss));
            else if (h.StartTime - hitWindows.WindowFor(HitResult.Meh) > endTime + hitWindows.WindowFor(HitResult.Meh) + 50)
                addDelayedPair(h, prev, endTime, hitWindows.WindowFor(HitResult.Meh), hitWindows.WindowFor(HitResult.Meh));
            else if (h.StartTime - hitWindows.WindowFor(HitResult.Ok) > endTime + hitWindows.WindowFor(HitResult.Ok) + 50)
                addDelayedPair(h, prev, endTime, hitWindows.WindowFor(HitResult.Ok), hitWindows.WindowFor(HitResult.Ok));
        }

        private void addDelayedPair(OsuHitObject h, OsuHitObject prev, double endTime, double releaseWindow, double approachWindow)
        {
            if (prev is not Spinner && h.StartTime - endTime < 1000 && AcceptCursorPosition(prev.StackedEndPosition))
                AddFrameToReplay(new OsuReplayFrame(endTime + releaseWindow, prev.StackedEndPosition));

            if (h is not Spinner && AcceptCursorPosition(h.StackedPosition))
                AddFrameToReplay(new OsuReplayFrame(h.StartTime - approachWindow, h.StackedPosition));
        }

        private void addHitObjectReplay(OsuHitObject h)
        {
            Vector2 startPosition = h.StackedPosition;
            Easing easing = preferredEasing;
            float spinnerDirection = -1;

            if (h is Spinner spinner)
            {
                if (spinner.SpinsRequired == 0)
                    return;

                calcSpinnerStartPosAndDirection(((OsuReplayFrame)Frames[^1]).Position, out startPosition, out spinnerDirection);

                if ((SPINNER_CENTRE - ((OsuReplayFrame)Frames[^1]).Position).Length > SPIN_RADIUS)
                    easing = Easing.In;
            }
            else if (!AcceptCursorPosition(startPosition))
            {
                return;
            }

            if (Frames.Count > 0)
                moveToHitObject(h, startPosition, easing);

            addHitObjectClickFrames(h, startPosition, spinnerDirection);
        }

        private static void calcSpinnerStartPosAndDirection(Vector2 prevPos, out Vector2 startPosition, out float spinnerDirection)
        {
            Vector2 spinCentreOffset = SPINNER_CENTRE - prevPos;
            float distFromCentre = spinCentreOffset.Length;
            float distToTangentPoint = MathF.Sqrt(distFromCentre * distFromCentre - SPIN_RADIUS * SPIN_RADIUS);

            if (distFromCentre > SPIN_RADIUS)
            {
                float angle = MathF.Asin(SPIN_RADIUS / distFromCentre);

                spinnerDirection = angle > 0 ? -1 : 1;

                spinCentreOffset.X = spinCentreOffset.X * MathF.Cos(angle) - spinCentreOffset.Y * MathF.Sin(angle);
                spinCentreOffset.Y = spinCentreOffset.X * MathF.Sin(angle) + spinCentreOffset.Y * MathF.Cos(angle);

                spinCentreOffset.Normalize();
                spinCentreOffset *= distToTangentPoint;

                startPosition = prevPos + spinCentreOffset;
            }
            else if (spinCentreOffset.Length > 0)
            {
                startPosition = SPINNER_CENTRE - spinCentreOffset * (SPIN_RADIUS / spinCentreOffset.Length);
                spinnerDirection = 1;
            }
            else
            {
                startPosition = SPINNER_CENTRE + new Vector2(0, -SPIN_RADIUS);
                spinnerDirection = 1;
            }
        }

        private void moveToHitObject(OsuHitObject h, Vector2 targetPos, Easing easing)
        {
            OsuReplayFrame lastFrame = (OsuReplayFrame)Frames[^1];

            double waitTime = h.StartTime - Math.Max(0.0, h.TimePreempt - ApplyModsToRate(h.StartTime - h.TimePreempt, 100));
            bool hasWaited = false;

            if (waitTime > lastFrame.Time)
            {
                lastFrame = new OsuReplayFrame(waitTime, lastFrame.Position) { Actions = lastFrame.Actions };
                hasWaited = true;
                AddFrameToReplay(lastFrame);
            }

            double timeDifference = ApplyModsToTimeDelta(lastFrame.Time, h.StartTime);
            OsuReplayFrame? lastLastFrame = Frames.Count >= 2 ? (OsuReplayFrame)Frames[^2] : null;

            if (timeDifference >= 0)
            {
                if (lastLastFrame != null && lastFrame is OsuKeyUpReplayFrame && !hasWaited)
                    lastFrame.Position = Interpolation.ValueAt(lastFrame.Time, lastFrame.Position, targetPos, lastLastFrame.Time, h.StartTime, easing);

                Vector2 lastPosition = lastFrame.Position;

                for (double time = lastFrame.Time + GetFrameDelay(lastFrame.Time); time < h.StartTime; time += GetFrameDelay(time))
                {
                    Vector2 currentPosition = Interpolation.ValueAt(time, lastPosition, targetPos, lastFrame.Time, h.StartTime, easing);
                    AddFrameToReplay(new OsuReplayFrame((int)time, currentPosition) { Actions = lastFrame.Actions });
                }
            }

            if (timeDifference >= 0 && timeDifference < MIN_FRAME_SEPARATION_FOR_ALTERNATING)
                buttonIndex++;
            else
                buttonIndex = 0;
        }

        private void addHitObjectClickFrames(OsuHitObject h, Vector2 startPosition, float spinnerDirection)
        {
            var action = buttonIndex % 2 == 0 ? OsuAction.LeftButton : OsuAction.RightButton;
            var startFrame = new OsuReplayFrame(h.StartTime, startPosition, action);

            double hEndTime = h.GetEndTime() + KEY_UP_DELAY;
            int endDelay = h is Spinner ? 1 : 0;
            var endFrame = new OsuKeyUpReplayFrame(hEndTime + endDelay, h.StackedEndPosition);

            int index = FindInsertionIndex(startFrame) - 1;

            if (index >= 0)
            {
                var previousFrame = (OsuReplayFrame)Frames[index];
                var previousActions = previousFrame.Actions;

                if (previousActions.Any())
                {
                    if (previousActions.Contains(action))
                    {
                        action = action == OsuAction.LeftButton ? OsuAction.RightButton : OsuAction.LeftButton;
                        startFrame.Actions.Clear();
                        startFrame.Actions.Add(action);
                    }

                    int endIndex = FindInsertionIndex(endFrame);

                    if (index < Frames.Count - 1)
                        Frames.RemoveRange(index + 1, Math.Max(0, endIndex - (index + 1)));

                    for (int j = index + 1; j < Frames.Count; ++j)
                    {
                        var frame = (OsuReplayFrame)Frames[j];

                        if (j < Frames.Count - 1 || frame.Actions.SequenceEqual(previousActions))
                        {
                            frame.Actions.Clear();
                            frame.Actions.Add(action);
                        }
                    }
                }
            }

            AddFrameToReplay(startFrame);

            const float spin_rpm = 0.05f / (2 * MathF.PI) * 60000;
            float radsPerMillisecond = float.DegreesToRadians(spin_rpm * 360) / 60000;

            switch (h)
            {
                case Spinner spinner:
                    addSpinnerFrames(spinner, startPosition, spinnerDirection, action, radsPerMillisecond, endFrame);
                    break;

                case Slider slider:
                    AddSliderBodyFrames(slider, action);

                    if (float.IsFinite(slider.StackedEndPosition.X) && float.IsFinite(slider.StackedEndPosition.Y))
                        AddFrameToReplay(new OsuReplayFrame(slider.EndTime, slider.StackedEndPosition, action));

                    break;
            }

            if (Frames[^1].Time <= endFrame.Time && float.IsFinite(endFrame.Position.X) && float.IsFinite(endFrame.Position.Y))
                AddFrameToReplay(endFrame);
        }

        private void addSpinnerFrames(Spinner spinner, Vector2 startPosition, float spinnerDirection, OsuAction action, float radsPerMillisecond, OsuKeyUpReplayFrame endFrame)
        {
            Vector2 difference = startPosition - SPINNER_CENTRE;
            float radius = difference.Length;
            float angle = radius == 0 ? 0 : MathF.Atan2(difference.Y, difference.X);
            double previousFrame = spinner.StartTime;
            double legacyAngularVelocity = 0;

            double spinnerFrameDelay(double time) => Math.Min(GetFrameDelay(time), ApplyModsToRate(time, Math.PI / 2 / radsPerMillisecond));
            double legacyAcceleration = 0.00008 + Math.Max(0, (5000 - (int)spinner.Duration) / 1000.0 / 2000);

            for (double nextFrame = spinner.StartTime + spinnerFrameDelay(spinner.StartTime); nextFrame < spinner.EndTime; nextFrame += spinnerFrameDelay(nextFrame))
            {
                double t = ApplyModsToTimeDelta(previousFrame, nextFrame) * spinnerDirection;
                angle = advanceSpinner(angle, t, previousFrame == spinner.StartTime, ref legacyAngularVelocity, legacyAcceleration, radsPerMillisecond);

                Vector2 pos = SPINNER_CENTRE + CirclePosition(angle, SPIN_RADIUS);
                AddFrameToReplay(new OsuReplayFrame(nextFrame, pos, action));
                previousFrame = nextFrame;
            }

            double trailing = ApplyModsToTimeDelta(previousFrame, spinner.EndTime) * spinnerDirection;
            angle = advanceSpinner(angle, trailing, previousFrame == spinner.StartTime, ref legacyAngularVelocity, legacyAcceleration, radsPerMillisecond);

            Vector2 endPosition = SPINNER_CENTRE + CirclePosition(angle, SPIN_RADIUS);
            AddFrameToReplay(new OsuReplayFrame(spinner.EndTime, endPosition, action));
            endFrame.Position = endPosition;
        }

        private float advanceSpinner(float angle, double signedDelta, bool firstFrame, ref double legacyAngularVelocity, double legacyAcceleration, float radsPerMillisecond)
        {
            if (!AccelerateSpinners)
                return angle + (float)signedDelta * radsPerMillisecond;

            // 第一帧输入帧确立角度，不增加旋转增量。
            if (firstFrame)
                return angle;

            legacyAngularVelocity = Math.Min(0.05, legacyAngularVelocity + legacyAcceleration * Math.Abs(signedDelta));
            return angle + (float)(signedDelta * legacyAngularVelocity);
        }

        private class OsuKeyUpReplayFrame : OsuReplayFrame
        {
            public OsuKeyUpReplayFrame(double time, Vector2 position)
                : base(time, position)
            {
            }
        }
    }
}
