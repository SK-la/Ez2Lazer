using System;
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
    ///   按墙钟单向推进（rewind 由 <see cref="Seek"/> 显式处理）；
    /// - 暂停恢复后的空白下落过渡由 <see cref="FrameStabilityContainer"/> 驱动显示时间；
    ///   本时钟在过渡期间冻结 gameplay 时间在 <see cref="PauseGameplayTime"/>；
    /// - <see cref="Start"/> / <see cref="Stop"/> 启用 / 禁用推进。
    ///
    /// 设计意图：让 <see cref="osu.Game.Screens.Play.MasterGameplayClockContainer"/> 在切换为「谱面时基」时，
    /// 通过 <see cref="ISourceChangeableClock.ChangeSource"/> 把这个 clock 作为
    /// <see cref="osu.Game.Beatmaps.FramedBeatmapClock"/> 的 SourceClock 使用。
    /// </summary>
    public class EzBeatmapTimeSource : IEzBeatmapTimeSource
    {
        /// <summary>
        /// 可选的参考时钟（legacy；当前谱面时基注入时一般为 null）。
        /// </summary>
        public IClock? SourceClock { get; set; }

        private readonly Stopwatch wallWatch = new Stopwatch();

        /// <summary>
        /// 当前谱面时钟的「正在推进」状态（用于 UI 绑定）。
        /// </summary>
        public Bindable<bool> IsRunning { get; } = new Bindable<bool>();

        public double CurrentTime { get; private set; }

        public double Rate { get; set; } = 1;

        public double ElapsedFrameTime { get; private set; }

        public double FramesPerSecond => wallWatch.IsRunning && wallWatch.ElapsedTicks > 0
            ? 1000.0 / Stopwatch.GetElapsedTime(lastTickTime, Stopwatch.GetTimestamp()).TotalMilliseconds
            : 0;

        private long lastTickTime;

        /// <summary>
        /// IClock.IsRunning 的实现。
        /// </summary>
        bool IClock.IsRunning => IsRunning.Value;

        /// <summary>
        /// 谱面时钟总开关（multiplayer / 暂停场景下关闭时停止推进）。
        /// 默认 true。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 暂停恢复时的虚拟 lead-in 窗口（毫秒）。设为 0 关闭过渡。
        /// </summary>
        public double ResumeLeadInWindowMs { get; set; }

        public double PauseGameplayTime { get; private set; }

        public bool IsInResumeLeadIn { get; private set; }

        public event Action? ResumeLeadInCompleted;

        private bool freezeGameplayTime;

        public EzBeatmapTimeSource(IClock? sourceClock = null)
        {
            SourceClock = sourceClock;
        }

        public void OnGameplayPaused(double time)
        {
            PauseGameplayTime = time;
        }

        public void BeginResumeLeadIn()
        {
            if (ResumeLeadInWindowMs <= 0)
                return;

            IsInResumeLeadIn = true;
            freezeGameplayTime = true;
            CurrentTime = PauseGameplayTime;
        }

        public void EndResumeLeadIn()
        {
            if (!IsInResumeLeadIn)
                return;

            IsInResumeLeadIn = false;
            freezeGameplayTime = false;
            ResumeLeadInCompleted?.Invoke();
        }

        public void ProcessFrame()
        {
            if (!Enabled)
            {
                ElapsedFrameTime = 0;
                IsRunning.Value = false;
                return;
            }

            if (freezeGameplayTime)
            {
                ElapsedFrameTime = 0;
                IsRunning.Value = true;
                return;
            }

            bool sourceRunning = SourceClock?.IsRunning ?? true;

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

            IsRunning.Value = sourceRunning;

            if (!sourceRunning)
            {
                ElapsedFrameTime = 0;
                return;
            }

            double delta = elapsed * Rate;
            ElapsedFrameTime = delta;

            if (delta > 0)
                CurrentTime += delta;
        }

        public bool Seek(double position)
        {
            CurrentTime = position;
            IsInResumeLeadIn = false;
            freezeGameplayTime = false;
            return true;
        }

        public void Reset()
        {
            CurrentTime = 0;
            ElapsedFrameTime = 0;
            PauseGameplayTime = 0;
            IsInResumeLeadIn = false;
            freezeGameplayTime = false;
        }

        public void ResetSpeedAdjustments()
        {
            Rate = 1;
        }

        public void Start() => Enabled = true;

        public void Stop() => Enabled = false;

        public void SetCurrentTime(double time)
        {
            CurrentTime = time;
            Logger.Log($"[Ez] EzBeatmapTimeSource.SetCurrentTime={time:F0}");
        }
    }
}
