// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Audio
{
    public partial class DuplicateVirtualTrack : CompositeDrawable
    {
        private const string log_prefix = "[LAsEz/DuplicateVirtualTrack]";

        // Enable diagnostic logging for loop timing investigation.
        private readonly bool diagnosticsEnabled = true;

        private void log(string message)
        {
            // Suppress informational logs by default. Only forward messages
            // that contain failure/error keywords to reduce verbosity in normal runs.
            if (!diagnosticsEnabled)
            {
                if (!message.Contains("failed", StringComparison.OrdinalIgnoreCase)
                    && !message.Contains("error", StringComparison.OrdinalIgnoreCase)
                    && !message.Contains("exception", StringComparison.OrdinalIgnoreCase))
                    return;
            }

            Logger.Log($"{log_prefix} {message}", LoggingTarget.Runtime);
        }

        private void debug(string message)
        {
            if (!diagnosticsEnabled) return;

            Logger.Log($"{log_prefix} [DEBUG] {message}", LoggingTarget.Runtime);
        }

        private bool startRequested;
        private bool started;
        private IWorkingBeatmap? pendingBeatmap;
        private bool waitingForClockLogged;

        private BindableDouble? beatmapTrackMuteAdjustment;
        private Track? mutedOriginalTrack;
        private double? desiredCandidateStartTime;
        private Track? activeCandidateTrack;
        private bool ownsCandidateTrack;
        private double? overrideDuration;
        private int? overrideLoopCount;
        private double? overrideLoopInterval;
        private bool overrideForceLooping;
        private int loopsRemaining;
        private double sliceStart;
        private double sliceEnd;
        private bool? prevCandidateLooping;
        private ScheduledDelegate? loopDelayDelegate;
        private ScheduledDelegate? loopCheckerDelegate;
        private double? lastLoopStartGameplayTime;
        private BindableDouble? candidateMuteAdjustment;
        private bool inLoopDelay;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        private bool? prevGameplayClockRunning;
        private Action<ValueChangedEvent<bool>>? pausedChangedHandler;
        private Action? seekHandler;

        [Resolved(canBeNull: true)]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        [Resolved(canBeNull: true)]
        private BeatmapManager? beatmapManager { get; set; }

        [Resolved(canBeNull: true)]
        private AudioManager? audioManager { get; set; }

        public void StartPreview(IWorkingBeatmap beatmap, OverrideSettings overrides)
        {
            pendingBeatmap = beatmap;
            startRequested = true;

            // 仅应用本类/管理器仍依赖的最小设置：起点与命中音效开关。
            desiredCandidateStartTime = overrides.StartTime;
            // 本类仅负责主音乐与 storyboard 背景音，不处理 note/hitsound 的触发。

            // gameplay 下不要把 MasterGameplayClockContainer 从真实 beatmap.Track "断开"。
            // 断开会导致：
            // 1) 变速 Mod（HT/DT/RateAdjust）对 gameplay 时钟不生效（TrackVirtual 不一定按 Tempo/Frequency 推进时间）。
            // 2) SubmittingPlayer 的播放校验会持续报 "System audio playback is not working"。
            // 这里改为：保留 beatmap.Track 作为时钟来源，但将其静音，避免听到整首歌。
            if (gameplayClock != null && beatmap.Track != null)
            {
                // 使用可撤销的音量调整而不是直接写入 Volume.Value，确保调整可以安全移除且不会覆盖其它调整。
                if (mutedOriginalTrack == null || mutedOriginalTrack != beatmap.Track)
                {
                    // 若之前对其它 track 应用过 mute adjustment，则先移除它。
                    try
                    {
                        if (beatmapTrackMuteAdjustment != null && mutedOriginalTrack != null)
                            mutedOriginalTrack.RemoveAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);
                    }
                    catch (Exception ex)
                    {
                        log($"error removing previous mute adjustment: {ex}");
                    }

                    beatmapTrackMuteAdjustment = new BindableDouble(0);
                    beatmap.Track.AddAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);
                    mutedOriginalTrack = beatmap.Track;

                    log(
                        $"StartPreview overrides: StartTime={overrides.StartTime} Duration={overrides.Duration} LoopCount={overrides.LoopCount} LoopInterval={overrides.LoopInterval} ForceLooping={overrides.ForceLooping}");
                }
            }

            // 不直接启动预览：当存在 gameplay 时钟时，延迟到 Drawable 生命周期（UpdateAfterChildren）并等待
            // gameplayClock.IsRunning 后再调用实际启动逻辑，以确保与外部时钟的时序一致。
            waitingForClockLogged = false;
            // 记录 desired candidate start time（已在上方设置），供 acquireIndependentTrack 在准备候选 track 时 seek 用。

            // 不再依赖父类的外部时钟行为；DuplicateVirtualTrack 自行准备候选 track 并控制起点。

            // 读取覆盖设置
            overrideDuration = overrides.Duration;
            overrideLoopCount = overrides.LoopCount;
            overrideLoopInterval = overrides.LoopInterval;
            overrideForceLooping = overrides.ForceLooping ?? false;

            log(
                $"StartPreview overrides: StartTime={overrides.StartTime} Duration={overrides.Duration} LoopCount={overrides.LoopCount} LoopInterval={overrides.LoopInterval} ForceLooping={overrides.ForceLooping}");

            // 计算初始循环计数与片段边界（若提供）
            sliceStart = desiredCandidateStartTime ?? 0;

            if (overrideDuration != null)
            {
                sliceEnd = sliceStart + overrideDuration.Value;
                loopsRemaining = overrideLoopCount ?? 1;
            }
            // 不再尝试修改 EzPreviewTrackManager.Enabled，由调用方/外层逻辑决定预览管理器的启用状态。
        }

        protected override void Dispose(bool isDisposing)
        {
            // 尝试移除之前添加的音量调整，确保不会在 Dispose 后仍保持静音。
            if (beatmapTrackMuteAdjustment != null && mutedOriginalTrack != null)
            {
                mutedOriginalTrack.RemoveAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);
            }

            beatmapTrackMuteAdjustment = null;
            mutedOriginalTrack = null;

            // 停止并释放候选轨（若为独立实例）
            if (activeCandidateTrack != null)
            {
                activeCandidateTrack.Stop();
                activeCandidateTrack = null;
                ownsCandidateTrack = false;
            }

            lastLoopStartGameplayTime = null;

            // Unbind paused handler if we've bound it.
            if (pausedChangedHandler != null && gameplayClockContainer != null)
                gameplayClockContainer.IsPaused.ValueChanged -= pausedChangedHandler;

            if (seekHandler != null && gameplayClockContainer != null)
                gameplayClockContainer.OnSeek -= seekHandler;

            base.Dispose(isDisposing);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // Always handle pause/resume for an active candidate track so that
            // playback is suspended together with the gameplay clock.
            if (activeCandidateTrack != null && gameplayClock != null)
            {
                bool running = gameplayClock.IsRunning;
                prevGameplayClockRunning ??= running;

                if (running != prevGameplayClockRunning)
                {
                    if (!running)
                    {
                        activeCandidateTrack.Stop();
                        loopCheckerDelegate?.Cancel();
                    }
                    else
                    {
                        activeCandidateTrack.Start();

                        if (overrideDuration != null && loopCheckerDelegate == null && activeCandidateTrack != null)
                        {
                            // Initialize or resume loop handling. prefer gameplay-clock driven scheduling
                            if (gameplayClock != null)
                            {
                                lastLoopStartGameplayTime ??= gameplayClock.CurrentTime;
                            }

                            // Delegate to shared helper which will choose gameplayClock-driven scheduling when available,
                            // or fallback to the previous polling-based checker when not.
                            ensureLoopCheckerRunning();
                        }
                    }

                    prevGameplayClockRunning = running;
                }
            }

                // Gameplay-clock driven loop check: perform loop transitions precisely when the gameplay clock crosses the expected boundary.
                try
                {
                    if (activeCandidateTrack != null && gameplayClock != null && overrideDuration != null && !inLoopDelay)
                    {
                        double sliceLength = sliceEnd - sliceStart;

                        if (sliceLength > 0)
                        {
                            lastLoopStartGameplayTime ??= gameplayClock.CurrentTime;

                            double expectedNext = lastLoopStartGameplayTime.Value + sliceLength;

                            // If we've reached or passed the expected loop end, trigger the loop.
                            if (gameplayClock.CurrentTime >= expectedNext)
                            {
                                // prevent double triggers from scheduled delegates
                                loopCheckerDelegate?.Cancel();
                                loopCheckerDelegate = null;

                                debug($"gameplay-driven trigger: gameplayNow={gameplayClock.CurrentTime} trackNow={activeCandidateTrack.CurrentTime} expectedNext={expectedNext} loopsRemaining={loopsRemaining}");

                                if (loopsRemaining <= 1)
                                {
                                    stopPreviewInternal("loops_finished");
                                }
                                else
                                {
                                    loopsRemaining = loopsRemaining == int.MaxValue ? int.MaxValue : loopsRemaining - 1;

                                    double interval = overrideLoopInterval ?? 0.0;

                                    if (interval > 0)
                                    {
                                        candidateMuteAdjustment ??= new BindableDouble(0);
                                        activeCandidateTrack.AddAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);
                                        inLoopDelay = true;

                                        loopDelayDelegate?.Cancel();
                                        loopDelayDelegate = Scheduler.AddDelayed(() =>
                                        {
                                            try
                                            {
                                                activeCandidateTrack?.Seek(sliceStart);
                                                if (candidateMuteAdjustment != null)
                                                    activeCandidateTrack?.RemoveAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);
                                                activeCandidateTrack?.Start();
                                                inLoopDelay = false;

                                                // advance baseline to next loop start
                                                lastLoopStartGameplayTime = expectedNext + interval;
                                                debug($"delayed restart: newBaseline={lastLoopStartGameplayTime}");
                                                ensureLoopCheckerRunning();
                                            }
                                            catch (Exception ex)
                                            {
                                                log($"delayed restart failed: {ex}");
                                            }
                                        }, (int)interval);
                                    }
                                    else
                                    {
                                        bool isUsingBeatmapTrack = pendingBeatmap?.Track != null && ReferenceEquals(activeCandidateTrack, pendingBeatmap.Track);
                                        if (!isUsingBeatmapTrack)
                                            activeCandidateTrack.Seek(sliceStart);

                                        // advance baseline to next loop start
                                        lastLoopStartGameplayTime = expectedNext;
                                        debug($"seamless restart: newBaseline={lastLoopStartGameplayTime}");
                                        ensureLoopCheckerRunning();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log($"gameplay-driven loop check failed: {ex}");
                }

            if (started || !startRequested || pendingBeatmap == null)
                return;

            // 当有 gameplay 时钟且第一次进入 running 状态时再启动切片播放，避免准备时间被抢占。
            if (gameplayClock != null && !gameplayClock.IsRunning)
            {
                if (!waitingForClockLogged)
                {
                    waitingForClockLogged = true;
                }

                return;
            }

            waitingForClockLogged = false;
            started = true;

            if (pendingBeatmap != null)
            {
                startCandidatePlayback(pendingBeatmap);
                startRequested = false;
                pendingBeatmap = null;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Bind to gameplay pause state changes for more reliable pause/resume handling.
            if (gameplayClockContainer != null)
            {
                pausedChangedHandler = e =>
                {
                    if (e.NewValue)
                    {
                        if (activeCandidateTrack != null)
                        {
                            activeCandidateTrack.Stop();

                            loopCheckerDelegate?.Cancel();
                            loopCheckerDelegate = null;
                        }
                    }
                    else
                    {
                        if (activeCandidateTrack != null)
                        {
                            // If we've progressed past the slice while paused, seek back to start to preserve looping behavior.
                            if (overrideDuration != null)
                            {
                                double now = activeCandidateTrack.CurrentTime;

                                if (now >= sliceEnd)
                                {
                                    // 避免对beatmap.Track进行Seek，因为那会影响主游戏音频
                                    bool isUsingBeatmapTrack = pendingBeatmap?.Track != null && ReferenceEquals(activeCandidateTrack, pendingBeatmap.Track);

                                    if (!isUsingBeatmapTrack)
                                    {
                                        activeCandidateTrack.Seek(sliceStart);
                                        log($"seeked candidate to sliceStart on resume ({sliceStart})");
                                    }
                                }
                            }

                            activeCandidateTrack.Start();
                            ensureLoopCheckerRunning();
                        }
                    }
                };
            }

            gameplayClockContainer?.IsPaused.BindValueChanged(pausedChangedHandler, true);

            // Bind to seek events so we can reposition independent candidate tracks
            // when gameplay time is jumped (skip intro / timeline seek).
            seekHandler = () =>
            {
                if (activeCandidateTrack == null) return;

                // If candidate is the underlying beatmap track, do not seek it here.
                bool isUsingBeatmapTrack = mutedOriginalTrack != null && ReferenceEquals(activeCandidateTrack, mutedOriginalTrack);

                if (!isUsingBeatmapTrack)
                {
                    double seekTarget = getInitialSeekTarget();
                    log($"OnSeek handler: seekTarget={seekTarget} activeHash={activeCandidateTrack?.GetHashCode()} isUsingBeatmapTrack={isUsingBeatmapTrack} currentBefore={activeCandidateTrack?.CurrentTime}");

                    // Cancel any pending loop logic to avoid it fighting our manual seek.
                    loopDelayDelegate?.Cancel();
                    loopDelayDelegate = null;

                    loopCheckerDelegate?.Cancel();
                    loopCheckerDelegate = null;

                    // If we muted the candidate during a loop delay, restore volume.
                    if (candidateMuteAdjustment != null)
                    {
                        activeCandidateTrack?.RemoveAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);

                        candidateMuteAdjustment = null;
                    }

                    activeCandidateTrack?.Stop();
                    activeCandidateTrack?.Seek(seekTarget);

                    if (gameplayClock == null || gameplayClock.IsRunning)
                    {
                        activeCandidateTrack?.Start();
                        ensureLoopCheckerRunning();
                    }

                    // Update gameplay-clock baseline so next loop timing remains accurate after manual seek.
                    try
                    {
                        if (gameplayClock != null && overrideDuration != null)
                        {
                            double sliceLength = sliceEnd - sliceStart;

                            if (sliceLength > 0)
                            {
                                // Prefer using the actual active track time if available (handles cases where
                                // the active track is the underlying beatmap.Track which we do not seek).
                                double currentTrackTime = activeCandidateTrack?.CurrentTime ?? seekTarget;
                                double offsetWithin = ((currentTrackTime - sliceStart) % sliceLength + sliceLength) % sliceLength;
                                lastLoopStartGameplayTime = gameplayClock.CurrentTime - offsetWithin;
                            }
                            else
                            {
                                lastLoopStartGameplayTime = gameplayClock.CurrentTime;
                            }
                        }
                    }
                    catch { }

                    log($"OnSeek handler: currentAfter={activeCandidateTrack?.CurrentTime}");

                    // Restart loop checker if needed.
                    ensureLoopCheckerRunning();
                }
            };

            if (gameplayClockContainer != null) gameplayClockContainer.OnSeek += seekHandler;
        }

        private void ensureLoopCheckerRunning()
        {
            try
            {
                if (overrideDuration == null || activeCandidateTrack == null)
                    return;
                // Prefer precise scheduling driven by the gameplay clock to avoid cumulative drift.
                loopCheckerDelegate?.Cancel();

                // If a gameplay clock is available, schedule a single delayed callback based on that clock.
                if (gameplayClock != null)
                {
                    // Ensure we have a baseline for the last loop start time.
                    lastLoopStartGameplayTime ??= gameplayClock.CurrentTime;

                    double sliceLength = sliceEnd - sliceStart;
                    double interval = overrideLoopInterval ?? 0.0;

                    // Time remaining until next loop in gameplay-clock space.
                    double elapsedSinceLastLoop = gameplayClock.CurrentTime - lastLoopStartGameplayTime.Value;
                    double timeUntilNextLoop = Math.Max(0, sliceLength + interval - elapsedSinceLastLoop);

                    // Schedule a one-shot delayed delegate relative to scheduler (ms).
                    debug($"scheduling gameplay-driven loop in {timeUntilNextLoop}ms (sliceLength={sliceLength} interval={interval}) lastLoopStartGameplayTime={lastLoopStartGameplayTime}");
                    loopCheckerDelegate = Scheduler.AddDelayed(() =>
                    {
                        try
                        {
                            if (activeCandidateTrack == null) return;

                            debug($"gameplay-loop-callback: gameplayNow={gameplayClock.CurrentTime} trackNow={activeCandidateTrack.CurrentTime} expectedNext={(lastLoopStartGameplayTime ?? gameplayClock.CurrentTime) + sliceLength}");

                            if (loopsRemaining <= 1)
                            {
                                stopPreviewInternal("loops_finished");
                                return;
                            }

                            loopsRemaining = loopsRemaining == int.MaxValue ? int.MaxValue : loopsRemaining - 1;

                            // Perform the loop restart using track.Seek and Start.
                            try
                            {
                                // If we are in an interval (interval>0), perform mute/delay behaviour to preserve Track progression.
                                if (interval > 0)
                                {
                                    candidateMuteAdjustment ??= new BindableDouble(0);
                                    activeCandidateTrack.AddAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);

                                    inLoopDelay = true;

                                    loopDelayDelegate?.Cancel();
                                    loopDelayDelegate = Scheduler.AddDelayed(() =>
                                    {
                                        activeCandidateTrack?.Seek(sliceStart);
                                        if (candidateMuteAdjustment != null)
                                            activeCandidateTrack?.RemoveAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);
                                        activeCandidateTrack?.Start();
                                        inLoopDelay = false;

                                        // Update baseline and reschedule next loop.
                                        lastLoopStartGameplayTime = (lastLoopStartGameplayTime ?? gameplayClock.CurrentTime) + sliceLength + interval;
                                        ensureLoopCheckerRunning();
                                    }, (int)interval);
                                }
                                else
                                {
                                    activeCandidateTrack.Seek(sliceStart);
                                    // update baseline for the next loop
                                    lastLoopStartGameplayTime = (lastLoopStartGameplayTime ?? gameplayClock.CurrentTime) + sliceLength;
                                    debug($"gameplay-loop-callback: seamless seek performed; newBaseline={lastLoopStartGameplayTime}");
                                    ensureLoopCheckerRunning();
                                }
                            }
                            catch (Exception ex)
                            {
                                log($"loop restart failed: {ex}");
                            }
                        }
                        catch (Exception ex)
                        {
                            log($"loopChecker error: {ex}");
                        }
                    }, timeUntilNextLoop);
                }
                else
                {
                    // Fallback: Cancel existing checker if any, then add a new repeating checker using mute-based interval handling.
                    loopCheckerDelegate = Scheduler.AddDelayed(() =>
                    {
                        try
                        {
                            if (activeCandidateTrack == null) return;

                            // If we're currently in a loop delay, ignore checks.
                            if (inLoopDelay)
                                return;

                            double now = activeCandidateTrack.CurrentTime;
                            const double epsilon = 2.0;

                            if (now + epsilon >= sliceEnd)
                            {
                                if (loopsRemaining <= 1)
                                {
                                    stopPreviewInternal("loops_finished");
                                    return;
                                }

                                loopsRemaining = loopsRemaining == int.MaxValue ? int.MaxValue : loopsRemaining - 1;

                                if (overrideLoopInterval > 0)
                                {
                                    // Enter delay: cancel checker, mark inLoopDelay and mute candidate instead of stopping it.
                                    loopCheckerDelegate?.Cancel();

                                    loopCheckerDelegate = null;

                                    candidateMuteAdjustment ??= new BindableDouble(0);

                                    activeCandidateTrack.AddAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);

                                    inLoopDelay = true;

                                    loopDelayDelegate?.Cancel();
                                    int delayMs = (int)Math.Max(0, overrideLoopInterval.Value);
                                    loopDelayDelegate = Scheduler.AddDelayed(() =>
                                    {
                                        activeCandidateTrack?.Seek(sliceStart);

                                        if (candidateMuteAdjustment != null) activeCandidateTrack?.RemoveAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);

                                        activeCandidateTrack?.Start();
                                        inLoopDelay = false;
                                        ensureLoopCheckerRunning();
                                    }, delayMs);
                                }
                                else
                                {
                                    try
                                    {
                                        activeCandidateTrack.Seek(sliceStart);
                                        log($"seamless seek to {sliceStart}");
                                    }
                                    catch (Exception ex) { log($"seamless seek failed: {ex}"); }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log($"loopChecker error: {ex}");
                        }
                    }, 30, true);
                }
            }
            catch (Exception ex)
            {
                log($"ensureLoopCheckerRunning failed: {ex}");
            }
        }

        private Track? createTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;

            if (gameplayClock == null)
                return beatmap.Track;

            return acquireIndependentTrack(beatmap, out ownsTrack) ?? beatmap.Track;
        }

        private Track? acquireIndependentTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;

            string audioFile = beatmap.BeatmapInfo.Metadata.AudioFile;

            if (string.IsNullOrEmpty(audioFile) || beatmap.BeatmapInfo.BeatmapSet is not BeatmapSetInfo beatmapSet)
            {
                return null;
            }

            string? rawFileStorePath = beatmapSet.GetPathForFile(audioFile);
            string? standardisedFileStorePath = rawFileStorePath;

            if (!string.IsNullOrEmpty(standardisedFileStorePath))
                standardisedFileStorePath = standardisedFileStorePath.ToStandardisedPath();

            bool hasBeatmapTrackStore = beatmapManager?.BeatmapTrackStore != null;

            // Candidates: prefer beatmap store, then global store (raw/standardised), then try direct audio filename fallbacks.
            Track?[] candidates = new[]
            {
                hasBeatmapTrackStore && !string.IsNullOrEmpty(rawFileStorePath) ? beatmapManager!.BeatmapTrackStore.Get(rawFileStorePath) : null,
                hasBeatmapTrackStore && !string.IsNullOrEmpty(standardisedFileStorePath) ? beatmapManager!.BeatmapTrackStore.Get(standardisedFileStorePath) : null,
                !string.IsNullOrEmpty(rawFileStorePath) ? audioManager?.Tracks.Get(rawFileStorePath) : null,
                !string.IsNullOrEmpty(standardisedFileStorePath) ? audioManager?.Tracks.Get(standardisedFileStorePath) : null,
                // Fallback: try loading by audio filename directly (some beatmaps only store filename)
                !string.IsNullOrEmpty(audioFile) ? audioManager?.Tracks.Get(audioFile) : null,
                !string.IsNullOrEmpty(audioFile) ? audioManager?.Tracks.Get(audioFile.Replace('\\', '/')) : null,
            };

            string[] candidateNames = new[] { "beatmapStoreRaw", "beatmapStoreStandardised", "globalStoreRaw", "globalStoreStandardised" };

            // Try candidates: ensure length populated first (lazy-load), prefer Length>0
            for (int i = 0; i < candidates.Length; i++)
            {
                var t = candidates[i];
                if (t == null) continue;

                try
                {
                    if (!t.IsLoaded || t.Length == 0)
                        t.Seek(t.CurrentTime);
                }
                catch (Exception ex)
                {
                    log($"ensure length failed for {candidateNames[i]}: {ex.Message}");
                }

                if (t.Length > 0)
                {
                    log($"selected {candidateNames[i]} (length={t.Length})");
                    if (gameplayClockContainer != null)
                        t.BindAdjustments(gameplayClockContainer.AdjustmentsFromMods);
                    if (gameplayClockContainer is MasterGameplayClockContainer master)
                        t.AddAdjustment(AdjustableProperty.Frequency, master.UserPlaybackRate);

                    // If this candidate is the same instance as the beatmap's track, do not claim ownership.
                    bool sameAsBeatmapTrack = beatmap.Track != null && ReferenceEquals(t, beatmap.Track);

                    // If it's an independent instance, stop and reset it to avoid carrying over previous playback state.
                    if (!sameAsBeatmapTrack)
                    {
                        try
                        {
                            t.Stop();
                            // Seek to desired candidate start time if provided, otherwise to 0.
                            double seekTarget = getInitialSeekTarget();
                            t.Seek(seekTarget);
                            log($"prepared independent candidate track (hash={t.GetHashCode()}) stopped and seeked to {seekTarget}.");
                        }
                        catch (Exception ex)
                        {
                            log($"failed to prepare candidate track: {ex}");
                        }
                    }

                    ownsTrack = !sameAsBeatmapTrack;
                    return t;
                }
            }

            // Fallback: pick the first non-null candidate (best-effort) and log
            for (int i = 0; i < candidates.Length; i++)
            {
                var t = candidates[i];
                if (t == null) continue;

                log($"fallback to {candidateNames[i]} (length={t.Length})");
                if (gameplayClockContainer != null)
                    t.BindAdjustments(gameplayClockContainer.AdjustmentsFromMods);
                if (gameplayClockContainer is MasterGameplayClockContainer master)
                    t.AddAdjustment(AdjustableProperty.Frequency, master.UserPlaybackRate);

                bool sameAsBeatmapTrack = beatmap.Track != null && ReferenceEquals(t, beatmap.Track);

                if (!sameAsBeatmapTrack)
                {
                    try
                    {
                        t.Stop();
                        double seekTarget = getInitialSeekTarget();
                        t.Seek(seekTarget);
                        log($"prepared fallback candidate track (hash={t.GetHashCode()}) stopped and seeked to {seekTarget}.");
                    }
                    catch (Exception ex)
                    {
                        log($"failed to prepare fallback candidate track: {ex}");
                    }
                }

                ownsTrack = !sameAsBeatmapTrack;
                return t;
            }

            log("no candidate found, using beatmap.Track");
            return null;
        }

        private void stopPreviewInternal(string reason)
        {
            // 停止并释放候选轨，优先停止 duplicate 以避免两轨同时播放。
            if (activeCandidateTrack != null)
            {
                try
                {
                    activeCandidateTrack.Stop();
                }
                catch (Exception ex)
                {
                    log($"failed to stop active candidate track on StopPreview: {ex}");
                }

                // If we applied a candidate mute adjustment during a loop delay, remove it now.
                if (candidateMuteAdjustment != null)
                {
                    activeCandidateTrack.RemoveAdjustment(AdjustableProperty.Volume, candidateMuteAdjustment);

                    candidateMuteAdjustment = null;
                }

                // Restore underlying track looping if we changed it earlier.
                if (prevCandidateLooping != null)
                {
                    activeCandidateTrack.Looping = prevCandidateLooping.Value;

                    log($"restored candidate track.Looping to {prevCandidateLooping.Value}");
                }

                if (ownsCandidateTrack)
                {
                    // do any necessary cleanup for owned track instances if required
                }

                activeCandidateTrack = null;
                ownsCandidateTrack = false;
            }

            // 恢复之前添加的音量调整（如果存在）。
            try
            {
                if (beatmapTrackMuteAdjustment != null && mutedOriginalTrack != null)
                {
                    log(
                        $"removing mute adjustment from original track (hash={mutedOriginalTrack.GetHashCode()}) on StopPreview. pre: vol={mutedOriginalTrack.Volume.Value:F3} aggr={mutedOriginalTrack.AggregateVolume.Value:F3}");

                    try
                    {
                        mutedOriginalTrack.RemoveAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);

                        log($"removed mute adjustment on StopPreview. post: vol={mutedOriginalTrack.Volume.Value:F3} aggr={mutedOriginalTrack.AggregateVolume.Value:F3}");
                    }
                    catch (Exception ex)
                    {
                        log($"failed to remove mute adjustment on StopPreview: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                log($"failed to remove mute adjustment on StopPreview: {ex}");
            }

            beatmapTrackMuteAdjustment = null;
            mutedOriginalTrack = null;

            // 取消任何挂起的延迟重启或检测器
            loopDelayDelegate?.Cancel();
            loopDelayDelegate = null;

            loopCheckerDelegate?.Cancel();
            loopCheckerDelegate = null;

            lastLoopStartGameplayTime = null;

            // (已在停止前尝试恢复底层 track.Looping 与移除候选静音调整)

            // 重置 DuplicateVirtualTrack 特有的状态
            startRequested = false;
            started = false;
            pendingBeatmap = null;
        }

        /// <summary>
        /// Public stop entry used by external callers (keeps compatibility with previous API).
        /// </summary>
        public void StopPreview(string? reason = null)
        {
            stopPreviewInternal(reason ?? "stopped");
        }

        private void startCandidatePlayback(IWorkingBeatmap beatmap)
        {
            bool owns;
            var t = createTrack(beatmap, out owns) ?? beatmap.Track;

            if (t == null)
            {
                return;
            }

            activeCandidateTrack = t;
            ownsCandidateTrack = owns;

            debug($"startCandidatePlayback: trackHash={t.GetHashCode()} owns={owns} isBeatmapTrack={ReferenceEquals(t, pendingBeatmap?.Track)} desiredStart={desiredCandidateStartTime}");

            {
                // Ensure seek to desired start time if provided
                if (desiredCandidateStartTime != null)
                {
                    // 避免对beatmap.Track进行Seek，因为那会影响主游戏音频
                    bool isUsingBeatmapTrack = pendingBeatmap?.Track != null && ReferenceEquals(t, pendingBeatmap.Track);

                    if (!isUsingBeatmapTrack)
                    {
                        double seekTarget = getInitialSeekTarget();
                        t.Seek(seekTarget);
                        debug($"startCandidatePlayback: seeked candidate to {seekTarget} (trackHash={t.GetHashCode()})");
                    }
                }

                // 如果需要将底层 Track 设置为循环（ForceLooping 且未指定 Duration），则切换 Looping
                if (overrideForceLooping && overrideDuration == null)
                {
                    prevCandidateLooping = t.Looping;
                    t.Looping = true;
                }

                t.Start();

                // 启动或确保短周期检测器（切片/拼接）运行。
                {
                    // Cancel any existing checker; ensureLoopCheckerRunning will recreate if needed.
                    loopCheckerDelegate?.Cancel();

                    loopCheckerDelegate = null;

                    // Let the shared helper create the loopChecker using consistent logic.
                    // Initialize gameplay-clock baseline for loop scheduling to avoid drift.
                    if (gameplayClock != null && overrideDuration != null)
                        lastLoopStartGameplayTime = gameplayClock.CurrentTime;

                    ensureLoopCheckerRunning();
                }
            }
        }

        private double getInitialSeekTarget()
        {
            // 计算音频应该 seek 到的位置
            // 考虑倒计时期间 gameplayClock.CurrentTime 是负数
            // 音频需要从 audioStart + gameplayClock.CurrentTime 开始，
            // 这样当 gameplayClock 推进到 0 时，音频正好在 audioStart
            double seekTarget = desiredCandidateStartTime ?? 0;

            if (gameplayClock != null)
            {
                // Use the gameplay clock container's StartTime as the baseline so that
                // seeking the gameplay timeline maps correctly to the candidate track's
                // absolute audio time.
                double baseline = gameplayClockContainer?.StartTime ?? 0;
                double delta = gameplayClock.CurrentTime - baseline;

                // If we have a configured slice length (duration-based looping), map the
                // delta into the slice via modulo so that arbitrary seeks land inside
                // the slice instead of placing the track past the slice end which would
                // immediately trigger the loop logic and jump to slice start.
                double segmentLength = (sliceEnd > sliceStart) ? (sliceEnd - sliceStart) : (overrideDuration ?? double.NaN);

                if (!double.IsNaN(segmentLength) && segmentLength > 0)
                {
                    double offsetWithin = ((delta % segmentLength) + segmentLength) % segmentLength;
                    seekTarget = sliceStart + offsetWithin;
                }
                else
                {
                    seekTarget += delta;
                }
            }

            return seekTarget;
        }
    }

    /// <summary>
    /// 预览覆盖参数集合，用于一次性配置预览的切片与循环行为。
    /// </summary>
    public class OverrideSettings
    {
        /// <summary>
        /// 预览起点（毫秒）。null 表示使用谱面元数据的 PreviewTime。
        /// </summary>
        public double? StartTime { get; init; }

        /// <summary>
        /// 长度（毫秒）。null 表示使用默认值。
        /// </summary>
        public double? Duration { get; init; }

        /// <summary>
        /// 循环次数。null 表示使用默认值（标准预览通常为 1，增强预览通常为无限）。
        /// </summary>
        public int? LoopCount { get; init; }

        /// <summary>
        /// 循环间隔（毫秒）。null 表示使用默认值。
        /// </summary>
        public double? LoopInterval { get; init; }

        /// <summary>
        /// 是否强制开启底层 Track.Looping。
        /// 注意：在启用外部驱动切片循环时，该项不会用于实现 Duration/LoopCount/LoopInterval 的严格约束。
        /// </summary>
        public bool? ForceLooping { get; init; }
    }
}
