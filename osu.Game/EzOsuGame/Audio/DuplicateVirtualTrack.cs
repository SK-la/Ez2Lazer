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
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Audio
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

            Logger.Log($"{log_prefix} {message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }

        private void debug(string message)
        {
            if (!diagnosticsEnabled) return;

            Logger.Log($"{log_prefix} [DEBUG] {message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
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
        private double sliceStart;
        private double sliceEnd;
        private bool? prevCandidateLooping;
        private double loopSyncBaseGameplayTime;
        private bool loopSyncBaseInitialised;
        private bool currentlyInBreakWindow;
        private int lastResolvedLoopIndex = -1;
        private double lastDriftCorrectionGameplayTime = double.NegativeInfinity;
        private readonly List<LoopScheduleEntry> loopSchedule = new List<LoopScheduleEntry>();

        private readonly struct LoopScheduleEntry
        {
            public readonly int Index;
            public readonly double PlayStart;
            public readonly double PlayEnd;
            public readonly double BreakEnd;
            public readonly double AudioStart;

            public LoopScheduleEntry(int index, double playStart, double playEnd, double breakEnd, double audioStart)
            {
                Index = index;
                PlayStart = playStart;
                PlayEnd = playEnd;
                BreakEnd = breakEnd;
                AudioStart = audioStart;
            }
        }

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
            // 避免同一局内重复调用 StartPreview 叠加状态，导致循环基线漂移。
            if (startRequested || started || activeCandidateTrack != null)
                stopPreviewInternal("restart_preview");

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
            }

            rebuildLoopSchedule();
            // 仅维护本类内部的切片播放状态，不处理外部组件的启停策略。
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

            releaseActiveCandidateTrack();

            // Unbind paused handler if we've bound it.
            if (pausedChangedHandler != null && gameplayClockContainer != null)
                gameplayClockContainer.IsPaused.ValueChanged -= pausedChangedHandler;

            if (seekHandler != null && gameplayClockContainer != null)
                gameplayClockContainer.OnSeek -= seekHandler;

            base.Dispose(isDisposing);
        }

        private void releaseActiveCandidateTrack()
        {
            if (activeCandidateTrack == null)
                return;

            try
            {
                activeCandidateTrack.Stop();
            }
            catch (Exception ex)
            {
                log($"failed to stop active candidate track during release: {ex}");
            }

            if (ownsCandidateTrack)
            {
                try
                {
                    activeCandidateTrack.Dispose();
                }
                catch (Exception ex)
                {
                    log($"failed to dispose owned candidate track: {ex}");
                }
            }

            activeCandidateTrack = null;
            ownsCandidateTrack = false;
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
                    }
                    else
                    {
                        synchroniseTrackToGameplayTimeline(forceSeek: true);
                    }

                    prevGameplayClockRunning = running;
                }
            }

            if (!started)
            {
                if (!startRequested || pendingBeatmap == null)
                    return;

                // 仅当 gameplay 时钟推进到 0 及之后才开始切片音频，
                // 保留谱面预备阶段的视觉缓冲，不让音频提前响。
                if (gameplayClock != null && (!gameplayClock.IsRunning || gameplayClock.CurrentTime < 0))
                {
                    if (!waitingForClockLogged)
                    {
                        waitingForClockLogged = true;
                    }

                    return;
                }

                waitingForClockLogged = false;
                started = true;

                startCandidatePlayback(pendingBeatmap);
                startRequested = false;
                pendingBeatmap = null;
            }

            synchroniseTrackToGameplayTimeline();
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
                        activeCandidateTrack?.Stop();
                    }
                    else
                    {
                        if (activeCandidateTrack != null)
                            synchroniseTrackToGameplayTimeline(forceSeek: true);
                    }
                };
            }

            gameplayClockContainer?.IsPaused.BindValueChanged(pausedChangedHandler, true);

            // Bind to seek events so we can reposition independent candidate tracks
            // when gameplay time is jumped (skip intro / timeline seek).
            seekHandler = () =>
            {
                if (activeCandidateTrack == null) return;
                if (gameplayClock == null)
                    return;

                loopSyncBaseGameplayTime = gameplayClock.CurrentTime;
                lastResolvedLoopIndex = -1;
                currentlyInBreakWindow = false;
                synchroniseTrackToGameplayTimeline(forceSeek: true);
            };

            if (gameplayClockContainer != null) gameplayClockContainer.OnSeek += seekHandler;
        }

        private Track? createTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;

            if (gameplayClock == null)
                return beatmap.Track;

            // gameplay 模式下必须使用独立轨，避免回退到 beatmap.Track 后对主时钟产生联动影响。
            return acquireIndependentTrack(beatmap, out ownsTrack);
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
                            double seekTarget = getInitialSeekTarget(useGameplayOffset: false);
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
                        double seekTarget = getInitialSeekTarget(useGameplayOffset: false);
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

                // Restore underlying track looping if we changed it earlier.
                if (prevCandidateLooping != null)
                {
                    activeCandidateTrack.Looping = prevCandidateLooping.Value;

                    log($"restored candidate track.Looping to {prevCandidateLooping.Value}");
                }

                releaseActiveCandidateTrack();
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

            loopSyncBaseGameplayTime = 0;
            loopSyncBaseInitialised = false;
            currentlyInBreakWindow = false;
            lastResolvedLoopIndex = -1;
            lastDriftCorrectionGameplayTime = double.NegativeInfinity;
            loopSchedule.Clear();

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
            var t = createTrack(beatmap, out owns);

            if (t == null)
            {
                log("no independent candidate track available in gameplay mode; skip duplicate playback to avoid clock coupling.");
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
                    bool isUsingBeatmapTrack = isUsingUnderlyingBeatmapTrack(t);

                    if (!isUsingBeatmapTrack)
                    {
                        double seekTarget = getInitialSeekTarget(useGameplayOffset: false);
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
                loopSyncBaseGameplayTime = gameplayClock?.CurrentTime ?? 0;
                loopSyncBaseInitialised = true;
                currentlyInBreakWindow = false;
                lastResolvedLoopIndex = -1;
                lastDriftCorrectionGameplayTime = double.NegativeInfinity;
            }
        }

        private double getInitialSeekTarget(bool useGameplayOffset = true)
        {
            // 计算音频应该 seek 到的位置
            // 考虑倒计时期间 gameplayClock.CurrentTime 是负数
            // 音频需要从 audioStart + gameplayClock.CurrentTime 开始，
            // 这样当 gameplayClock 推进到 0 时，音频正好在 audioStart
            double seekTarget = desiredCandidateStartTime ?? 0;

            if (useGameplayOffset && gameplayClock != null)
            {
                // Use the gameplay clock container's StartTime as the baseline so that
                // seeking the gameplay timeline maps correctly to the candidate track's
                // absolute audio time.
                double delta = gameplayClock.CurrentTime;

                // If we have a configured slice length (duration-based looping), map the
                // delta into the slice via modulo so that arbitrary seeks land inside
                // the slice instead of placing the track past the slice end which would
                // immediately trigger the loop logic and jump to slice start.
                double segmentLength = (sliceEnd > sliceStart) ? (sliceEnd - sliceStart) : (overrideDuration ?? double.NaN);

                if (delta < 0)
                    return seekTarget;

                bool shouldWrapBySegment = !double.IsNaN(segmentLength) && segmentLength > 0;

                if (shouldWrapBySegment)
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

        private bool isUsingUnderlyingBeatmapTrack(Track? track)
        {
            return track != null && mutedOriginalTrack != null && ReferenceEquals(track, mutedOriginalTrack);
        }

        private void rebuildLoopSchedule()
        {
            loopSchedule.Clear();

            if (overrideDuration == null)
                return;

            double duration = Math.Max(1, overrideDuration.Value);
            double interval = Math.Max(0, overrideLoopInterval ?? 0);
            int count = Math.Max(1, overrideLoopCount ?? 1);

            double cursor = 0;

            for (int i = 0; i < count; i++)
            {
                double playStart = cursor;
                double playEnd = playStart + duration;
                double breakEnd = playEnd + interval;

                loopSchedule.Add(new LoopScheduleEntry(i, playStart, playEnd, breakEnd, sliceStart));
                cursor = breakEnd;
            }
        }

        private void synchroniseTrackToGameplayTimeline(bool forceSeek = false)
        {
            if (activeCandidateTrack == null || overrideDuration == null || gameplayClock == null)
                return;

            if (loopSchedule.Count == 0)
                return;

            if (!loopSyncBaseInitialised)
            {
                loopSyncBaseGameplayTime = gameplayClock.CurrentTime;
                loopSyncBaseInitialised = true;
            }

            if (!gameplayClock.IsRunning || gameplayClock.CurrentTime < 0)
            {
                activeCandidateTrack.Stop();
                return;
            }

            double elapsed = Math.Max(0, gameplayClock.CurrentTime - loopSyncBaseGameplayTime);
            LoopScheduleEntry? resolvedEntry = null;
            bool inBreak = false;

            for (int i = 0; i < loopSchedule.Count; i++)
            {
                var entry = loopSchedule[i];

                if (elapsed < entry.PlayStart)
                {
                    resolvedEntry = entry;
                    break;
                }

                if (elapsed < entry.PlayEnd)
                {
                    resolvedEntry = entry;
                    break;
                }

                if (elapsed < entry.BreakEnd)
                {
                    resolvedEntry = entry;
                    inBreak = true;
                    break;
                }
            }

            if (resolvedEntry == null)
            {
                stopPreviewInternal("loops_finished");
                return;
            }

            var activeEntry = resolvedEntry.Value;

            if (inBreak)
            {
                if (!currentlyInBreakWindow)
                {
                    activeCandidateTrack.Stop();
                    currentlyInBreakWindow = true;
                }

                return;
            }

            double loopElapsed = Math.Clamp(elapsed - activeEntry.PlayStart, 0, activeEntry.PlayEnd - activeEntry.PlayStart);
            double desiredTrackTime = activeEntry.AudioStart + loopElapsed;
            bool loopChanged = activeEntry.Index != lastResolvedLoopIndex;
            bool leavingBreak = currentlyInBreakWindow;

            // 只在边界事件（新循环 / 休息段结束）执行硬同步，避免每帧 seek 导致音频撕裂。
            if (forceSeek || loopChanged || leavingBreak)
            {
                activeCandidateTrack.Seek(desiredTrackTime);
                activeCandidateTrack.Start();
            }
            else
            {
                // 常态播放仅做低频大偏差纠偏（且有冷却），防止长期漂移同时避免抖动。
                const double large_drift_threshold = 120.0;
                const double correction_cooldown_ms = 300.0;
                double drift = Math.Abs(activeCandidateTrack.CurrentTime - desiredTrackTime);

                if (drift > large_drift_threshold && gameplayClock.CurrentTime - lastDriftCorrectionGameplayTime >= correction_cooldown_ms)
                {
                    activeCandidateTrack.Seek(desiredTrackTime);
                    lastDriftCorrectionGameplayTime = gameplayClock.CurrentTime;
                }
            }

            currentlyInBreakWindow = false;
            lastResolvedLoopIndex = activeEntry.Index;
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
