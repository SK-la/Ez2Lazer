// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Input.Handlers;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// A container which consumes a parent gameplay clock and standardises frame counts for children.
    /// Will ensure a minimum of 50 frames per clock second is maintained, regardless of any system lag or seeks.
    /// </summary>
    [Cached(typeof(IGameplayClock))]
    [Cached(typeof(IFrameStableClock))]
    public sealed partial class FrameStabilityContainer : Container, IHasReplayHandler, IFrameStableClock
    {
        public ReplayInputHandler? ReplayInputHandler { get; set; }

        private int invalidBassTimeLogCount;

        /// <summary>
        /// The number of CPU milliseconds to spend at most during seek catch-up.
        /// </summary>
        private const double max_catchup_milliseconds = 10;

        /// <summary>
        /// Whether to enable frame-stable playback.
        /// </summary>
        internal bool FrameStablePlayback { get; set; } = true;

        private readonly Bindable<bool> isCatchingUp = new Bindable<bool>();

        private readonly Bindable<bool> waitingOnFrames = new Bindable<bool>();

        public double GameplayStartTime { get; }

        internal IGameplayClock? ParentGameplayClock { get; private set; }

        /// <summary>
        /// A clock which is used as reference for time, rate and running state.
        /// </summary>
        private IClock referenceClock = null!;

        /// <summary>
        /// A local manual clock which tracks the reference clock.
        /// Values are transferred from <see cref="referenceClock"/> each update call.
        /// </summary>
        private readonly ManualClock manualClock;

        /// <summary>
        /// The main framed clock which has stability applied to it.
        /// This gets exposed to children as an <see cref="IGameplayClock"/>.
        /// </summary>
        private readonly FramedClock framedClock;

        [Resolved]
        private OsuGame? game { get; set; }

        private readonly Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// The current direction of playback to be exposed to frame stable children.
        /// </summary>
        /// <remarks>
        /// Initially it is presumed that playback will proceed in the forward direction.
        /// </remarks>
        private int direction = 1;

        private PlaybackState state;

        private bool hasReplayAttached => ReplayInputHandler != null;

        private bool firstConsumption = true;

        public FrameStabilityContainer(double gameplayStartTime = double.MinValue)
        {
            RelativeSizeAxes = Axes.Both;

            framedClock = new FramedClock(manualClock = new ManualClock());

            GameplayStartTime = gameplayStartTime;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IGameplayClock? gameplayClock)
        {
            if (gameplayClock != null)
            {
                ParentGameplayClock = gameplayClock;
                IsPaused.BindTo(ParentGameplayClock.IsPaused);
            }

            referenceClock = gameplayClock ?? Clock;
            Clock = this;
        }

        public override bool UpdateSubTree()
        {
            stopwatch.Restart();

            int iterations = 0;

            // [Ez] 把一轮 update 切成「时钟推进」「drawable 子树」「其余」三段，供帧探针归因。
            // 子树是 FSC 之下的整个 ruleset 层级（播放区、物件、判定线），HUD 与框架调度在它之外。
            double clockTicks = 0;
            double subtreeTicks = 0;
            long allocBefore = DeepAlloc ? GC.GetAllocatedBytesForCurrentThread() : 0;
            long subtreeAlloc = 0;

            do
            {
                iterations++;

                long beforeClock = Stopwatch.GetTimestamp();

                // update clock is always trying to approach the aim time.
                // it should be provided as the original value each loop.
                updateClock();

                clockTicks += Stopwatch.GetTimestamp() - beforeClock;

                if (state == PlaybackState.NotValid)
                    break;

                long beforeSubtree = Stopwatch.GetTimestamp();
                long allocAtSubtreeStart = DeepAlloc ? GC.GetAllocatedBytesForCurrentThread() : 0;

                base.UpdateSubTree();
                UpdateSubTreeMasking();

                subtreeTicks += Stopwatch.GetTimestamp() - beforeSubtree;

                if (DeepAlloc)
                    subtreeAlloc += GC.GetAllocatedBytesForCurrentThread() - allocAtSubtreeStart;
            } while (state == PlaybackState.RequiresCatchUp && stopwatch.ElapsedMilliseconds < max_catchup_milliseconds);

            // [Ez] Catch-up loop count of this pass, read by the press-latency probe.
            EzLastUpdateIterations = iterations;

            double tickToMs = 1000.0 / Stopwatch.Frequency;
            subtreeProbeMs = subtreeTicks * tickToMs;
            clockProbeMs = clockTicks * tickToMs;
            subtreeProbeAllocBytes = subtreeAlloc;
            loopAllocProbeBytes = DeepAlloc ? GC.GetAllocatedBytesForCurrentThread() - allocBefore : 0;

            // [Ez] Frame boundary for the frame-stall probe. Must stay at the same position in every
            // pass, otherwise the delta between two calls is not a whole frame.
            EzOsuGame.Diagnostics.EzFrameStallDiagnostics.RecordFrame();

            return true;
        }

        /// <summary>[Ez] 上一轮 FSC 子树（<c>base.UpdateSubTree</c>）耗时；探针用，不参与游戏逻辑。</summary>
        public static double SubtreeProbeMs => subtreeProbeMs;

        /// <summary>[Ez] 上一轮 FSC 子树内分配字节数；仅 Deep 模式有值。</summary>
        public static long SubtreeProbeAllocBytes => subtreeProbeAllocBytes;

        /// <summary>[Ez] 上一轮 <see cref="UpdateSubTree"/> 全程（时钟 + 子树 + masking）的分配字节数；仅 Deep 模式有值。</summary>
        public static long LoopAllocProbeBytes => loopAllocProbeBytes;

        /// <summary>[Ez] 上一轮 <see cref="updateClock"/> 累计耗时（catch-up 时为多次之和）。</summary>
        public static double ClockProbeMs => clockProbeMs;

        private static double subtreeProbeMs;
        private static double clockProbeMs;
        private static long subtreeProbeAllocBytes;
        private static long loopAllocProbeBytes;

        /// <summary>[Ez] 分配读数只在 Deep 模式开启，light 模式下这里必须为 false，否则每帧两次 GC 查询会污染帧长。</summary>
        private static bool DeepAlloc => EzOsuGame.Diagnostics.EzFrameStallDiagnostics.Deep && EzOsuGame.Diagnostics.EzFrameStallDiagnostics.Enabled;

        /// <summary>
        /// [Ez] Number of subtree passes performed by the last <see cref="UpdateSubTree"/> call.
        /// Values above 1 mean the pass was a catch-up. Probe only; never read for gameplay logic.
        /// </summary>
        public static int EzLastUpdateIterations;

        private void updateClock()
        {
            if (waitingOnFrames.Value)
            {
                // if waiting on frames, run one update loop to determine if frames have arrived.
                state = PlaybackState.Valid;
            }
            else if (IsPaused.Value && !hasReplayAttached)
            {
                // time should not advance while paused, nor should anything run.
                state = PlaybackState.NotValid;
                return;
            }
            else
            {
                state = PlaybackState.Valid;
            }

            double proposedTime = referenceClock.CurrentTime;

            if (FrameStablePlayback)
                // if we require frame stability, the proposed time will be adjusted to move at most one known
                // frame interval in the current direction.
                applyFrameStability(ref proposedTime);

            if (hasReplayAttached)
            {
                bool valid = updateReplay(ref proposedTime);

                if (!valid)
                    state = PlaybackState.NotValid;
            }

            // TODO: replace IsDebugBuild with a framework flag which asserts we are in a test scene, interactively or otherwise.
            bool allowReferenceClockSeeks = hasReplayAttached || DebugUtils.IsNUnitRunning || DebugUtils.IsDebugBuild || !FrameStablePlayback;

            // This is a hotfix for ongoing bass issues we are trying to resolve (see https://www.un4seen.com/forum/?topic=20482.msg145474#msg145474)
            //
            // In testing this triggers *very* rarely even when set to super low values (10 ms). The cases we're worried about involve multi-second jumps.
            // A difference of more than 500 ms seems like a sane number we should never exceed.
            //
            // Double-checking against the parent clock ensures we don't accidentally freeze time when the game stutters due to a long running frame.
            if (!allowReferenceClockSeeks && Math.Abs(proposedTime - referenceClock.CurrentTime) > 500 && game?.Clock.ElapsedFrameTime <= 500)
            {
                if (invalidBassTimeLogCount < 10)
                {
                    invalidBassTimeLogCount++;
                    Logger.Log("Ignoring likely invalid time value provided by BASS during gameplay");
                    Logger.Log($"- provided: {referenceClock.CurrentTime:N2}");
                    Logger.Log($"- expected: {proposedTime:N2}");
                }

                state = PlaybackState.NotValid;
                return;
            }

            invalidBassTimeLogCount = 0;

            // if the proposed time is the same as the current time, assume that the clock will continue progressing in the same direction as previously.
            // this avoids spurious flips in direction from -1 to 1 during rewinds.
            if (state == PlaybackState.Valid && proposedTime != manualClock.CurrentTime)
                direction = proposedTime >= manualClock.CurrentTime ? 1 : -1;

            double timeBehind = Math.Abs(proposedTime - referenceClock.CurrentTime);

            isCatchingUp.Value = timeBehind > 200;
            waitingOnFrames.Value = hasReplayAttached && state == PlaybackState.NotValid;

            manualClock.CurrentTime = proposedTime;
            manualClock.Rate = Math.Abs(referenceClock.Rate) * direction;
            manualClock.IsRunning = referenceClock.IsRunning;

            // [Ez] Record the wall-clock moment the ManualClock is set, for sub-frame timing correction.
            EzOsuGame.Timing.EzSubFrameCorrection.RecordFscUpdate();

            // determine whether catch-up is required.
            if (state == PlaybackState.Valid && timeBehind > 0)
                state = PlaybackState.RequiresCatchUp;

            // The manual clock time has changed in the above code. The framed clock now needs to be updated
            // to ensure that the its time is valid for our children before input is processed
            framedClock.ProcessFrame();

            if (framedClock.ElapsedFrameTime != 0)
                IsRewinding = framedClock.ElapsedFrameTime < 0;
        }

        /// <summary>
        /// Attempt to advance replay playback for a given time.
        /// </summary>
        /// <param name="proposedTime">The time which is to be displayed.</param>
        /// <returns>Whether playback is still valid.</returns>
        private bool updateReplay(ref double proposedTime)
        {
            Debug.Assert(ReplayInputHandler != null);

            double? newTime;

            if (FrameStablePlayback)
            {
                // when stability is turned on, we shouldn't execute for time values the replay is unable to satisfy.
                newTime = ReplayInputHandler.SetFrameFromTime(proposedTime);
            }
            else
            {
                // when stability is disabled, we don't really care about accuracy.
                // looping over the replay will allow it to catch up and feed out the required values
                // for the current time.
                while ((newTime = ReplayInputHandler.SetFrameFromTime(proposedTime)) != proposedTime)
                {
                    if (newTime == null)
                    {
                        // special case for when the replay actually can't arrive at the required time.
                        // protects from potential endless loop.
                        break;
                    }
                }
            }

            if (newTime == null)
                return false;

            proposedTime = newTime.Value;
            return true;
        }

        /// <summary>
        /// Apply frame stability modifier to a time.
        /// </summary>
        /// <param name="proposedTime">The time which is to be displayed.</param>
        private void applyFrameStability(ref double proposedTime)
        {
            const double sixty_frame_time = 1000.0 / 60;

            if (firstConsumption)
            {
                // On the first update, frame-stability seeking would result in unexpected/unwanted behaviour.
                // Instead we perform an initial seek to the proposed time.

                // process frame (in addition to finally clause) to clear out ElapsedTime
                manualClock.CurrentTime = proposedTime;
                framedClock.ProcessFrame();

                firstConsumption = false;
                return;
            }

            if (manualClock.CurrentTime < GameplayStartTime)
                manualClock.CurrentTime = proposedTime = Math.Min(GameplayStartTime, proposedTime);
            else if (Math.Abs(manualClock.CurrentTime - proposedTime) > sixty_frame_time * 1.2f)
            {
                proposedTime = proposedTime > manualClock.CurrentTime
                    ? Math.Min(proposedTime, manualClock.CurrentTime + sixty_frame_time)
                    : Math.Max(proposedTime, manualClock.CurrentTime - sixty_frame_time);
            }
        }

        #region Delegation of IGameplayClock

        public IBindable<bool> IsPaused { get; } = new BindableBool();

        public bool IsRewinding { get; private set; }

        public double CurrentTime => framedClock.CurrentTime;

        public double Rate => framedClock.Rate;

        public bool IsRunning => framedClock.IsRunning;

        public void ProcessFrame() { }

        public double ElapsedFrameTime => framedClock.ElapsedFrameTime;

        public double FramesPerSecond => framedClock.FramesPerSecond;

        public double StartTime => ParentGameplayClock?.StartTime ?? 0;

        private readonly AudioAdjustments gameplayAdjustments = new AudioAdjustments();

        public IAdjustableAudioComponent AdjustmentsFromMods => ParentGameplayClock?.AdjustmentsFromMods ?? gameplayAdjustments;

        #endregion

        #region Delegation of IFrameStableClock

        IBindable<bool> IFrameStableClock.IsCatchingUp => isCatchingUp;
        IBindable<bool> IFrameStableClock.WaitingOnFrames => waitingOnFrames;

        #endregion

        private enum PlaybackState
        {
            /// <summary>
            /// Playback is not possible. Child hierarchy should not be processed.
            /// </summary>
            NotValid,

            /// <summary>
            /// Playback is running behind real-time. Catch-up will be attempted by processing more than once per
            /// game loop (limited to a sane maximum to avoid frame drops).
            /// </summary>
            RequiresCatchUp,

            /// <summary>
            /// In a valid state, progressing one child hierarchy loop per game loop.
            /// </summary>
            Valid
        }
    }
}
