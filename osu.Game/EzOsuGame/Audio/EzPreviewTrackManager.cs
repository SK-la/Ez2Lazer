// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.EzOsuGame.Audio
{
    /// <summary>
    /// <para>一个增强的预览音轨管理器，支持在预览时播放note音效和故事板背景音。</para>
    /// <para>主要有两个用途：</para>
    /// 1. 在选歌界面实现最完整的游戏音轨预览。
    /// <para>2. 提供拓展支持，自定义预览时间、循环次数和间隔、关联游戏时钟、开关note音效等。</para>
    /// </summary>
    public partial class EzPreviewTrackManager : CompositeDrawable
    {
        // 单例/实例都可用，但我们使用实例级 Bindable 以便在 `SongSelect` 中直接 BindTo。
        public BindableBool EnabledBindable { get; } = new BindableBool();

        // 预览时，非重复音效必须大于此值才会激活hitsound预览，否则退回外部预览。
        private const int hitsound_threshold = 10;
        private const double scheduler_interval = 16; // ~60fps
        private const double trigger_tolerance = 5; // ms 容差
        private const int max_cached_beatmaps = 3; // LRU 缓存：最多保留最近 3 首歌曲的样本

        private readonly SampleSchedulerState sampleScheduler = new SampleSchedulerState();
        private readonly PlaybackState playback = new PlaybackState();

        private Track? currentTrack;
        private IWorkingBeatmap? currentBeatmap;
        private ScheduledDelegate? updateDelegate;
        private Container audioContainer = null!;
        private ISampleStore? fallbackSampleStore;
        private ISampleStore? previewSampleStore;
        private IBeatmapSetInfo? lastPreviewSet;

        // LRU 缓存：按 BeatmapSetID 缓存样本数据
        private readonly LinkedList<string> beatmapAccessOrder = new LinkedList<string>();
        private readonly Dictionary<string, BeatmapSampleCache> sampleCache = new Dictionary<string, BeatmapSampleCache>();

        [Resolved]
        protected AudioManager AudioManager { get; private set; } = null!;

        [Resolved(CanBeNull = true)]
        private SkinManager? skinManager { get; set; }

        [Resolved(CanBeNull = true)]
        private MusicController? musicController { get; set; }

        /// <summary>
        /// 覆盖预览起点时间（毫秒）。
        /// 若为 null，则使用谱面元数据的预览时间（PreviewTime）。
        /// </summary>
        public double? OverridePreviewStartTime { get; set; }

        private bool ownsCurrentTrack;
        private bool previewMainAudioAvailable;
        private bool previewHitSoundsEnabled;
        private bool previewStoryboardEnabled;

        #region Disposal

        protected override void Dispose(bool isDisposing)
        {
            StopPreview();

            if (isDisposing)
            {
                EnabledBindable.UnbindAll();
                sampleScheduler.Reset();
                sampleCache.Clear();
                beatmapAccessOrder.Clear();
            }

            previewSampleStore?.Dispose();
            previewSampleStore = null;

            base.Dispose(isDisposing);
        }

        #endregion

        /// <summary>
        /// 为指定谱面启动预览。
        /// 若命中音效数量低于阈值，会自动回退到“仅 BGM”的标准预览。
        /// </summary>
        /// <param name="beatmap">要预览的谱面</param>
        /// <param name="forceEnhanced">是否强制使用增强预览（忽略命中音效数量阈值）</param>
        public bool StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            if (!EnabledBindable.Value)
                return false;

            StopPreview();
            resetPreviewSampleStoreForSet(beatmap);

            currentBeatmap = beatmap;
            currentTrack = CreateTrack(beatmap, out ownsCurrentTrack);
            previewMainAudioAvailable = currentTrack is not null && currentTrack is not TrackVirtual;

            if (currentTrack == null)
                Logger.Log("EzPreviewTrackManager: currentTrack is null (falling back?)", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

            playback.ResetPlaybackProgress();

            previewHitSoundsEnabled = forceEnhanced || fastCheckShouldPreviewHitSounds(beatmap, hitsound_threshold);
            previewStoryboardEnabled = hasStoryboardSamples(beatmap.Storyboard);

            if (!forceEnhanced && !previewHitSoundsEnabled && !previewStoryboardEnabled)
                return false;

            startEnhancedPreview(beatmap);
            return true;
        }

        public void StopPreview()
        {
            StopPreviewInternal();
        }

        protected void StopPreviewInternal()
        {
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

            // 切歌时必须同步切断旧歌已经发出的样本通道，尤其是 virtual 谱面下的 storyboard / keysound。
            stopActiveChannels();

            // 保存到缓存而不是完全清空
            saveCurrentBeatmapToCache();
            currentBeatmap = null;
            currentTrack = null;
            previewMainAudioAvailable = false;
            previewHitSoundsEnabled = false;
            previewStoryboardEnabled = false;
            playback.ResetPlaybackProgress();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            fallbackSampleStore = AudioManager.Samples;

            InternalChild = audioContainer = new Container
            {
                RelativeSizeAxes = Axes.Both
            };

            // EnabledBindable.BindValueChanged(_ =>
            // {
            //     // 在主调度线程执行 Stop/Start 操作以保证线程安全。
            //     Schedule(() =>
            //     {
            //         if (currentBeatmap != null)
            //         {
            //             StopPreviewInternal();
            //             StartPreview(currentBeatmap);
            //         }
            //     });
            // }, false);
        }

        private void resetPreviewSampleStoreForSet(IWorkingBeatmap beatmap)
        {
            var newSet = getBeatmapSet(beatmap);

            if (previewSampleStore != null && !EqualityComparer<IBeatmapSetInfo?>.Default.Equals(newSet, lastPreviewSet))
            {
                previewSampleStore.Dispose();
                previewSampleStore = null;
            }

            lastPreviewSet = newSet;

            previewSampleStore ??= createPreviewSampleStore();
        }

        private static IBeatmapSetInfo? getBeatmapSet(IWorkingBeatmap beatmap) => beatmap.BeatmapInfo?.BeatmapSet;

        private ISampleStore createPreviewSampleStore()
        {
            var source = fallbackSampleStore ?? AudioManager.Samples;
            var store = new ResourceStore<byte[]>(new SampleStreamResourceStore(source));
            var preview = AudioManager.GetSampleStore(store, AudioManager.SampleMixer);

            // Allow .ogg lookups in addition to the default wav/mp3.
            preview.AddExtension(@"ogg");
            return preview;
        }

        private sealed class SampleStreamResourceStore : IResourceStore<byte[]>
        {
            private readonly ISampleStore source;

            public SampleStreamResourceStore(ISampleStore source)
            {
                this.source = source;
            }

            public byte[] Get(string name)
            {
                using (var stream = source.GetStream(name))
                {
                    using (var memory = new MemoryStream())
                    {
                        stream?.CopyTo(memory);
                        return memory.ToArray();
                    }
                }
            }

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.Run(() => Get(name), cancellationToken);

            public Stream GetStream(string name) => source.GetStream(name);

            public IEnumerable<string> GetAvailableResources() => source.GetAvailableResources();

            public void Dispose()
            {
            }
        }

        // 快速判定该谱面是否属于 KeySound 谱：遍历命中对象直到达到阈值即返回 true。
        // 统计谱面中命中对象所使用的采样音频的唯一文件名数量（使用 HitSampleInfo.LookupNames 的首选值）。
        private bool fastCheckShouldPreviewHitSounds(IWorkingBeatmap beatmap, int threshold)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var obj in beatmap.Beatmap.HitObjects)
            {
                var stack = new Stack<HitObject>();
                stack.Push(obj);

                while (stack.Count > 0)
                {
                    var ho = stack.Pop();

                    foreach (var sm in ho.Samples)
                    {
                        string? first = sm.LookupNames.FirstOrDefault();
                        if (first == null || !set.Add(first))
                            continue;

                        if (set.Count >= threshold)
                            return true;
                    }

                    foreach (var n in ho.NestedHitObjects)
                        stack.Push(n);
                }
            }

            return set.Count >= threshold;
        }

        private static bool hasStoryboardSamples(Storyboard? storyboard)
        {
            if (storyboard?.Layers == null)
                return false;

            foreach (var layer in storyboard.Layers)
            {
                if (layer.Elements.OfType<StoryboardSampleInfo>().Any())
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 启动增强预览（BGM + 命中音效 + 故事板音效）。
        /// </summary>
        private void startEnhancedPreview(IWorkingBeatmap beatmap)
        {
            try
            {
                beatmap.PrepareTrackForPreview(true);
                playback.PreviewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;

                if (playback.PreviewStartTime < 0 || playback.PreviewStartTime > (currentTrack?.Length ?? 0))
                {
                    // virtual 谱面没有可供“截取副歌”的真实主音频，预览起点无效时应从时间线开头开始，
                    // 否则会直接跳过前半段 storyboard sample。
                    playback.PreviewStartTime = previewMainAudioAvailable
                        ? (currentTrack?.Length ?? 0) * 0.4
                        : 0;
                }

                // 尝试从缓存恢复
                bool cacheHit = restoreFromCache(beatmap);

                if (!cacheHit)
                {
                    // 缓存未命中，需要重新准备并保存
                    var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);
                    prepareScheduledData(playableBeatmap, beatmap.Storyboard);
                }

                applyPreviewModeToScheduledData();

                resetScheduledTriggers();

                double longestEventTime = Math.Max(previewHitSoundsEnabled ? sampleScheduler.LongestHitTime : 0, previewStoryboardEnabled ? sampleScheduler.LongestStoryboardTime : 0);

                // 对预览来说，track.Length 是整段时钟可推进的长度；
                // 但当 AudioFilename 为 virtual 时，它并不代表真实主音频存在。
                double trackTimelineEndTime = currentTrack?.Length ?? 0;
                double mainAudioEndTime = previewMainAudioAvailable ? trackTimelineEndTime : 0;

                // 预览循环必须以“时钟推进长度 / 谱面事件长度”的较长者为基准。
                // 主音频是否存在单独控制，不再把 virtual track 的长度误当成真实音频长度。
                playback.PreviewEndTime = Math.Max(trackTimelineEndTime, longestEventTime);

                if (playback.PreviewEndTime <= playback.PreviewStartTime)
                    playback.PreviewEndTime = Math.Max(trackTimelineEndTime, playback.PreviewStartTime + 1);

                playback.TrackLoopLength = Math.Max(1, trackTimelineEndTime - playback.PreviewStartTime);
                playback.ShortBgmOneShotMode = previewMainAudioAvailable && mainAudioEndTime + trigger_tolerance < playback.PreviewEndTime;
                playback.ResetPlaybackProgress();

                if (!previewMainAudioAvailable)
                    playback.ResetLogicalClock(playback.PreviewStartTime, Time.Current);

                if (sampleScheduler.ScheduledHitSounds.Count == 0 && sampleScheduler.ScheduledStoryboardSamples.Count == 0)
                {
                    clearEnhancedElements();
                    return;
                }

                sampleScheduler.ResetIndices(playback.PreviewStartTime);

                currentTrack?.Seek(playback.PreviewStartTime);

                if (currentTrack != null)
                {
                    currentTrack.Volume.Value = 1f;
                    currentTrack.Looping = true;
                    currentTrack.RestartPoint = playback.PreviewStartTime;
                }

                if (shouldStartControlledTrack())
                    currentTrack?.Start();

                playback.IsPlaying = true;

                // 保持样本调度逻辑以触发命中音效与故事板样本。
                updateDelegate = Scheduler.AddDelayed(updateSamples, scheduler_interval, true);
                updateSamples();
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: startEnhancedPreview error: {ex}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                clearEnhancedElements();
            }
        }

        private void prepareHitSounds(IBeatmap beatmap)
        {
            sampleScheduler.ScheduledHitSounds.Clear();
            sampleScheduler.LongestHitTime = 0;

            foreach (var ho in beatmap.HitObjects)
                schedule(ho);

            sampleScheduler.ScheduledHitSounds.Sort((a, b) => a.Time.CompareTo(b.Time));

            void schedule(HitObject ho)
            {
                if (ho.Samples.Any())
                {
                    sampleScheduler.ScheduledHitSounds.Add(new ScheduledHitSound
                    {
                        Time = ho.StartTime,
                        Samples = ho.Samples.ToArray(),
                        HasTriggered = false
                    });

                    sampleScheduler.LongestHitTime = Math.Max(sampleScheduler.LongestHitTime, ho.StartTime);
                }

                foreach (var n in ho.NestedHitObjects)
                    schedule(n);
            }
        }

        private void prepareStoryboardSamples(Storyboard? storyboard)
        {
            sampleScheduler.ScheduledStoryboardSamples.Clear();
            sampleScheduler.LongestStoryboardTime = 0;

            if (storyboard?.Layers == null) return;

            foreach (var layer in storyboard.Layers)
            {
                foreach (var element in layer.Elements)
                {
                    if (element is StoryboardSampleInfo s)
                    {
                        sampleScheduler.ScheduledStoryboardSamples.Add(new ScheduledStoryboardSample
                        {
                            Time = s.StartTime,
                            Sample = s,
                            HasTriggered = false
                        });
                        sampleScheduler.LongestStoryboardTime = Math.Max(sampleScheduler.LongestStoryboardTime, s.StartTime);
                    }
                }
            }

            sampleScheduler.ScheduledStoryboardSamples.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        private void applyPreviewModeToScheduledData()
        {
            if (!previewHitSoundsEnabled)
            {
                sampleScheduler.ScheduledHitSounds.Clear();
                sampleScheduler.LongestHitTime = 0;
            }

            if (!previewStoryboardEnabled)
            {
                sampleScheduler.ScheduledStoryboardSamples.Clear();
                sampleScheduler.StoryboardSampleCache.Clear();
                sampleScheduler.LongestStoryboardTime = 0;
            }
        }

        // 样本预加载：去重后调用一次 GetChannel() 以确保缓存 / 文件读取
        private void preloadSamples()
        {
            try
            {
                if (previewHitSoundsEnabled)
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
                                        if (!ch.IsDisposed && !ch.ManualFree)
                                            ch.Dispose();
                                    }
                                }
                            }
                        }
                    }
                }

                if (previewStoryboardEnabled)
                {
                    var uniqueStoryboard = new HashSet<string>();

                    foreach (var sb in sampleScheduler.ScheduledStoryboardSamples)
                    {
                        // 通过统一的 fetchStoryboardSample 进行预热
                        var fetched = fetchStoryboardSample(sb.Sample, true);

                        if (fetched.sample != null && uniqueStoryboard.Add(fetched.chosenKey))
                        {
                            var ch = fetched.sample.GetChannel();

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
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: Preload error {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        // 调度函数基于索引推进
        private void updateSamples()
        {
            if (!playback.IsPlaying || currentTrack == null) return;

            double physicalTime = currentTrack.CurrentTime;

            if (isControlledPlaybackPaused() || !currentTrack.IsRunning)
            {
                if (currentTrack.IsDisposed)
                {
                    StopPreview();
                    return;
                }

                if (!playback.TrackPausedExternally)
                {
                    stopActiveChannels();
                    playback.TrackPausedExternally = true;
                }

                if (!previewMainAudioAvailable)
                    playback.LastLogicalClockTime = Time.Current;

                playback.LastTrackTime = physicalTime;
                return;
            }

            playback.TrackPausedExternally = false;
            double logicalTime;

            if (!previewMainAudioAvailable)
            {
                double schedulerTime = Time.Current;
                double elapsed = Math.Max(0, schedulerTime - playback.LastLogicalClockTime);

                playback.LastLogicalClockTime = schedulerTime;
                playback.LogicalClockTime += elapsed;
                logicalTime = playback.LogicalClockTime;
            }
            else
            {
                if (playback.LastTrackTime != 0 && physicalTime + 200 < playback.LastTrackTime)
                {
                    playback.TrackLoopCount++;

                    if (playback.ShortBgmOneShotMode && !playback.ShortBgmMutedAfterFirstLoop)
                    {
                        currentTrack.Volume.Value = 0f;
                        playback.ShortBgmMutedAfterFirstLoop = true;
                    }
                }

                logicalTime = playback.PreviewStartTime + playback.TrackLoopCount * playback.TrackLoopLength + Math.Max(0, physicalTime - playback.PreviewStartTime);
            }

            if (logicalTime > playback.PreviewEndTime + trigger_tolerance)
            {
                restartPreviewCycle();
                return;
            }

            double logicalTimeForEvents = logicalTime;

            if (previewHitSoundsEnabled)
            {
                sampleScheduler.NextHitSoundIndex = findNextValidIndex(sampleScheduler.ScheduledHitSounds, sampleScheduler.NextHitSoundIndex, logicalTimeForEvents - trigger_tolerance);

                while (sampleScheduler.NextHitSoundIndex < sampleScheduler.ScheduledHitSounds.Count)
                {
                    var hs = sampleScheduler.ScheduledHitSounds[sampleScheduler.NextHitSoundIndex];

                    if (hs.HasTriggered)
                    {
                        sampleScheduler.NextHitSoundIndex++;
                        continue;
                    }

                    if (hs.Time > logicalTimeForEvents + trigger_tolerance) break;

                    if (Math.Abs(hs.Time - logicalTimeForEvents) <= trigger_tolerance)
                    {
                        triggerHitSound(hs.Samples);
                        hs.HasTriggered = true;
                        sampleScheduler.ScheduledHitSounds[sampleScheduler.NextHitSoundIndex] = hs;
                        sampleScheduler.NextHitSoundIndex++;
                    }
                    else if (hs.Time < logicalTimeForEvents - trigger_tolerance)
                    {
                        // 已错过（比如用户 Seek）
                        hs.HasTriggered = true;
                        sampleScheduler.ScheduledHitSounds[sampleScheduler.NextHitSoundIndex] = hs;
                        sampleScheduler.NextHitSoundIndex++;
                    }
                    else break;
                }
            }

            if (previewStoryboardEnabled)
            {
                // 同样优化 storyboard samples
                sampleScheduler.NextStoryboardSampleIndex = findNextValidIndex(sampleScheduler.ScheduledStoryboardSamples, sampleScheduler.NextStoryboardSampleIndex,
                    logicalTimeForEvents - trigger_tolerance);

                while (sampleScheduler.NextStoryboardSampleIndex < sampleScheduler.ScheduledStoryboardSamples.Count)
                {
                    var sb = sampleScheduler.ScheduledStoryboardSamples[sampleScheduler.NextStoryboardSampleIndex];

                    if (sb.HasTriggered)
                    {
                        sampleScheduler.NextStoryboardSampleIndex++;
                        continue;
                    }

                    if (sb.Time > logicalTimeForEvents + trigger_tolerance) break;

                    if (Math.Abs(sb.Time - logicalTimeForEvents) <= trigger_tolerance)
                    {
                        triggerStoryboardSample(sb.Sample);
                        sb.HasTriggered = true;
                        sampleScheduler.ScheduledStoryboardSamples[sampleScheduler.NextStoryboardSampleIndex] = sb;
                        sampleScheduler.NextStoryboardSampleIndex++;
                    }
                    else if (sb.Time < logicalTimeForEvents - trigger_tolerance)
                    {
                        sb.HasTriggered = true;
                        sampleScheduler.ScheduledStoryboardSamples[sampleScheduler.NextStoryboardSampleIndex] = sb;
                        sampleScheduler.NextStoryboardSampleIndex++;
                    }
                    else break;
                }
            }

            cleanupInactiveChannels();
            playback.LastTrackTime = physicalTime;
        }

        private void cleanupInactiveChannels()
        {
            for (int i = sampleScheduler.ActiveChannels.Count - 1; i >= 0; i--)
            {
                var channel = sampleScheduler.ActiveChannels[i];

                if (channel.Playing)
                    continue;

                try
                {
                    if (!channel.IsDisposed && !channel.ManualFree)
                        channel.Dispose();
                }
                catch
                {
                    // Ignore disposal errors.
                }

                sampleScheduler.ActiveChannels.RemoveAt(i);
            }
        }

        private void stopActiveChannels()
        {
            foreach (var channel in sampleScheduler.ActiveChannels)
            {
                channel.Stop();

                if (!channel.IsDisposed && !channel.ManualFree)
                    channel.Dispose();
            }

            sampleScheduler.ActiveChannels.Clear();
        }

        private void resetScheduledTriggers(bool stopActiveChannels = false)
        {
            if (stopActiveChannels)
                this.stopActiveChannels();

            for (int i = 0; i < sampleScheduler.ScheduledHitSounds.Count; i++)
            {
                var scheduled = sampleScheduler.ScheduledHitSounds[i];
                scheduled.HasTriggered = false;
                sampleScheduler.ScheduledHitSounds[i] = scheduled;
            }

            for (int i = 0; i < sampleScheduler.ScheduledStoryboardSamples.Count; i++)
            {
                var scheduled = sampleScheduler.ScheduledStoryboardSamples[i];
                scheduled.HasTriggered = false;
                sampleScheduler.ScheduledStoryboardSamples[i] = scheduled;
            }
        }

        private void restartPreviewCycle()
        {
            if (currentTrack == null)
                return;

            resetScheduledTriggers(stopActiveChannels: true);
            sampleScheduler.ResetIndices(playback.PreviewStartTime);
            playback.ResetPlaybackProgress();

            if (!previewMainAudioAvailable)
                playback.ResetLogicalClock(playback.PreviewStartTime, Time.Current);

            currentTrack.Volume.Value = 1f;
            currentTrack.Seek(playback.PreviewStartTime);

            if (!currentTrack.IsRunning && shouldStartControlledTrack())
                currentTrack.Start();
        }

        private bool shouldStartControlledTrack() => !isControlledPlaybackPaused();

        private bool isControlledPlaybackPaused() => musicController?.UserPauseRequested == true;

        private void triggerHitSound(HitSampleInfo[] samples)
        {
            if (samples.Length == 0) return;

            try
            {
                foreach (var info in samples)
                {
                    // bool playedAny = false;

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
                        break; // 只需播放命中链中的首个可用样本
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerHitSound error: {ex}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        // note hitsound 只使用谱面采样，避免把用户皮肤/默认皮肤里的全局打击音误当作 KeySound 预览播放出来。
        private IEnumerable<ISample?> fetchSamplesForInfo(HitSampleInfo info, bool preloadOnly = false)
        {
            var sample = currentBeatmap?.Skin.GetSample(info);
            if (sample != null)
                yield return sample;
        }

        private void triggerStoryboardSample(StoryboardSampleInfo sampleInfo)
        {
            try
            {
                var (sample, _, _) = fetchStoryboardSample(sampleInfo);

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
                // Logger.Log($"EzPreviewTrackManager: Played storyboard sample {sampleInfo.Path} <- {chosenKey}");
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerStoryboardSample error: {ex}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        /// <summary>
        /// 统一 storyboard 样本获取逻辑。返回 (sample, 命中的key, 尝试列表)
        /// 顺序：缓存 -> beatmap skin
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

            // 3. 若谱面本地资源未命中，则退到当前用户皮肤/默认皮肤。
            if (selected == null)
                consider(skinManager?.GetSample(info), "skinManager:" + normalizedPath);

            // 4. 写缓存（即使 null 也缓存，避免重复磁盘尝试；预加载阶段写入，触发阶段复用）
            sampleScheduler.StoryboardSampleCache[normalizedPath] = selected;

            return (selected, chosenKey, tried);
        }

        private void clearEnhancedElements()
        {
            // 停止并释放所有仍在播放的样本通道，避免依赖最终化器来回收短期通道
            stopActiveChannels();

            sampleScheduler.Reset();
            playback.ShortBgmOneShotMode = false;
            playback.ShortBgmMutedAfterFirstLoop = false;
            // 避免在非更新线程直接操作 InternalChildren 导致 InvalidThreadForMutationException
            Schedule(() => audioContainer.Clear());
        }

        /// <summary>
        /// 准备调度数据（命中音效和故事板样本）。
        /// </summary>
        private void prepareScheduledData(IBeatmap beatmap, Storyboard? storyboard)
        {
            prepareHitSounds(beatmap);
            prepareStoryboardSamples(storyboard);
        }

        /// <summary>
        /// 保存当前谱面的样本数据到 LRU 缓存。
        /// </summary>
        private void saveCurrentBeatmapToCache()
        {
            if (currentBeatmap == null || currentBeatmap.BeatmapInfo?.BeatmapSet?.OnlineID <= 0)
                return;

            string beatmapSetId = currentBeatmap.BeatmapInfo!.BeatmapSet!.OnlineID.ToString();

            // 保存到缓存
            if (!sampleCache.TryGetValue(beatmapSetId, out var value))
            {
                value = new BeatmapSampleCache();
                sampleCache[beatmapSetId] = value;
            }

            var cache = value;
            cache.ScheduledHitSounds = sampleScheduler.ScheduledHitSounds.ToArray();
            cache.ScheduledStoryboardSamples = sampleScheduler.ScheduledStoryboardSamples.ToArray();
            cache.StoryboardSampleCache = new Dictionary<string, ISample?>(sampleScheduler.StoryboardSampleCache);
            cache.LongestHitTime = sampleScheduler.LongestHitTime;
            cache.LongestStoryboardTime = sampleScheduler.LongestStoryboardTime;

            // 更新访问顺序（LRU）
            beatmapAccessOrder.Remove(beatmapSetId);
            beatmapAccessOrder.AddFirst(beatmapSetId);

            // 如果超过缓存上限，移除最旧的
            while (beatmapAccessOrder.Count > max_cached_beatmaps)
            {
                string? oldest = beatmapAccessOrder.Last?.Value;

                if (oldest != null)
                {
                    sampleCache.Remove(oldest);
                    beatmapAccessOrder.RemoveLast();
                }
            }
        }

        /// <summary>
        /// 尝试从缓存恢复谱面的样本数据。
        /// </summary>
        private bool restoreFromCache(IWorkingBeatmap beatmap)
        {
            if (beatmap.BeatmapInfo?.BeatmapSet?.OnlineID <= 0)
                return false;

            string beatmapSetId = beatmap.BeatmapInfo!.BeatmapSet!.OnlineID.ToString();

            if (!sampleCache.TryGetValue(beatmapSetId, out var cache))
                return false;

            // 从缓存恢复数据（使用 Clear + AddRange 方式避免 readonly 字段赋值错误）
            sampleScheduler.ScheduledHitSounds.Clear();
            sampleScheduler.ScheduledHitSounds.AddRange(cache.ScheduledHitSounds);

            sampleScheduler.ScheduledStoryboardSamples.Clear();
            sampleScheduler.ScheduledStoryboardSamples.AddRange(cache.ScheduledStoryboardSamples);

            sampleScheduler.StoryboardSampleCache.Clear();

            foreach (var kvp in cache.StoryboardSampleCache)
            {
                sampleScheduler.StoryboardSampleCache[kvp.Key] = kvp.Value;
            }

            sampleScheduler.LongestHitTime = cache.LongestHitTime;
            sampleScheduler.LongestStoryboardTime = cache.LongestStoryboardTime;

            // 更新访问顺序（LRU）
            beatmapAccessOrder.Remove(beatmapSetId);
            beatmapAccessOrder.AddFirst(beatmapSetId);

            return true;
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

        private sealed class PlaybackState
        {
            public bool IsPlaying;
            public bool TrackPausedExternally;

            public double PreviewStartTime;
            public double PreviewEndTime;

            public double LastTrackTime;

            public double TrackLoopLength;
            public int TrackLoopCount;

            public double LogicalClockTime;
            public double LastLogicalClockTime;

            public bool ShortBgmOneShotMode;
            public bool ShortBgmMutedAfterFirstLoop;

            public void ResetPlaybackProgress()
            {
                LastTrackTime = 0;
                TrackLoopCount = 0;
                LogicalClockTime = 0;
                LastLogicalClockTime = 0;
                ShortBgmMutedAfterFirstLoop = false;
                TrackPausedExternally = false;
            }

            public void ResetLogicalClock(double previewStartTime, double schedulerTime)
            {
                LogicalClockTime = previewStartTime;
                LastLogicalClockTime = schedulerTime;
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

            // 用于缓存的最长事件时间
            public double LongestHitTime;
            public double LongestStoryboardTime;

            public void ResetIndices(double startTime)
            {
                NextHitSoundIndex = findNextValidIndex(ScheduledHitSounds, 0, startTime);
                NextStoryboardSampleIndex = findNextValidIndex(ScheduledStoryboardSamples, 0, startTime);
            }

            public void Reset()
            {
                ActiveChannels.Clear();
                ScheduledHitSounds.Clear();
                ScheduledStoryboardSamples.Clear();
                StoryboardSampleCache.Clear();
                LongestHitTime = 0;
                LongestStoryboardTime = 0;
                ResetIndices(0);
            }
        }

        /// <summary>
        /// 用于 LRU 缓存的谱面样本数据结构。
        /// </summary>
        private sealed class BeatmapSampleCache
        {
            public ScheduledHitSound[] ScheduledHitSounds { get; set; } = Array.Empty<ScheduledHitSound>();
            public ScheduledStoryboardSample[] ScheduledStoryboardSamples { get; set; } = Array.Empty<ScheduledStoryboardSample>();
            public Dictionary<string, ISample?> StoryboardSampleCache { get; set; } = new Dictionary<string, ISample?>();
            public double LongestHitTime { get; set; }
            public double LongestStoryboardTime { get; set; }
        }

        protected virtual Track? CreateTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            var beatmapTrack = beatmap.Track;

            if (beatmapTrack is TrackVirtual)
                beatmapTrack.Length = getVirtualTimelineLength(beatmap);

            ownsTrack = false;
            return beatmapTrack;
        }

        private double getVirtualTimelineLength(IWorkingBeatmap beatmap)
        {
            double previewTime = Math.Max(0, OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime);
            double mappedEndTime = 0;

            if (beatmap.Beatmap.HitObjects.Any())
                mappedEndTime = beatmap.Beatmap.GetLastObjectTime();
            else if (beatmap.BeatmapInfo.Length > 0)
                mappedEndTime = previewTime + beatmap.BeatmapInfo.Length;

            double storyboardEndTime = beatmap.Storyboard?.LatestEventTime ?? 0;
            double lastRelevantTime = Math.Max(previewTime, Math.Max(mappedEndTime, storyboardEndTime));

            // virtual 谱面没有真实主音频时，额外补若干拍尾巴，保证末尾事件后仍有可用时钟空间。
            double beatLength = beatmap.Beatmap.ControlPointInfo.TimingPointAt(lastRelevantTime).BeatLength;

            if (double.IsNaN(beatLength) || double.IsInfinity(beatLength) || beatLength <= 0)
                beatLength = 1000;

            double tailPadding = Math.Max(beatLength * 4, 1000);
            return Math.Max(lastRelevantTime + tailPadding, previewTime + tailPadding);
        }
    }
}
