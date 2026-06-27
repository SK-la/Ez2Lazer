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
    /// - 暂停恢复 lead-in：<see cref="ResumeLeadInWindowMs"/> > 0 时，恢复瞬间记录暂停位置，
    ///   CurrentTime 在 lead-in 窗口内沿虚拟时间轴推进（SourceClock 不动），直到追上暂停位置才同步。
    ///
    /// 设计意图：让 <see cref="osu.Game.Screens.Play.MasterGameplayClockContainer"/> 在切换为「谱面时基」时，
    /// 通过 <see cref="ISourceChangeableClock.ChangeSource"/> 把这个 clock 作为
    /// <see cref="osu.Game.Beatmaps.FramedBeatmapClock"/> 的 SourceClock 使用，使所有播放 / 暂停 / 跳转行为
    /// 由 <see cref="osu.Game.Beatmaps.FramedBeatmapClock"/> 统一驱动；谱面时钟本身只关心时间推进。
    /// </summary>
    public class EzBeatmapTimeSource : IEzBeatmapTimeSource
    {
        /// <summary>
        /// 可选的参考时钟：决定本时钟是否在推进（仅当 IsRunning=true）。
        /// 若为 null，则使用内部 <see cref="Stopwatch"/> 跟踪墙钟。
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
        /// 暂停恢复时的虚拟 lead-in 窗口（毫秒）。
        /// 恢复后，CurrentTime 在这个窗口内逐步推进（smooth catch-up），不写回 SourceClock。
        /// 设为 0 或负值表示关闭虚拟 lead-in（直接 catch-up）。
        /// </summary>
        public double ResumeLeadInWindowMs { get; set; }

        /// <summary>
        /// 当前是否处于「暂停恢复 lead-in」阶段。
        /// </summary>
        public bool IsResuming { get; private set; }

        /// <summary>
        /// 暂停位置（毫秒）。仅在 IsResuming == true 时有意义。
        /// </summary>
        private double pausePosition;

        /// <summary>
        /// SourceClock 在暂停前的最后推进时间（用于 resume 检测）。
        /// </summary>
        private double lastSourceTime;

        /// <summary>
        /// 上一帧 IsRunning 的值（用于检测 paused → resumed 转换）。
        /// </summary>
        private bool lastIsRunning;

        /// <summary>
        /// 是否已记录过 SourceClock 的 Resume 位置（避免在第一次 ProcessFrame 时错误触发）。
        /// </summary>
        private bool hasRecordedResume;

        /// <summary>
        /// 当 SourceClock == null 时，lead-in 结束后时钟是否已停止。
        /// </summary>
        private bool leadInFinishedAndStopped;

        public EzBeatmapTimeSource(IClock? sourceClock = null)
        {
            SourceClock = sourceClock;
            lastIsRunning = false;
        }

        public void RecordPause(double sourceTimeAtPause)
        {
            // 当 SourceClock != null 且已注入时，ProcessFrame 会自动检测。
            // 此方法仅在 SourceClock == null（无音频 / beatmap clock）场景下由容器显式调用。
            if (SourceClock == null)
            {
                pausePosition = sourceTimeAtPause;
                IsResuming = true;
                hasRecordedResume = false;
                leadInFinishedAndStopped = false;
            }
        }

        public void ResetLeadIn()
        {
            IsResuming = false;
            pausePosition = 0;
            hasRecordedResume = false;
            leadInFinishedAndStopped = false;
        }

        public void ProcessFrame()
        {
            if (!Enabled)
            {
                ElapsedFrameTime = 0;
                IsRunning.Value = false;
                IsResuming = false;
                lastIsRunning = false;
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

            // 3. 记录 SourceClock 暂停前的最后推进时间（用于 resume 检测）。
            if (sourceRunning)
                lastSourceTime = SourceClock?.CurrentTime ?? CurrentTime;

            // 4. 自动检测暂停 → 恢复（仅在 SourceClock != null 时可用）。
            if (SourceClock != null)
            {
                // 从 paused 切换到 running：触发 lead-in。
                if (sourceRunning && !lastIsRunning && !hasRecordedResume && ResumeLeadInWindowMs > 0)
                {
                    pausePosition = lastSourceTime;
                    IsResuming = true;
                    hasRecordedResume = true;

                    if (SourceClock is IAdjustableClock adj)
                        adj.Seek(pausePosition);
                }

                // 从 running 切换到 paused：重置状态。
                if (!sourceRunning && lastIsRunning)
                {
                    IsResuming = false;
                    hasRecordedResume = false;
                }
            }

            IsRunning.Value = sourceRunning;
            lastIsRunning = sourceRunning;

            if (!sourceRunning)
            {
                ElapsedFrameTime = 0;
                IsResuming = false;
                return;
            }

            // 5. SourceClock == null 且 lead-in 已结束时，不再推进。
            if (leadInFinishedAndStopped)
            {
                ElapsedFrameTime = 0;
                return;
            }

            double delta = elapsed * Rate;
            ElapsedFrameTime = delta;

            // 6. Lead-in 阶段处理。
            // leadInStart = 当前 CurrentTime（暂停时刻的谱面时间）；
            // leadInEnd = pausePosition（暂停位置）。
            // CurrentTime 在 [leadInStart, leadInEnd] 区间内推进，直到追上 pausePosition。
            if (IsResuming && ResumeLeadInWindowMs > 0)
            {
                double leadInEnd = pausePosition;

                if (delta > 0)
                    CurrentTime += delta;

                if (CurrentTime >= leadInEnd)
                {
                    CurrentTime = leadInEnd;
                    ElapsedFrameTime = 0;
                    IsResuming = false;
                    hasRecordedResume = true;

                    // 当 SourceClock == null 时，lead-in 结束后停止。
                    if (SourceClock == null)
                        leadInFinishedAndStopped = true;
                }
            }
            else
            {
                if (delta > 0)
                    CurrentTime += delta;
            }
        }

        public bool Seek(double position)
        {
            CurrentTime = position;
            IsResuming = false;
            leadInFinishedAndStopped = false;
            return true;
        }

        public void Reset()
        {
            CurrentTime = 0;
            ElapsedFrameTime = 0;
            IsResuming = false;
            pausePosition = 0;
            hasRecordedResume = false;
            leadInFinishedAndStopped = false;
            lastIsRunning = false;
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
