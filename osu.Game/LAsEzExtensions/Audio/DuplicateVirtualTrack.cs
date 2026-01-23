// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Containers;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Audio
{
    public partial class DuplicateVirtualTrack : CompositeDrawable
    {
        private bool startRequested;
        private bool started;
        private IWorkingBeatmap? pendingBeatmap;
        private bool waitingForClockLogged;

        private BindableDouble? beatmapTrackMuteAdjustment;
        private Track? mutedOriginalTrack;
        private double? desiredCandidateStartTime;
        private bool? prevEzPreviewManagerEnabled;
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

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        private bool? prevGameplayClockRunning;
        private Action<ValueChangedEvent<bool>>? pausedChangedHandler;

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
                        Logger.Log($"DuplicateVirtualTrack: error removing previous mute adjustment: {ex}", LoggingTarget.Runtime);
                    }

                    beatmapTrackMuteAdjustment = new BindableDouble(0);
                    beatmap.Track.AddAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);
                    mutedOriginalTrack = beatmap.Track;

                    try
                    {
                        Logger.Log($"DuplicateVirtualTrack: muted original track (hash={mutedOriginalTrack.GetHashCode()}) preMuteVol={mutedOriginalTrack.Volume.Value:F3} agg={mutedOriginalTrack.AggregateVolume.Value:F3}", LoggingTarget.Runtime);
                    }
                    catch { }
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

            try
            {
                Logger.Log($"DuplicateVirtualTrack: StartPreview overrides: StartTime={overrides.StartTime} Duration={overrides.Duration} LoopCount={overrides.LoopCount} LoopInterval={overrides.LoopInterval} ForceLooping={overrides.ForceLooping}", LoggingTarget.Runtime);
            }
            catch { }

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

            // Unbind paused handler if we've bound it.
            try
            {
                if (pausedChangedHandler != null && gameplayClockContainer != null)
                    gameplayClockContainer.IsPaused.ValueChanged -= pausedChangedHandler;
            }
            catch { }

            base.Dispose(isDisposing);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // Always handle pause/resume for an active candidate track so that
            // playback is suspended together with the gameplay clock.
            try
            {
                if (activeCandidateTrack != null && gameplayClock != null)
                {
                    bool running = gameplayClock.IsRunning;
                    if (prevGameplayClockRunning == null)
                        prevGameplayClockRunning = running;

                    if (running != prevGameplayClockRunning)
                    {
                        if (!running)
                        {
                            try
                            {
                                activeCandidateTrack.Stop();
                                Logger.Log("DuplicateVirtualTrack: paused candidate due to gameplayClock.IsRunning=false", LoggingTarget.Runtime);
                            }
                            catch { }

                            // Pause loop checking while paused to avoid triggering seeks while stopped.
                            try { loopCheckerDelegate?.Cancel(); } catch { }
                        }
                        else
                        {
                            try
                            {
                                // Resume playback from current time.
                                activeCandidateTrack.Start();
                                Logger.Log("DuplicateVirtualTrack: resumed candidate due to gameplayClock.IsRunning=true", LoggingTarget.Runtime);
                            }
                            catch { }

                            // If we have a duration-based loop configured, ensure the loopChecker is running again.
                            try
                            {
                                if (overrideDuration != null && loopCheckerDelegate == null && activeCandidateTrack != null)
                                {
                                    loopCheckerDelegate = Scheduler.AddDelayed(() =>
                                    {
                                        try
                                        {
                                            if (activeCandidateTrack == null) return;
                                            double now = activeCandidateTrack.CurrentTime;
                                            const double epsilon = 2.0;
                                            if (now + epsilon >= sliceEnd)
                                            {
                                                Logger.Log($"DuplicateVirtualTrack: loopChecker triggered now={now} sliceEnd={sliceEnd} loopsRemaining={loopsRemaining}", LoggingTarget.Runtime);

                                                if (loopsRemaining <= 1)
                                                {
                                                    stopPreviewInternal("loops_finished");
                                                    return;
                                                }

                                                loopsRemaining = loopsRemaining == int.MaxValue ? int.MaxValue : loopsRemaining - 1;

                                                if (overrideLoopInterval != null && overrideLoopInterval > 0)
                                                {
                                                    try { activeCandidateTrack.Stop(); } catch { }

                                                    loopDelayDelegate?.Cancel();
                                                    var delayMs = (int)Math.Max(0, overrideLoopInterval.Value);
                                                    loopDelayDelegate = Scheduler.AddDelayed(() =>
                                                    {
                                                        try { activeCandidateTrack?.Seek(sliceStart); activeCandidateTrack?.Start(); Logger.Log($"DuplicateVirtualTrack: restarted candidate after interval", LoggingTarget.Runtime); }
                                                        catch (Exception ex) { Logger.Log($"DuplicateVirtualTrack: delayed restart failed: {ex}", LoggingTarget.Runtime); }
                                                    }, delayMs);
                                                }
                                                else
                                                {
                                                    try { activeCandidateTrack.Seek(sliceStart); Logger.Log($"DuplicateVirtualTrack: seamless seek to {sliceStart}", LoggingTarget.Runtime); }
                                                    catch (Exception ex) { Logger.Log($"DuplicateVirtualTrack: seamless seek failed: {ex}", LoggingTarget.Runtime); }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Log($"DuplicateVirtualTrack: loopChecker error: {ex}", LoggingTarget.Runtime);
                                        }
                                    }, 30, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"DuplicateVirtualTrack: failed to restart loopChecker on resume: {ex}", LoggingTarget.Runtime);
                            }
                        }

                        prevGameplayClockRunning = running;
                    }
                }
            }
            catch { }

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

            try
            {
                if (pendingBeatmap != null)
                {
                    startCandidatePlayback(pendingBeatmap);
                    startRequested = false;
                    pendingBeatmap = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"DuplicateVirtualTrack: failed to start candidate playback: {ex}", LoggingTarget.Runtime);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Bind to gameplay pause state changes for more reliable pause/resume handling.
            try
            {
                if (gameplayClockContainer != null)
                {
                    pausedChangedHandler = e =>
                    {
                        try
                        {
                            if (e.NewValue)
                            {
                                if (activeCandidateTrack != null)
                                {
                                    try { activeCandidateTrack.Stop(); Logger.Log("DuplicateVirtualTrack: paused candidate via IsPaused=true", LoggingTarget.Runtime); } catch { }
                                    try { loopCheckerDelegate?.Cancel(); loopCheckerDelegate = null; } catch { }
                                }
                            }
                            else
                            {
                                if (activeCandidateTrack != null)
                                {
                                    try
                                    {
                                        // If we've progressed past the slice while paused, seek back to start to preserve looping behavior.
                                        if (overrideDuration != null)
                                        {
                                            try
                                            {
                                                double now = activeCandidateTrack.CurrentTime;
                                                if (now >= sliceEnd)
                                                {
                                                    activeCandidateTrack.Seek(sliceStart);
                                                    Logger.Log($"DuplicateVirtualTrack: seeked candidate to sliceStart on resume ({sliceStart})", LoggingTarget.Runtime);
                                                }
                                            }
                                            catch { }
                                        }

                                        activeCandidateTrack.Start();
                                        Logger.Log("DuplicateVirtualTrack: resumed candidate via IsPaused=false", LoggingTarget.Runtime);
                                    }
                                    catch { }

                                    // Restart loopChecker if needed.
                                    try { ensureLoopCheckerRunning(); } catch { }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"DuplicateVirtualTrack: error handling IsPaused change: {ex}", LoggingTarget.Runtime);
                        }
                    };

                    gameplayClockContainer.IsPaused.BindValueChanged(pausedChangedHandler, true);
                }
            }
            catch { }
        }

        private void ensureLoopCheckerRunning()
        {
            try
            {
                if (overrideDuration == null || activeCandidateTrack == null)
                    return;

                // Cancel existing checker if any, then add a new repeating checker.
                loopCheckerDelegate?.Cancel();
                loopCheckerDelegate = Scheduler.AddDelayed(() =>
                {
                    try
                    {
                        if (activeCandidateTrack == null) return;
                        double now = activeCandidateTrack.CurrentTime;
                        const double epsilon = 2.0;
                        if (now + epsilon >= sliceEnd)
                        {
                            Logger.Log($"DuplicateVirtualTrack: loopChecker triggered now={now} sliceEnd={sliceEnd} loopsRemaining={loopsRemaining}", LoggingTarget.Runtime);

                            if (loopsRemaining <= 1)
                            {
                                stopPreviewInternal("loops_finished");
                                return;
                            }

                            loopsRemaining = loopsRemaining == int.MaxValue ? int.MaxValue : loopsRemaining - 1;

                            if (overrideLoopInterval != null && overrideLoopInterval > 0)
                            {
                                try { activeCandidateTrack.Stop(); } catch { }

                                loopDelayDelegate?.Cancel();
                                var delayMs = (int)Math.Max(0, overrideLoopInterval.Value);
                                loopDelayDelegate = Scheduler.AddDelayed(() =>
                                {
                                    try { activeCandidateTrack?.Seek(sliceStart); activeCandidateTrack?.Start(); Logger.Log($"DuplicateVirtualTrack: restarted candidate after interval", LoggingTarget.Runtime); }
                                    catch (Exception ex) { Logger.Log($"DuplicateVirtualTrack: delayed restart failed: {ex}", LoggingTarget.Runtime); }
                                }, delayMs);
                            }
                            else
                            {
                                try { activeCandidateTrack.Seek(sliceStart); Logger.Log($"DuplicateVirtualTrack: seamless seek to {sliceStart}", LoggingTarget.Runtime); }
                                catch (Exception ex) { Logger.Log($"DuplicateVirtualTrack: seamless seek failed: {ex}", LoggingTarget.Runtime); }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"DuplicateVirtualTrack: loopChecker error: {ex}", LoggingTarget.Runtime);
                    }
                }, 30, true);
            }
            catch (Exception ex)
            {
                Logger.Log($"DuplicateVirtualTrack: ensureLoopCheckerRunning failed: {ex}", LoggingTarget.Runtime);
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
                Logger.Log("DuplicateVirtualTrack: no audio metadata or beatmap set, falling back to original track", LoggingTarget.Runtime);
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
                    Logger.Log($"DuplicateVirtualTrack: ensure length failed for {candidateNames[i]}: {ex.Message}", LoggingTarget.Runtime);
                }

                if (t.Length > 0)
                {
                    Logger.Log($"DuplicateVirtualTrack: selected {candidateNames[i]} (length={t.Length})", LoggingTarget.Runtime);
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
                            double seekTarget = desiredCandidateStartTime ?? 0;
                            t.Seek(seekTarget);
                            Logger.Log($"DuplicateVirtualTrack: prepared independent candidate track (hash={t.GetHashCode()}) stopped and seeked to {seekTarget}.", LoggingTarget.Runtime);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"DuplicateVirtualTrack: failed to prepare candidate track: {ex}", LoggingTarget.Runtime);
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

                Logger.Log($"DuplicateVirtualTrack: fallback to {candidateNames[i]} (length={t.Length})", LoggingTarget.Runtime);
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
                        double seekTarget = desiredCandidateStartTime ?? 0;
                        t.Seek(seekTarget);
                        Logger.Log($"DuplicateVirtualTrack: prepared fallback candidate track (hash={t.GetHashCode()}) stopped and seeked to {seekTarget}.", LoggingTarget.Runtime);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"DuplicateVirtualTrack: failed to prepare fallback candidate track: {ex}", LoggingTarget.Runtime);
                    }
                }

                ownsTrack = !sameAsBeatmapTrack;
                return t;
            }

            Logger.Log("DuplicateVirtualTrack: no candidate found, using beatmap.Track", LoggingTarget.Runtime);
            return null;
        }

        private void stopPreviewInternal(string reason)
        {
            // 停止并释放候选轨，优先停止 duplicate 以避免两轨同时播放。
            try
            {
                if (activeCandidateTrack != null)
                {
                    try
                    {
                        activeCandidateTrack.Stop();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"DuplicateVirtualTrack: failed to stop active candidate track on StopPreview: {ex}", LoggingTarget.Runtime);
                    }

                    if (ownsCandidateTrack)
                    {
                        // do any necessary cleanup for owned track instances if required
                    }

                    activeCandidateTrack = null;
                    ownsCandidateTrack = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"DuplicateVirtualTrack: error while stopping candidate track: {ex}", LoggingTarget.Runtime);
            }

            // 恢复之前添加的音量调整（如果存在）。
            try
            {
                if (beatmapTrackMuteAdjustment != null && mutedOriginalTrack != null)
                {
                    try
                    {
                        Logger.Log($"DuplicateVirtualTrack: removing mute adjustment from original track (hash={mutedOriginalTrack.GetHashCode()}) on StopPreview. pre: vol={mutedOriginalTrack.Volume.Value:F3} aggr={mutedOriginalTrack.AggregateVolume.Value:F3}", LoggingTarget.Runtime);
                    }
                    catch { }

                    try
                    {
                        mutedOriginalTrack.RemoveAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);

                        try
                        {
                            Logger.Log($"DuplicateVirtualTrack: removed mute adjustment on StopPreview. post: vol={mutedOriginalTrack.Volume.Value:F3} aggr={mutedOriginalTrack.AggregateVolume.Value:F3}", LoggingTarget.Runtime);
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"DuplicateVirtualTrack: failed to remove mute adjustment on StopPreview: {ex}", LoggingTarget.Runtime);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"DuplicateVirtualTrack: failed to remove mute adjustment on StopPreview: {ex}", LoggingTarget.Runtime);
            }

            beatmapTrackMuteAdjustment = null;
            mutedOriginalTrack = null;

            // 取消任何挂起的延迟重启或检测器
            try
            {
                loopDelayDelegate?.Cancel();
                loopDelayDelegate = null;
            }
            catch { }

            try
            {
                loopCheckerDelegate?.Cancel();
                loopCheckerDelegate = null;
            }
            catch { }

            // 恢复之前修改的底层 track Looping（若我们在 Start 时修改过）
            try
            {
                if (activeCandidateTrack != null && prevCandidateLooping != null)
                {
                    try
                    {
                        activeCandidateTrack.Looping = prevCandidateLooping.Value;
                        Logger.Log($"DuplicateVirtualTrack: restored candidate track.Looping to {prevCandidateLooping.Value}", LoggingTarget.Runtime);
                    }
                    catch { }
                }
            }
            catch { }

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
            try
            {
                bool owns;
                var t = createTrack(beatmap, out owns) ?? beatmap.Track;

                if (t == null)
                {
                    Logger.Log("DuplicateVirtualTrack: no track available to play", LoggingTarget.Runtime);
                    return;
                }

                activeCandidateTrack = t;
                ownsCandidateTrack = owns;

                try
                {
                    // Ensure seek to desired start time if provided
                    if (desiredCandidateStartTime != null)
                        t.Seek(desiredCandidateStartTime.Value);

                    // 如果需要将底层 Track 设置为循环（ForceLooping 且未指定 Duration），则切换 Looping
                    if (overrideForceLooping && overrideDuration == null)
                    {
                        try
                        {
                            prevCandidateLooping = t.Looping;
                            t.Looping = true;
                            Logger.Log($"DuplicateVirtualTrack: enabled underlying track.Looping for candidate (hash={t.GetHashCode()})", LoggingTarget.Runtime);
                        }
                        catch { }
                    }

                    t.Start();
                    Logger.Log($"DuplicateVirtualTrack: started candidate track (hash={t.GetHashCode()}) at {t.CurrentTime}", LoggingTarget.Runtime);

                    // 启动短周期检测器以可靠处理切片/拼接（避免仅依赖 Drawable 的 Update 时序）
                    try
                    {
                        loopCheckerDelegate?.Cancel();
                        loopCheckerDelegate = null;

                        if (overrideDuration != null && !double.IsNaN(sliceEnd))
                        {
                            loopCheckerDelegate = Scheduler.AddDelayed(() =>
                            {
                                try
                                {
                                    if (activeCandidateTrack == null) return;
                                    double now = activeCandidateTrack.CurrentTime;
                                    const double epsilon = 2.0;
                                    if (now + epsilon >= sliceEnd)
                                    {
                                        Logger.Log($"DuplicateVirtualTrack: loopChecker triggered now={now} sliceEnd={sliceEnd} loopsRemaining={loopsRemaining}", LoggingTarget.Runtime);

                                        if (loopsRemaining <= 1)
                                        {
                                            stopPreviewInternal("loops_finished");
                                            return;
                                        }

                                        loopsRemaining = loopsRemaining == int.MaxValue ? int.MaxValue : loopsRemaining - 1;

                                        if (overrideLoopInterval != null && overrideLoopInterval > 0)
                                        {
                                            try { activeCandidateTrack.Stop(); } catch { }

                                            loopDelayDelegate?.Cancel();
                                            var delayMs = (int)Math.Max(0, overrideLoopInterval.Value);
                                            loopDelayDelegate = Scheduler.AddDelayed(() =>
                                            {
                                                try { activeCandidateTrack?.Seek(sliceStart); activeCandidateTrack?.Start(); Logger.Log($"DuplicateVirtualTrack: restarted candidate after interval", LoggingTarget.Runtime); }
                                                catch (Exception ex) { Logger.Log($"DuplicateVirtualTrack: delayed restart failed: {ex}", LoggingTarget.Runtime); }
                                            }, delayMs);
                                        }
                                        else
                                        {
                                            try { activeCandidateTrack.Seek(sliceStart); Logger.Log($"DuplicateVirtualTrack: seamless seek to {sliceStart}", LoggingTarget.Runtime); }
                                            catch (Exception ex) { Logger.Log($"DuplicateVirtualTrack: seamless seek failed: {ex}", LoggingTarget.Runtime); }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log($"DuplicateVirtualTrack: loopChecker error: {ex}", LoggingTarget.Runtime);
                                }
                            }, 30, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"DuplicateVirtualTrack: failed to start loopChecker: {ex}", LoggingTarget.Runtime);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"DuplicateVirtualTrack: failed to start candidate track: {ex}", LoggingTarget.Runtime);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"DuplicateVirtualTrack: StartCandidatePlayback failed: {ex}", LoggingTarget.Runtime);
            }
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
