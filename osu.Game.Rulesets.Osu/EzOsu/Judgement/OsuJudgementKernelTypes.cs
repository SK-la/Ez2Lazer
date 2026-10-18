// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement
{
    /// <summary>内核收到的输入类型。判定时刻由调用方解析完毕，内核不再做任何偏移运算。</summary>
    public enum OsuInputKind
    {
        Press,
        Release,
        Move,
        FrameTick,
    }

    /// <summary>一条已解析的输入事件。</summary>
    public readonly struct OsuInputEvent
    {
        public readonly double Time;
        public readonly OsuAction Action;
        public readonly Vector2 Position;
        public readonly OsuInputKind Kind;

        public OsuInputEvent(double time, OsuAction action, Vector2 position, OsuInputKind kind)
        {
            Time = time;
            Action = action;
            Position = position;
            Kind = kind;
        }
    }

    /// <summary>
    /// 内核产出的权威判定。时间即内核结算该目标时采用的时刻，不再随驱动侧的当前时钟变化。
    /// </summary>
    public readonly struct OsuJudgementEvent
    {
        public readonly HitObject Target;
        public readonly HitResult Result;
        public readonly double Time;
        public readonly Vector2? CursorPosition;

        /// <summary>转盘累计旋转（仅转盘事件有值）。</summary>
        public readonly float? TotalRotation;

        public readonly double? SpinnerTimeStarted;
        public readonly double? SpinnerTimeCompleted;

        private OsuJudgementEvent(HitObject target, HitResult result, double time, Vector2? cursorPosition,
                                  float? totalRotation, double? spinnerTimeStarted, double? spinnerTimeCompleted)
        {
            Target = target;
            Result = result;
            Time = time;
            CursorPosition = cursorPosition;
            TotalRotation = totalRotation;
            SpinnerTimeStarted = spinnerTimeStarted;
            SpinnerTimeCompleted = spinnerTimeCompleted;
        }

        public static OsuJudgementEvent Hit(HitObject target, HitResult result, double time, Vector2? cursorPosition = null)
            => new OsuJudgementEvent(target, result, time, cursorPosition, null, null, null);

        public static OsuJudgementEvent SpinnerBody(Spinner spinner, HitResult result, double time, float totalRotation,
                                                    double? timeStarted, double? timeCompleted)
            => new OsuJudgementEvent(spinner, result, time, null, totalRotation, timeStarted, timeCompleted);
    }

    /// <summary>内核所需的环境派生参数；判定语义本身不依赖配置。</summary>
    public sealed class OsuJudgementKernelSettings
    {
        public bool UseLegacyNoteLock { get; init; }

        public double HittableRange { get; init; } = OsuHitWindows.MISS_WINDOW;

        public bool AllowMultiJudgementPerPress { get; init; }
    }

    /// <summary>
    /// 时间可寻址的输入采样源：内核在结算 deadline 时按该 deadline 的时刻取值，
    /// 因此判定结果与驱动侧的帧率无关。
    /// </summary>
    public interface IOsuJudgementSampleSource
    {
        Vector2 PositionAt(double time);

        bool IsPressedAt(OsuAction action, double time);

        /// <summary>
        /// 该时刻之前（不含该时刻）最近一次采样到的按键状态。
        /// 用于复刻「上一帧是否按住另一个键」的前置判断：同一时刻刚按下的键不能算作已按住。
        /// </summary>
        bool WasPressedAt(OsuAction action, double time);

        bool IsRewinding { get; }
    }

    /// <summary>能按 deadline 推进的判定状态。</summary>
    internal interface IOsuKernelDeadlineOwner
    {
        /// <summary>构造序，仅用于同刻 deadline 的稳定排序。</summary>
        int DeadlineOrder { get; }

        double? NextDeadline { get; }

        void SettleAt(double deadline);
    }

    internal static class OsuActionSampling
    {
        public static bool IsTracked(OsuAction action) => action is OsuAction.LeftButton or OsuAction.RightButton;
    }
}
