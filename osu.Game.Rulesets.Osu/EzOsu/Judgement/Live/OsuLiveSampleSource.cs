// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement.Live
{
    /// <summary>
    /// 实机采样历史：内核结算 deadline 时按该时刻回查位置与按键，避免慢帧把判定推迟到当前帧。
    /// </summary>
    internal sealed class OsuLiveSampleSource : IOsuJudgementSampleSource
    {
        private const int max_samples = 2048;

        private readonly struct Sample
        {
            public readonly double Time;
            public readonly Vector2 Position;
            public readonly bool Left;
            public readonly bool Right;

            public Sample(double time, Vector2 position, bool left, bool right)
            {
                Time = time;
                Position = position;
                Left = left;
                Right = right;
            }
        }

        private readonly List<Sample> samples = new List<Sample>(max_samples);

        public bool IsRewinding { get; set; }

        /// <summary>
        /// 设置后，截止时刻的采样取自回放，而非时钟碰巧停在的帧。
        /// </summary>
        public Func<double, Vector2>? ReplayPositionAt { get; set; }

        public Func<double, OsuAction, bool>? ReplayIsPressedAt { get; set; }

        public Func<double, OsuAction, bool>? ReplayWasPressedAt { get; set; }

        public void Record(double time, Vector2 position, bool left, bool right)
        {
            if (samples.Count > 0)
            {
                var last = samples[samples.Count - 1];

                // 同一时刻多次记录只保留最后一次（例如按键在同一帧内被采样）。
                if (last.Time == time)
                {
                    samples[samples.Count - 1] = new Sample(time, position, left, right);
                    return;
                }

                if (time < last.Time)
                    samples.Clear();
            }

            samples.Add(new Sample(time, position, left, right));

            if (samples.Count > max_samples)
                samples.RemoveRange(0, samples.Count - max_samples);
        }

        public void Reset() => samples.Clear();

        /// <summary>丢弃该时刻之后的采样（倒带后重新前进时使用）。</summary>
        public void TruncateTo(double time)
        {
            int keep = 0;

            while (keep < samples.Count && samples[keep].Time <= time)
                keep++;

            if (keep < samples.Count)
                samples.RemoveRange(keep, samples.Count - keep);
        }

        public Vector2 PositionAt(double time)
        {
            if (ReplayPositionAt != null)
                return ReplayPositionAt(time);

            int index = findIndex(time);

            if (index < 0)
                return samples.Count > 0 ? samples[0].Position : Vector2.Zero;

            var start = samples[index];

            if (index + 1 >= samples.Count)
                return start.Position;

            var end = samples[index + 1];

            if (end.Time <= start.Time)
                return start.Position;

            float progress = (float)Math.Clamp((time - start.Time) / (end.Time - start.Time), 0, 1);
            return start.Position + (end.Position - start.Position) * progress;
        }

        public bool IsPressedAt(OsuAction action, double time)
        {
            if (ReplayIsPressedAt != null)
                return ReplayIsPressedAt(time, action);

            int index = findIndex(time);

            if (index < 0)
                return false;

            return pressedAt(samples[index], action);
        }

        public bool WasPressedAt(OsuAction action, double time)
        {
            if (ReplayWasPressedAt != null)
                return ReplayWasPressedAt(time, action);

            int index = findIndex(time, strict: true);

            if (index < 0)
                return false;

            return pressedAt(samples[index], action);
        }

        private static bool pressedAt(in Sample sample, OsuAction action) => action switch
        {
            OsuAction.LeftButton => sample.Left,
            OsuAction.RightButton => sample.Right,
            _ => false,
        };

        private int findIndex(double time, bool strict = false)
        {
            if (samples.Count == 0 || time < samples[0].Time)
                return -1;

            int low = 0;
            int high = samples.Count - 1;

            while (low < high)
            {
                int middle = (low + high + 1) / 2;

                if (strict ? samples[middle].Time < time : samples[middle].Time <= time)
                    low = middle;
                else
                    high = middle - 1;
            }

            if (strict && samples[low].Time >= time)
                return low - 1;

            return low;
        }
    }
}
