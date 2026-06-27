// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Timing;

namespace osu.Game.EzOsuGame.Clocks
{
    /// <summary>
    /// 谱面时基（beatmap-anchored clock）。
    ///
    /// 行为：
    /// - 内部用一个 <see cref="double"/> 跟踪 <see cref="CurrentTime"/>，每帧由 <see cref="ProcessFrame"/>
    ///   按「自上次帧以来的实际时间」单向推进（不回退，rewind 由 <see cref="Seek"/> 显式处理）；
    /// - <see cref="SourceClock"/> 是可选的参考时钟（一般是父 <see cref="FramedBeatmapClock"/> 或音频 Track）。
    ///   当不为 null 时，<see cref="SourceClock"/> 的 <see cref="IClock.IsRunning"/> 决定本时钟是否推进；
    ///   否则使用内部 <see cref="Stopwatch"/> 跟踪墙钟；
    /// - <see cref="Start"/> / <see cref="Stop"/> 启用 / 禁用推进。
    ///
    /// 设计意图：让 <see cref="osu.Game.Screens.Play.MasterGameplayClockContainer"/> 在切换为「谱面时基」时，
    /// 通过 <see cref="osu.Framework.Timing.ISourceChangeableClock.ChangeSource"/> 把这个 clock 作为
    /// <see cref="osu.Game.Beatmaps.FramedBeatmapClock"/> 的 SourceClock 使用，使所有播放 / 暂停 / 跳转行为
    /// 由 <see cref="osu.Game.Beatmaps.FramedBeatmapClock"/> 统一驱动；谱面时钟本身只关心时间推进。
    /// </summary>
    public sealed class EzBeatmapTimeSource : IEzBeatmapTimeSource
    {
        /// <summary>
        /// 可选的参考时钟：决定本时钟是否在推进（仅当 IsRunning=true）。
        /// 若为 null，则使用内部 <see cref="Stopwatch"/> 跟踪墙钟。
        /// </summary>
        public IClock? SourceClock { get; set; }

        private readonly Stopwatch wallWatch = new Stopwatch();

        private readonly Bindable<bool> isRunning = new Bindable<bool>();

        /// <summary>
        /// 当前谱面时钟的「正在推进」状态（用于 UI 绑定）。
        /// </summary>
        public Bindable<bool> IsRunning => isRunning;

        private double currentTime;

        public double CurrentTime
        {
            get => currentTime;
            private set => currentTime = value;
        }

        public double Rate { get; set; } = 1;

        public double ElapsedFrameTime { get; private set; }

        public double FramesPerSecond => wallWatch.IsRunning && wallWatch.ElapsedTicks > 0 ? 1000.0 / Stopwatch.GetElapsedTime(lastTickTime, Stopwatch.GetTimestamp()).TotalMilliseconds : 0;

        private long lastTickTime;

        /// <summary>
        /// IClock.IsRunning 的实现。
        /// </summary>
        bool IClock.IsRunning => isRunning.Value;

        /// <summary>
        /// 谱面时钟总开关（multiplayer / 暂停场景下关闭时停止推进）。
        /// 默认 true。
        /// </summary>
        public bool Enabled { get; set; } = true;

        public EzBeatmapTimeSource(IClock? sourceClock = null)
        {
            SourceClock = sourceClock;
        }

        public void ProcessFrame()
        {
            // 时钟被禁用时（multiplayer / 暂停等）不推进，但仍暴露当前时间。
            if (!Enabled)
            {
                ElapsedFrameTime = 0;
                isRunning.Value = false;
                return;
            }

            // 1. 决定参考源是否在运行。
            bool sourceRunning = SourceClock?.IsRunning ?? true;

            // 2. 计算 ElapsedFrameTime（毫秒）。
            double elapsed;
            if (SourceClock is IFrameBasedClock fbc)
            {
                fbc.ProcessFrame();
                elapsed = fbc.ElapsedFrameTime;
                sourceRunning = fbc.IsRunning;
            }
            else
            {
                long now = Stopwatch.GetTimestamp();
                if (!wallWatch.IsRunning)
                {
                    wallWatch.Restart();
                    lastTickTime = now;
                    elapsed = 0;
                }
                else
                {
                    elapsed = Stopwatch.GetElapsedTime(lastTickTime, now).TotalMilliseconds;
                    lastTickTime = now;
                }
            }

            isRunning.Value = sourceRunning;

            if (!sourceRunning)
            {
                ElapsedFrameTime = 0;
                return;
            }

            double delta = elapsed * Rate;
            ElapsedFrameTime = delta;

            // 单向推进；rewind 由 Seek 显式处理。
            if (delta > 0)
                CurrentTime += delta;
        }

        public bool Seek(double position)
        {
            CurrentTime = position;
            return true;
        }

        public void Reset()
        {
            CurrentTime = 0;
            ElapsedFrameTime = 0;
        }

        public void ResetSpeedAdjustments()
        {
            Rate = 1;
        }

        /// <summary>
        /// IAdjustableClock.Start()：开启推进。
        /// </summary>
        public void Start() => Enabled = true;

        /// <summary>
        /// IAdjustableClock.Stop()：关闭推进；保留当前时间。
        /// </summary>
        public void Stop() => Enabled = false;

        /// <summary>
        /// 调试用：手动设置内部时钟的当前时间。
        /// </summary>
        public void SetCurrentTime(double time)
        {
            CurrentTime = time;
            Logger.Log($"[Ez] EzBeatmapTimeSource.SetCurrentTime={time:F0}");
        }
    }
}
