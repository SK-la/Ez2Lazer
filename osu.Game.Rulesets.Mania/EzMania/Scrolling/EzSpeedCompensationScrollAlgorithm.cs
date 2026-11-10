// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;

namespace osu.Game.Rulesets.Mania.EzMania.Scrolling
{
    /// <summary>
    /// 在既有滚动算法上叠一层匀变速下落：速度随时间线性变化，面积守恒，
    /// 因此走完整容器的总时长不变，改变的只是运动在屏幕上的分布。
    /// </summary>
    /// <remarks>
    /// 令 r 为"到判定线速度 / 常规速度"，位置律（u 为距判定线的归一化位置，1 = 容器顶）：
    /// <code>
    /// w = 1 − u
    /// u' = 1 − [(2 − r)·w + (r − 1)·w²]
    /// </code>
    /// 端点保持（0→0，1→1），导数为 r 在 u=0、2−r 在 u=1；容器外按端点速度线性延伸，避免反演折叠。
    /// r = 1 时与原算法完全等价。
    /// </remarks>
    public sealed class EzSpeedCompensationScrollAlgorithm : IScrollAlgorithm
    {
        private const float degenerate_epsilon = 1e-3f;

        private readonly IScrollAlgorithm inner;
        private readonly float compensation;

        public EzSpeedCompensationScrollAlgorithm(IScrollAlgorithm inner, float compensation)
        {
            this.inner = inner;
            this.compensation = Math.Clamp(compensation, 0.2f, 1.8f);
        }

        public double GetDisplayStartTime(double originTime, float offset, double timeRange, float scrollLength)
            => Math.Min(TimeAt(-offset, originTime, timeRange, scrollLength) - timeRange,
                        inner.GetDisplayStartTime(originTime, offset, timeRange, scrollLength));

        public float GetLength(double startTime, double endTime, double timeRange, float scrollLength)
            => -PositionAt(startTime, endTime, timeRange, scrollLength);

        public float PositionAt(double time, double currentTime, double timeRange, float scrollLength, double? originTime = null)
            => project(inner.PositionAt(time, currentTime, timeRange, scrollLength, originTime), scrollLength);

        public double TimeAt(float position, double currentTime, double timeRange, float scrollLength)
            => inner.TimeAt(unproject(position, scrollLength), currentTime, timeRange, scrollLength);

        public void Reset() => inner.Reset();

        private float project(float position, float scrollLength)
        {
            if (scrollLength <= 0f || Math.Abs(compensation - 1f) < degenerate_epsilon)
                return position;

            float u = position / scrollLength;

            if (u < 0f)
                return scrollLength * (compensation * u);

            if (u > 1f)
                return scrollLength * (1f + (2f - compensation) * (u - 1f));

            float w = 1f - u;
            return scrollLength * (1f - ((2f - compensation) * w + (compensation - 1f) * w * w));
        }

        private float unproject(float position, float scrollLength)
        {
            if (scrollLength <= 0f || Math.Abs(compensation - 1f) < degenerate_epsilon)
                return position;

            float v = position / scrollLength;

            if (v < 0f)
                return scrollLength * (v / compensation);

            if (v > 1f)
                return scrollLength * (1f + (v - 1f) / (2f - compensation));

            float a = compensation - 1f;
            float b = 2f - compensation;
            float c = v - 1f;
            float discriminant = b * b - 4f * a * c;

            if (discriminant < 0f)
                return position;

            float w = (-b + MathF.Sqrt(discriminant)) / (2f * a);
            return scrollLength * (1f - Math.Clamp(w, 0f, 1f));
        }
    }
}
