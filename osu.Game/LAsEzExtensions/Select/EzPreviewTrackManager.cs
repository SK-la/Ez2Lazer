// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Framework.Timing;

namespace osu.Game.LAsEzExtensions.Select
{
    /// <summary>
    /// <para>一个增强的预览音轨管理器，支持在预览时播放note音效和故事板背景音。</para>
    /// <para>主要有两个用途：</para>
    /// 1. 在选歌界面实现最完整的游戏音轨预览。
    /// <para>2. 提供拓展支持，自定义预览时间、循环次数和间隔、关联游戏时钟、开关note音效等。</para>
    /// </summary>
    public partial class EzPreviewTrackManager : CompositeDrawable
    {
        /// <summary>
        /// 全局静态开关：当设置为 <see langword="false"/> 时，EzPreviewTrackManager 将拒绝启动新的预览。
        /// 由外部（例如 UI 的 `keySoundPreview`）控制。
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 当前是否处于“正在播放预览”的状态。
        /// 注意：该值同时要求内部状态认为正在播放，且底层 <see cref="Track"/> 实际处于运行状态。
        /// </summary>
        public bool IsPlaying => playback.IsPlaying && currentTrack?.IsRunning == true;

        private const int hitsound_threshold = 10;
        private const double preview_window_length = 20000; // 20s
        private const double scheduler_interval = 16; // ~60fps
        private const double trigger_tolerance = 15; // ms 容差

        // 外部时钟驱动（gameplay）模式下，过短的切片会导致高频 Seek/Restart，音频听感会明显“撕裂”。
        // 这里设置一个最小切片长度作为下限，避免 1ms 这类极端情况。
        private const double min_loop_length = 100; // ms

        // 在 gameplay 外部时钟驱动模式下，音频设备/解码缓冲会导致 Track.CurrentTime 与外部时钟存在微小漂移。
        // 若仍按 15ms 容差每帧 Seek，会产生明显的“撕裂/卡带”听感。
        // 因此对“音频轨道重同步”使用更宽容差，并加入冷却时间。
        private const double audio_resync_tolerance = 50; // ms
        private const double audio_resync_cooldown = 120; // ms

        private double lastAudioResyncClockTime;
        private const double max_dynamic_preview_length = 60000; // 动态扩展最长 ms
        private readonly SampleSchedulerState sampleScheduler = new SampleSchedulerState();
        private readonly PlaybackState playback = new PlaybackState();

        private Track? currentTrack;
        private IWorkingBeatmap? currentBeatmap;
        private ScheduledDelegate? updateDelegate;
        private Container audioContainer = null!;
        private ISampleStore sampleStore = null!;

        [Resolved]
        protected AudioManager AudioManager { get; private set; } = null!;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        public bool EnableHitSounds { get; set; } = true;

        /// <summary>
        /// 覆盖预览起点时间（毫秒）。
        /// 若为 null，则使用谱面元数据的预览时间（PreviewTime）。
        /// </summary>
        public double? OverridePreviewStartTime { get; set; }

        /// <summary>
        /// 覆盖预览段长度（毫秒）。
        /// 若为 null，则使用默认窗口长度，并在增强预览中可能动态延长以覆盖更多事件。
        /// </summary>
        public double? OverridePreviewDuration { get; set; }

        /// <summary>
        /// 覆盖底层 Track 的 Looping 行为。
        /// 注意：当启用外部驱动（存在 Duration/LoopCount/LoopInterval/ExternalClock 等）时，
        /// 预览会通过 <see cref="updateSamples"/> 的 Stop/Seek/Start 来严格实现切片与间隔，
        /// 此时 Track.Looping/RestartPoint 不再用于控制循环。
        /// </summary>
        public bool? OverrideLooping { get; set; }

        /// <summary>
        /// 覆盖循环次数。
        /// - 标准预览中默认 1 次。
        /// - 增强预览中默认无限（直到用户停止预览）。
        /// </summary>
        public int? OverrideLoopCount { get; set; }

        /// <summary>
        /// 覆盖循环间隔（毫秒）。
        /// 仅在使用外部驱动的切片循环模式下生效。
        /// </summary>
        public double? OverrideLoopInterval { get; set; }

        /// <summary>
        /// 外部时钟源。
        /// 设定后将使用该时钟的时间域来计算“逻辑播放时间”，用于与 gameplay 等场景同步（例如切片从 cutStart 开始）。
        /// </summary>
        public IClock? ExternalClock { get; set; }

        /// <summary>
        /// 当提供 <see cref="ExternalClock"/> 时，定义预览“开始”的参考时间（毫秒，属于外部时钟的时间域）。
        /// 主要用于把预览延后到某个外部时间点才开始生效（例如 gameplay 时间到达 cutStart 时再开始切片播放）。
        /// 若为 null，则会在 <see cref="StartPreview"/> 实际启动时捕获当下的外部时钟时间作为参考。
        /// </summary>
        public double? ExternalClockStartTime { get; set; }

        /// <summary>
        /// 从 <see cref="PreviewOverrideSettings"/> 批量应用覆盖参数。
        /// 传入 null 等同于 <see cref="ResetOverrides"/>。
        /// </summary>
        public void ApplyOverrides(PreviewOverrideSettings? settings)
        {
            if (settings == null)
            {
                ResetOverrides();
                return;
            }

            OverridePreviewStartTime = settings.PreviewStart;
            OverridePreviewDuration = settings.PreviewDuration;
            OverrideLoopCount = settings.LoopCount;
            OverrideLoopInterval = settings.LoopInterval;
            OverrideLooping = settings.ForceLooping;
            EnableHitSounds = settings.EnableHitSounds;
        }

        /// <summary>
        /// 重置所有覆盖参数到默认值。
        /// </summary>
        public void ResetOverrides()
        {
            OverridePreviewStartTime = null;
            OverridePreviewDuration = null;
            OverrideLoopCount = null;
            OverrideLoopInterval = null;
            OverrideLooping = null;
            EnableHitSounds = true;
        }

        /// <summary>
        /// 重置循环状态，用于在开始新预览时清除之前的循环进度。
        /// </summary>
        public void ResetLoopState()
        {
            playback.ResetExternalClockCapture();
            // 其他重置逻辑如果需要
        }

        private bool ownsCurrentTrack;

        #region Disposal

        protected override void Dispose(bool isDisposing)
        {
            StopPreview();
            base.Dispose(isDisposing);
        }

        #endregion

        /// <summary>
        /// 为指定谱面启动预览。
        /// 若命中音效数量低于阈值，会自动回退到“仅 BGM”的标准预览。
        /// </summary>
        /// <param name="beatmap">要预览的谱面</param>
        /// <param name="forceEnhanced">是否强制使用增强预览（忽略命中音效数量阈值）</param>
        public void StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            if (!Enabled)
                return;

            if (playback.IsPlaying && currentBeatmap == beatmap)
                return;

            StopPreview();

            currentBeatmap = beatmap;
            currentTrack = CreateTrack(beatmap, out ownsCurrentTrack);
            playback.ResetExternalClockCapture();

            bool enableEnhanced = forceEnhanced || fastCheckShouldUseEnhanced(beatmap, hitsound_threshold);

            if (!enableEnhanced)
            {
                startStandardPreview(beatmap);
                return;
            }

            startEnhancedPreview(beatmap);
        }

        public void StopPreview()
        {
            StopPreviewInternal("manual");
        }

        protected virtual void StopPreviewInternal(string reason)
        {
            // Logger.Log($"EzPreviewTrackManager: Stopping preview (reason={reason})");
            playback.IsPlaying = false;
            updateDelegate?.Cancel();
            updateDelegate = null;

            if (currentTrack != null)
            {
                currentTrack.Volume.Value = 1f;
                currentTrack.Stop();
                if (ownsCurrentTrack)
                    currentTrack.Dispose();
            }

            clearEnhancedElements();
            currentBeatmap = null;
            currentTrack = null;
            playback.ResetPlaybackProgress();

            // 清除所有 override 设置，确保不会影响后续使用
            OverrideLooping = null;
            OverrideLoopCount = null;
            OverrideLoopInterval = null;
            ExternalClock = null;
            ExternalClockStartTime = null;
            OverridePreviewStartTime = null;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            sampleStore = AudioManager.Samples;

            InternalChild = audioContainer = new Container
            {
                RelativeSizeAxes = Axes.Both
            };
        }

        // 快速判定：遍历命中对象直到达到阈值即返回 true，避免完整建立 HashSet 带来的额外分配
        private bool fastCheckShouldUseEnhanced(IWorkingBeatmap beatmap, int threshold)
        {
            try
            {
                var playable = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);
                var set = new HashSet<HitSampleInfo>();

                foreach (var obj in playable.HitObjects)
                {
                    collect(obj, set);
                    if (set.Count >= threshold)
                        return true;
                }

                return set.Count >= threshold;
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: fastCheckShouldUseEnhanced error: {ex}", LoggingTarget.Runtime);
                return false;
            }

            static void collect(HitObject ho, HashSet<HitSampleInfo> s)
            {
                foreach (var sm in ho.Samples) s.Add(sm);
                foreach (var n in ho.NestedHitObjects) collect(n, s);
            }
        }

        private void startStandardPreview(IWorkingBeatmap beatmap)
        {
            beatmap.PrepareTrackForPreview(true);
            playback.PreviewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;

            if (playback.PreviewStartTime < 0 || playback.PreviewStartTime > currentTrack?.Length)
                playback.PreviewStartTime = (currentTrack?.Length ?? 0) * 0.4;

            playback.PreviewEndTime = OverridePreviewDuration.HasValue
                ? playback.PreviewStartTime + Math.Max(0, OverridePreviewDuration.Value)
                : playback.PreviewStartTime + preview_window_length;

            double segmentLength = playback.PreviewEndTime - playback.PreviewStartTime;
            double minSegmentLength = ExternalClock != null ? min_loop_length : 1;
            playback.LoopSegmentLength = Math.Max(minSegmentLength, segmentLength);
            playback.LoopInterval = Math.Max(0, OverrideLoopInterval ?? 0);
            playback.EffectiveLoopCount = OverrideLoopCount ?? 1;
            // 当存在“切片/循环”相关 override 时，需要通过 updateSamples() 手动驱动 Track（Stop/Seek）。
            // 仅靠 Track.Looping/RestartPoint 无法严格约束 Duration/LoopCount/LoopInterval。
            playback.UseExternalLooping = ExternalClock != null
                                          || playback.LoopInterval > 0
                                          || OverrideLoopCount.HasValue
                                          || OverridePreviewDuration.HasValue
                                          || (OverrideLooping.HasValue && !OverrideLooping.Value);

            if (currentTrack != null)
            {
                currentTrack.Seek(playback.PreviewStartTime);
                currentTrack.Looping = !playback.UseExternalLooping && (OverrideLooping ?? true);
                currentTrack.RestartPoint = playback.PreviewStartTime;
            }

            currentTrack?.Start();
            playback.IsPlaying = true;

            if (playback.UseExternalLooping)
            {
                updateDelegate = Scheduler.AddDelayed(updateSamples, scheduler_interval, true);
                updateSamples();
            }
        }

        /// <summary>
        /// 启动增强预览（BGM + 命中音效 + 故事板音效）。
        /// </summary>
        private void startEnhancedPreview(IWorkingBeatmap beatmap)
        {
            double longestHitTime = 0; // 修复作用域：提前声明
            double longestStoryboardTime = 0;

            playback.LastReferenceTime = 0;

            void collectLongest(HitObject ho)
            {
                longestHitTime = Math.Max(longestHitTime, ho.StartTime);
                foreach (var n in ho.NestedHitObjects) collectLongest(n);
            }

            try
            {
                var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

                beatmap.PrepareTrackForPreview(true);
                playback.PreviewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;
                if (playback.PreviewStartTime < 0 || playback.PreviewStartTime > (currentTrack?.Length ?? 0))
                    playback.PreviewStartTime = (currentTrack?.Length ?? 0) * 0.4;

                foreach (var ho in playableBeatmap.HitObjects)
                    collectLongest(ho);

                if (beatmap.Storyboard?.Layers != null)
                {
                    foreach (var layer in beatmap.Storyboard.Layers)
                    {
                        foreach (var element in layer.Elements)
                        {
                            if (element is StoryboardSampleInfo s)
                                longestStoryboardTime = Math.Max(longestStoryboardTime, s.StartTime);
                        }
                    }
                }

                double longestEventTime = Math.Max(longestHitTime, longestStoryboardTime);
                double defaultEnd = playback.PreviewStartTime + preview_window_length;
                double dynamicEnd = defaultEnd;

                if (currentTrack != null)
                {
                    double segmentAfterStart = Math.Max(1, currentTrack.Length - playback.PreviewStartTime);
                    if (segmentAfterStart < preview_window_length * 0.6 && longestEventTime > defaultEnd)
                        dynamicEnd = Math.Min(playback.PreviewStartTime + max_dynamic_preview_length, longestEventTime);
                }

                double segmentLength = OverridePreviewDuration.HasValue
                    ? Math.Max(0, OverridePreviewDuration.Value)
                    : dynamicEnd - playback.PreviewStartTime;

                double minSegmentLength = ExternalClock != null ? min_loop_length : 1;
                playback.LoopSegmentLength = Math.Max(minSegmentLength, segmentLength);
                playback.EffectiveLoopCount = OverrideLoopCount ?? int.MaxValue;
                playback.LoopInterval = Math.Max(0, OverrideLoopInterval ?? 0);

                if (playback.EffectiveLoopCount == int.MaxValue)
                    playback.PreviewEndTime = double.MaxValue;
                else
                    playback.PreviewEndTime = playback.PreviewStartTime + playback.EffectiveLoopCount * (playback.LoopSegmentLength + playback.LoopInterval) - playback.LoopInterval;

                playback.LastTrackTime = playback.PreviewStartTime;
                playback.LegacyLoopCount = 0;
                playback.LegacyLogicalOffset = 0;
                playback.UseExternalLooping = ExternalClock != null
                                              || playback.LoopInterval > 0
                                              || OverrideLoopCount.HasValue
                                              || OverridePreviewDuration.HasValue
                                              || (OverrideLooping.HasValue && !OverrideLooping.Value);
                playback.ShortBgmOneShotMode = false;
                playback.ShortBgmMutedAfterFirstLoop = false;

                // 判定一次性短BGM模式：原始音轨长度 <2s 且 谱面事件覆盖长度 > 10s
                if (currentTrack != null && currentTrack.Length < 2000 && longestEventTime >= playback.PreviewStartTime + preview_window_length)
                    playback.ShortBgmOneShotMode = true;

                prepareHitSounds(playableBeatmap, playback.PreviewEndTime);
                prepareStoryboardSamples(beatmap.Storyboard, playback.PreviewEndTime);
                // preloadSamples();

                if (sampleScheduler.ScheduledHitSounds.Count == 0 && sampleScheduler.ScheduledStoryboardSamples.Count == 0)
                {
                    clearEnhancedElements();
                    startStandardPreview(beatmap);
                    return;
                }

                sampleScheduler.ResetIndices();

                currentTrack?.Seek(playback.PreviewStartTime);

                if (currentTrack != null)
                {
                    currentTrack.Looping = !playback.UseExternalLooping && (OverrideLooping ?? true);
                    currentTrack.RestartPoint = playback.PreviewStartTime;
                }

                currentTrack?.Start();
                playback.IsPlaying = true;

                updateDelegate = Scheduler.AddDelayed(updateSamples, scheduler_interval, true);
                updateSamples();
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: startEnhancedPreview error: {ex}", LoggingTarget.Runtime);
                clearEnhancedElements();
                startStandardPreview(beatmap);
            }
        }

        // 改为接受 endTime 参数
        private void prepareHitSounds(IBeatmap beatmap, double previewEndTime)
        {
            if (!EnableHitSounds)
            {
                sampleScheduler.ScheduledHitSounds.Clear();
                return;
            }

            sampleScheduler.ScheduledHitSounds.Clear();
            foreach (var ho in beatmap.HitObjects)
                schedule(ho, previewEndTime);
            sampleScheduler.ScheduledHitSounds.Sort((a, b) => a.Time.CompareTo(b.Time));

            void schedule(HitObject ho, double end)
            {
                if (ho.StartTime >= playback.PreviewStartTime && ho.StartTime <= end && ho.Samples.Any())
                {
                    sampleScheduler.ScheduledHitSounds.Add(new ScheduledHitSound
                    {
                        Time = ho.StartTime,
                        Samples = ho.Samples.ToArray(),
                        HasTriggered = false
                    });
                }

                foreach (var n in ho.NestedHitObjects) schedule(n, end);
            }
        }

        private void prepareStoryboardSamples(Storyboard? storyboard, double previewEndTime)
        {
            sampleScheduler.ScheduledStoryboardSamples.Clear();
            if (storyboard?.Layers == null) return;

            foreach (var layer in storyboard.Layers)
            {
                foreach (var element in layer.Elements)
                {
                    if (element is StoryboardSampleInfo s && s.StartTime >= playback.PreviewStartTime && s.StartTime <= previewEndTime)
                    {
                        sampleScheduler.ScheduledStoryboardSamples.Add(new ScheduledStoryboardSample
                        {
                            Time = s.StartTime,
                            Sample = s,
                            HasTriggered = false
                        });
                    }
                }
            }

            sampleScheduler.ScheduledStoryboardSamples.Sort((a, b) => a.Time.CompareTo(b.Time));
            // 移除成功日志
        }

        // 样本预加载：去重后调用一次 GetChannel() 以确保缓存 / 文件读取
        private void preloadSamples()
        {
            try
            {
                var uniqueHitInfos = new HashSet<string?>();

                foreach (var s in sampleScheduler.ScheduledHitSounds.SelectMany(h => h.Samples))
                {
                    foreach (var sample in fetchSamplesForInfo(s, true))
                    {
                        string? key = sample?.ToString();

                        if (key != null && uniqueHitInfos.Add(key))
                        {
                            var ch = sample?.GetChannel();

                            if (ch != null)
                            {
                                try
                                {
                                    ch.Stop();
                                }
                                finally
                                {
                                    // GetChannel() does not register the channel with the Sample unless Play() was invoked,
                                    // so ensure we dispose temporary channels created for preload to avoid relying on finalizers.
                                    if (!ch.IsDisposed && !ch.ManualFree)
                                        ch.Dispose();
                                }
                            }
                        }
                    }
                }

                var uniqueStoryboard = new HashSet<string>();

                foreach (var sb in sampleScheduler.ScheduledStoryboardSamples)
                {
                    // 通过统一的 fetchStoryboardSample 进行预热
                    var fetched = fetchStoryboardSample(sb.Sample, true);

                    if (fetched.sample != null && uniqueStoryboard.Add(fetched.chosenKey))
                    {
                        var ch = fetched.sample.GetChannel();

                        if (ch != null)
                        {
                            try
                            {
                                ch.Stop();
                            }
                            finally
                            {
                                if (!ch.IsDisposed && !ch.ManualFree)
                                    ch.Dispose();
                            }
                        }
                    }
                }

                // 移除成功日志
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: Preload error {ex.Message}", LoggingTarget.Runtime);
            }
        }

        // 调度函数基于索引推进
        private void updateSamples()
        {
            if (!playback.IsPlaying || currentTrack == null) return;

            if (!tryGetLogicalTime(out double logicalTime, out bool inBreak))
            {
                StopPreviewInternal("loop-end");
                return;
            }

            if (inBreak)
            {
                if (currentTrack.IsRunning)
                    currentTrack.Stop();
                if (Math.Abs(currentTrack.CurrentTime - logicalTime) > trigger_tolerance)
                    currentTrack.Seek(logicalTime);
                playback.LastTrackTime = logicalTime;
                sampleScheduler.ActiveChannels.RemoveAll(c => !c.Playing);
                return;
            }

            if (!currentTrack.IsRunning)
            {
                if (currentTrack.IsDisposed)
                {
                    StopPreview();
                    return;
                }

                currentTrack.Start();
            }

            double drift = Math.Abs(currentTrack.CurrentTime - logicalTime);

            // 在 gameplay 外部时钟模式下，不要为了微小漂移每帧 Seek。
            // 只在漂移明显且超过冷却时间时才重同步一次。
            if (ExternalClock != null)
            {
                if (drift > audio_resync_tolerance && Clock.CurrentTime - lastAudioResyncClockTime >= audio_resync_cooldown)
                {
                    currentTrack.Seek(logicalTime);
                    lastAudioResyncClockTime = Clock.CurrentTime;
                }
            }
            else
            {
                if (drift > trigger_tolerance)
                    currentTrack.Seek(logicalTime);
            }

            // 记录上一次逻辑时间，供外部时钟“暂停/不推进”判定使用。
            // 否则在某些情况下会被误判为暂停，从而导致重复 Seek 到固定时间点。
            playback.LastTrackTime = logicalTime;

            double logicalTimeForEvents = logicalTime;
            bool withinWindow = logicalTimeForEvents <= playback.PreviewEndTime + trigger_tolerance;

            sampleScheduler.NextHitSoundIndex = findNextValidIndex(sampleScheduler.ScheduledHitSounds, sampleScheduler.NextHitSoundIndex, logicalTimeForEvents - trigger_tolerance);

            while (withinWindow && sampleScheduler.NextHitSoundIndex < sampleScheduler.ScheduledHitSounds.Count)
            {
                var hs = sampleScheduler.ScheduledHitSounds[sampleScheduler.NextHitSoundIndex];

                if (hs.HasTriggered)
                {
                    sampleScheduler.NextHitSoundIndex++;
                    continue;
                }

                if (hs.Time > logicalTime + trigger_tolerance) break;

                if (Math.Abs(hs.Time - logicalTime) <= trigger_tolerance)
                {
                    triggerHitSound(hs.Samples);
                    hs.HasTriggered = true;
                    sampleScheduler.ScheduledHitSounds[sampleScheduler.NextHitSoundIndex] = hs;
                    sampleScheduler.NextHitSoundIndex++;
                }
                else if (hs.Time < logicalTime - trigger_tolerance)
                {
                    // 已错过（比如用户 Seek）
                    hs.HasTriggered = true;
                    sampleScheduler.ScheduledHitSounds[sampleScheduler.NextHitSoundIndex] = hs;
                    sampleScheduler.NextHitSoundIndex++;
                }
                else break;
            }

            // 同样优化 storyboard samples
            sampleScheduler.NextStoryboardSampleIndex = findNextValidIndex(sampleScheduler.ScheduledStoryboardSamples, sampleScheduler.NextStoryboardSampleIndex, logicalTimeForEvents - trigger_tolerance);

            while (withinWindow && sampleScheduler.NextStoryboardSampleIndex < sampleScheduler.ScheduledStoryboardSamples.Count)
            {
                var sb = sampleScheduler.ScheduledStoryboardSamples[sampleScheduler.NextStoryboardSampleIndex];

                if (sb.HasTriggered)
                {
                    sampleScheduler.NextStoryboardSampleIndex++;
                    continue;
                }

                if (sb.Time > logicalTime + trigger_tolerance) break;

                if (Math.Abs(sb.Time - logicalTime) <= trigger_tolerance)
                {
                    triggerStoryboardSample(sb.Sample);
                    sb.HasTriggered = true;
                    sampleScheduler.ScheduledStoryboardSamples[sampleScheduler.NextStoryboardSampleIndex] = sb;
                    sampleScheduler.NextStoryboardSampleIndex++;
                }
                else if (sb.Time < logicalTime - trigger_tolerance)
                {
                    sb.HasTriggered = true;
                    sampleScheduler.ScheduledStoryboardSamples[sampleScheduler.NextStoryboardSampleIndex] = sb;
                    sampleScheduler.NextStoryboardSampleIndex++;
                }
                else break;
            }

            playback.LastTrackTime = logicalTimeForEvents;
            sampleScheduler.ActiveChannels.RemoveAll(c => !c.Playing);
        }

        private void triggerHitSound(HitSampleInfo[] samples)
        {
            if (samples.Length == 0) return;

            try
            {
                foreach (var info in samples)
                {
                    bool playedAny = false;

                    foreach (var sample in fetchSamplesForInfo(info))
                    {
                        if (sample == null) continue;

                        var channelInner = sample.GetChannel();

                        // 同上：仅当命中对象样本显式给出音量 (>0) 时才应用；否则保持默认以跟随系统设置。
                        if (info.Volume > 0)
                        {
                            double volInner = Math.Clamp(info.Volume / 100.0, 0, 1);
                            channelInner.Volume.Value = (float)volInner;
                        }

                        channelInner.Play();
                        sampleScheduler.ActiveChannels.Add(channelInner);
                        playedAny = true;
                        break; // 只需播放命中链中的首个可用样本
                    }

                    if (!playedAny)
                        Logger.Log($"EzPreviewTrackManager: Miss hitsound {info.Bank}-{info.Name}", LoggingTarget.Runtime);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerHitSound error: {ex}", LoggingTarget.Runtime);
            }
        }

        // 多级检索：谱面 skin -> 全局 skinSource -> sampleStore (LookupNames) -> Gameplay/ 回退
        private IEnumerable<ISample?> fetchSamplesForInfo(HitSampleInfo info, bool preloadOnly = false)
        {
            // 1. 谱面皮肤
            var s = currentBeatmap?.Skin.GetSample(info);
            if (s != null) yield return s;

            // 2. 全局皮肤源
            var global = skinSource.GetSample(info);
            if (global != null) yield return global;

            // 3. LookupNames 走样本库
            foreach (string name in info.LookupNames)
            {
                // LookupNames 通常包含 Gameplay/ 前缀；若没有尝试补全
                ISample? storeSample = sampleStore.Get(name) ?? sampleStore.Get($"Gameplay/{name}");
                if (storeSample != null) yield return storeSample;
            }

            // 4. 兜底（兼容 legacy 组合）
            yield return sampleStore.Get($"Gameplay/{info.Bank}-{info.Name}");
        }

        private void triggerStoryboardSample(StoryboardSampleInfo sampleInfo)
        {
            try
            {
                var (sample, _, tried) = fetchStoryboardSample(sampleInfo);

                if (sample == null)
                {
                    // Logger.Log($"EzPreviewTrackManager: Miss storyboard sample {sampleInfo.Path} (tried: {string.Join("|", tried)})", LoggingTarget.Runtime);
                    return;
                }

                var channel = sample.GetChannel();

                // 仅在谱面 Storyboard 显式指定音量 (>0) 时应用相对缩放；否则保持默认，完全跟随系统全局音量/效果音量设置。
                if (sampleInfo.Volume > 0)
                {
                    double vol = Math.Clamp(sampleInfo.Volume / 100.0, 0, 1);
                    channel.Volume.Value = (float)vol;
                }

                channel.Play();
                sampleScheduler.ActiveChannels.Add(channel);
                // Logger.Log($"EzPreviewTrackManager: Played storyboard sample {sampleInfo.Path} <- {chosenKey}", LoggingTarget.Runtime);
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerStoryboardSample error: {ex}", LoggingTarget.Runtime);
            }
        }

        /// <summary>
        /// 统一 storyboard 样本获取逻辑。返回 (sample, 命中的key, 尝试列表)
        /// 顺序：缓存 -> beatmap skin -> 全局 skin -> sampleStore.LookupNames -> sampleStore 原Path 变体
        /// </summary>
        private (ISample? sample, string chosenKey, List<string> tried) fetchStoryboardSample(StoryboardSampleInfo info, bool preload = false)
        {
            var tried = new List<string>();
            string normalizedPath = info.Path.Replace('\\', '/');

            // 1. 缓存
            if (sampleScheduler.StoryboardSampleCache.TryGetValue(normalizedPath, out var cached) && cached != null)
                return (cached, normalizedPath + "(cache)", tried);

            ISample? selected = null;
            string chosenKey = string.Empty;

            void consider(ISample? s, string key)
            {
                if (s != null && selected == null)
                {
                    selected = s;
                    chosenKey = key;
                }

                tried.Add(key);
            }

            // 2. beatmap skin
            // StoryboardSampleInfo 实现 ISampleInfo，直接走 Skin.GetSample 会使用其 LookupNames
            var beatmapSkinSample = currentBeatmap?.Skin.GetSample(info);
            consider(beatmapSkinSample, "beatmapSkin:" + normalizedPath);

            // 3. 全局皮肤
            var globalSkinSample = skinSource.GetSample(info);
            consider(globalSkinSample, "globalSkin:" + normalizedPath);

            // 4. LookupNames in sampleStore
            foreach (string name in info.LookupNames)
            {
                string key = name.Replace('\\', '/');
                var s = sampleStore.Get(key);
                consider(s, "store:" + key);
                if (selected != null) break;
            }

            // 5. 额外尝试：去扩展名或补 wav/mp3 (有些 beatmap 在 LookupNames 第二项无扩展)
            if (selected == null)
            {
                string withoutExt = System.IO.Path.ChangeExtension(normalizedPath, null);

                foreach (string ext in new[] { ".wav", ".ogg", ".mp3" })
                {
                    var s = sampleStore.Get(withoutExt + ext);
                    consider(s, "store-extra:" + withoutExt + ext);
                    if (selected != null) break;
                }
            }

            // 6. 写缓存（即使 null 也缓存，避免重复磁盘尝试；预加载阶段写入，触发阶段复用）
            sampleScheduler.StoryboardSampleCache[normalizedPath] = selected;

            return (selected, chosenKey, tried);
        }

        private void clearEnhancedElements()
        {
            // 停止并释放所有仍在播放的样本通道，避免依赖最终化器来回收短期通道
            foreach (var channel in sampleScheduler.ActiveChannels)
            {
                try
                {
                    channel.Stop();
                }
                catch
                {
                }

                try
                {
                    if (!channel.IsDisposed && !channel.ManualFree)
                        channel.Dispose();
                }
                catch
                {
                }
            }

            sampleScheduler.Reset();
            playback.ShortBgmOneShotMode = false;
            playback.ShortBgmMutedAfterFirstLoop = false;
            // 避免在非更新线程直接操作 InternalChildren 导致 InvalidThreadForMutationException
            Schedule(() => audioContainer.Clear());
        }

        private struct ScheduledHitSound
        {
            public double Time;
            public HitSampleInfo[] Samples;
            public bool HasTriggered;
        }

        private struct ScheduledStoryboardSample
        {
            public double Time;
            public StoryboardSampleInfo Sample;
            public bool HasTriggered;
        }

        // 二分查找辅助方法：找到第一个 Time >= minTime 的索引
        private static int findNextValidIndex(List<ScheduledHitSound> list, int startIndex, double minTime)
        {
            int low = startIndex, high = list.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                double time = list[mid].Time;

                if (time < minTime)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return low;
        }

        private static int findNextValidIndex(List<ScheduledStoryboardSample> list, int startIndex, double minTime)
        {
            int low = startIndex, high = list.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                double time = list[mid].Time;

                if (time < minTime)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return low;
        }

        private bool tryGetLogicalTime(out double logicalTime, out bool inBreak)
        {
            logicalTime = playback.PreviewStartTime;
            inBreak = false;

            if (!playback.UseExternalLooping)
                return legacyTrackLogicalTime(out logicalTime, out inBreak);

            double referenceTime = ExternalClock?.CurrentTime ?? currentTrack?.CurrentTime ?? 0;

            // 如果 gameplay 时钟暂停（或“看似在跑但时间不推进”），音频也应保持暂停。
            // 否则 updateSamples() 会每帧 Seek 到固定时间点，而音频设备继续播放，
            // 听起来像“卡带”一样重复同一小段。
            if (ExternalClock != null)
            {
                // 有些时钟在暂停时仍可能 IsRunning=true，但 CurrentTime 不再推进。
                // 这里在已观察到上一次 referenceTime 后，把近似 0 的 delta 视为暂停。
                const double paused_delta_epsilon = 0.5; // ms

                if (!ExternalClock.IsRunning
                    || (playback.LastReferenceTime != 0 && Math.Abs(referenceTime - playback.LastReferenceTime) <= paused_delta_epsilon))
                {
                    inBreak = true;
                    logicalTime = playback.LastTrackTime == 0 ? playback.PreviewStartTime : playback.LastTrackTime;
                    playback.LastReferenceTime = referenceTime;
                    return true;
                }
            }

            if (!playback.ExternalClockStartCaptured)
            {
                playback.ExternalClockStartReference = ExternalClockStartTime ?? referenceTime;
                playback.ExternalClockStartCaptured = true;
            }

            double timeSinceStart = referenceTime - playback.ExternalClockStartReference;

            double segmentLen = playback.LoopSegmentLength + playback.LoopInterval;
            if (segmentLen <= 0)
                return false;

            if (timeSinceStart < 0)
            {
                inBreak = true; // 外部时钟尚未到达起点，保持暂停
                logicalTime = playback.PreviewStartTime;
                playback.LastReferenceTime = referenceTime;
                return true;
            }

            double segment = Math.Floor(timeSinceStart / segmentLen);

            if (segment < 0)
            {
                inBreak = true;
                logicalTime = playback.PreviewStartTime;
                playback.LastReferenceTime = referenceTime;
                return true;
            }

            if (segment >= playback.EffectiveLoopCount)
                return false;

            double offset = timeSinceStart - segment * segmentLen;

            if (offset < playback.LoopSegmentLength)
            {
                logicalTime = playback.PreviewStartTime + offset;
                playback.LastReferenceTime = referenceTime;
                return true;
            }

            inBreak = true;
            logicalTime = playback.PreviewEndTime;

            if (ExternalClock == null && playback.UseExternalLooping && inBreak && currentTrack != null && !currentTrack.IsRunning)
            {
                referenceTime += Clock.ElapsedFrameTime;
            }

            playback.LastReferenceTime = referenceTime;
            return true;
        }

        private bool legacyTrackLogicalTime(out double logicalTime, out bool inBreak)
        {
            double physicalTime = currentTrack?.CurrentTime ?? 0;

            // 回绕检测阈值200ms：避免因 Seek 导致的误判
            if (physicalTime + 200 < playback.LastTrackTime)
            {
                playback.LegacyLoopCount++;
                playback.LegacyLogicalOffset = playback.LegacyLoopCount * playback.LoopSegmentLength;

                if (playback.ShortBgmOneShotMode && !playback.ShortBgmMutedAfterFirstLoop && currentTrack != null)
                {
                    currentTrack.Volume.Value = 0f;
                    playback.ShortBgmMutedAfterFirstLoop = true;
                }
            }

            logicalTime = physicalTime + playback.LegacyLogicalOffset;
            inBreak = false;
            return true;
        }

        private sealed class PlaybackState
        {
            public bool IsPlaying;

            public double PreviewStartTime;
            public double PreviewEndTime;

            public double LastTrackTime;

            public double LoopSegmentLength;
            public double LoopInterval;
            public int EffectiveLoopCount;
            public bool UseExternalLooping;

            public int LegacyLoopCount;
            public double LegacyLogicalOffset;

            public bool ShortBgmOneShotMode;
            public bool ShortBgmMutedAfterFirstLoop;

            public double ExternalClockStartReference;
            public bool ExternalClockStartCaptured;
            public double LastReferenceTime;

            public void ResetExternalClockCapture()
            {
                ExternalClockStartCaptured = false;
                ExternalClockStartReference = 0;
            }

            public void ResetPlaybackProgress()
            {
                LastTrackTime = 0;
                LegacyLoopCount = 0;
                LegacyLogicalOffset = 0;
                LastReferenceTime = 0;
            }
        }

        private sealed class SampleSchedulerState
        {
            public readonly List<ScheduledHitSound> ScheduledHitSounds = new List<ScheduledHitSound>();
            public readonly List<ScheduledStoryboardSample> ScheduledStoryboardSamples = new List<ScheduledStoryboardSample>();
            public readonly Dictionary<string, ISample?> StoryboardSampleCache = new Dictionary<string, ISample?>();
            public readonly List<SampleChannel> ActiveChannels = new List<SampleChannel>();

            public int NextHitSoundIndex;
            public int NextStoryboardSampleIndex;

            public void ResetIndices()
            {
                NextHitSoundIndex = 0;
                NextStoryboardSampleIndex = 0;
            }

            public void Reset()
            {
                ActiveChannels.Clear();
                ScheduledHitSounds.Clear();
                ScheduledStoryboardSamples.Clear();
                StoryboardSampleCache.Clear();
                ResetIndices();
            }
        }

        protected virtual Track? CreateTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;
            return beatmap.Track;
        }
    }

    /// <summary>
    /// 预览覆盖参数集合，用于一次性配置 <see cref="EzPreviewTrackManager"/> 的切片与循环行为。
    /// </summary>
    public class PreviewOverrideSettings
    {
        /// <summary>
        /// 预览起点（毫秒）。null 表示使用谱面元数据的 PreviewTime。
        /// </summary>
        public double? PreviewStart { get; init; }

        /// <summary>
        /// 预览段长度（毫秒）。null 表示使用默认值。
        /// </summary>
        public double? PreviewDuration { get; init; }

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

        public bool EnableHitSounds { get; init; } = true;
    }
}
