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
using osu.Game.Rulesets.Objects;
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
        private const double preview_window_length = 20000; // 20s
        private const double scheduler_interval = 16; // ~60fps
        private const double trigger_tolerance = 15; // ms 容差

        private const double max_dynamic_preview_length = 60000; // 动态扩展最长 ms
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

        /// <summary>
        /// 覆盖预览起点时间（毫秒）。
        /// 若为 null，则使用谱面元数据的预览时间（PreviewTime）。
        /// </summary>
        public double? OverridePreviewStartTime { get; set; }

        private bool ownsCurrentTrack;

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

            if (currentTrack == null)
                Logger.Log("EzPreviewTrackManager: currentTrack is null (falling back?)", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

            playback.ResetPlaybackProgress();

            if (!forceEnhanced && !fastCheckShouldUseEnhanced(beatmap, hitsound_threshold)) return false;

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

            // 保存到缓存而不是完全清空
            saveCurrentBeatmapToCache();
            currentBeatmap = null;
            currentTrack = null;
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

        // 快速判定：遍历命中对象直到达到阈值即返回 true。
        // 统计谱面中命中对象所使用的采样音频的唯一文件名数量（使用 HitSampleInfo.LookupNames 的首选值）。
        private bool fastCheckShouldUseEnhanced(IWorkingBeatmap beatmap, int threshold)
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
                        if (first != null && set.Add(first) && set.Count >= threshold)
                            return true;
                    }

                    foreach (var n in ho.NestedHitObjects)
                        stack.Push(n);
                }
            }

            return set.Count >= threshold;
        }

        /// <summary>
        /// 启动增强预览（BGM + 命中音效 + 故事板音效）。
        /// </summary>
        private void startEnhancedPreview(IWorkingBeatmap beatmap)
        {
            double longestHitTime;

            void collectLongest(HitObject ho)
            {
                longestHitTime = Math.Max(longestHitTime, ho.StartTime);
                foreach (var n in ho.NestedHitObjects) collectLongest(n);
            }

            try
            {
                // 尝试从缓存恢复
                bool cacheHit = restoreFromCache(beatmap);

                if (!cacheHit)
                {
                    // 缓存未命中，需要重新准备并保存
                    var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);
                    prepareScheduledData(playableBeatmap, beatmap.Storyboard);
                }

                beatmap.PrepareTrackForPreview(true);
                playback.PreviewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;
                if (playback.PreviewStartTime < 0 || playback.PreviewStartTime > (currentTrack?.Length ?? 0))
                    playback.PreviewStartTime = (currentTrack?.Length ?? 0) * 0.4;

                longestHitTime = sampleScheduler.LongestHitTime;
                double longestStoryboardTime = sampleScheduler.LongestStoryboardTime;

                double longestEventTime = Math.Max(longestHitTime, longestStoryboardTime);
                double defaultEnd = playback.PreviewStartTime + preview_window_length;
                double dynamicEnd = defaultEnd;

                if (currentTrack != null)
                {
                    double segmentAfterStart = Math.Max(1, currentTrack.Length - playback.PreviewStartTime);
                    if (segmentAfterStart < preview_window_length * 0.6 && longestEventTime > defaultEnd)
                        dynamicEnd = Math.Min(playback.PreviewStartTime + max_dynamic_preview_length, longestEventTime);
                }

                double segmentLength = dynamicEnd - playback.PreviewStartTime;

                playback.LoopSegmentLength = Math.Max(1, segmentLength);

                // 播放至谱面事件结束或音轨结尾（取较小者），作为一次循环的结束点。
                playback.PreviewEndTime = Math.Min(currentTrack?.Length ?? double.MaxValue, playback.PreviewStartTime + playback.LoopSegmentLength);

                playback.LastTrackTime = playback.PreviewStartTime;
                playback.LegacyLoopCount = 0;
                playback.LegacyLogicalOffset = 0;
                // external-clock / manual looping behavior has been removed from this manager.
                playback.ShortBgmOneShotMode = false;
                playback.ShortBgmMutedAfterFirstLoop = false;

                // 判定一次性短 BGM 模式：
                // 若主音频非常短（<10s），则将其视为短 BGM，在循环时在第一次循环后静音（从而实现“播放一次可听，后续不再循环”的效果），
                // 同时保留命中音效与故事板音效的触发逻辑。
                if (currentTrack != null)
                {
                    if (currentTrack.Length < 10000 || longestEventTime > (currentTrack.Length + 5000))
                        playback.ShortBgmOneShotMode = true;
                }

                if (sampleScheduler.ScheduledHitSounds.Count == 0 && sampleScheduler.ScheduledStoryboardSamples.Count == 0)
                {
                    clearEnhancedElements();
                    return;
                }

                sampleScheduler.ResetIndices();

                currentTrack?.Seek(playback.PreviewStartTime);

                if (currentTrack != null)
                {
                    // 在已简化的 manager 中，交由底层 track 的 Looping 决定是否循环；我们尽量保持默认可循环体验。
                    currentTrack.Looping = true;
                    currentTrack.RestartPoint = playback.PreviewStartTime;
                }

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

        private void prepareHitSounds(IBeatmap beatmap, double previewEndTime)
        {
            sampleScheduler.ScheduledHitSounds.Clear();
            sampleScheduler.LongestHitTime = 0;

            foreach (var ho in beatmap.HitObjects)
            {
                schedule(ho, previewEndTime);
                sampleScheduler.LongestHitTime = Math.Max(sampleScheduler.LongestHitTime, ho.StartTime);
            }

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
            sampleScheduler.LongestStoryboardTime = 0;

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
                        sampleScheduler.LongestStoryboardTime = Math.Max(sampleScheduler.LongestStoryboardTime, s.StartTime);
                    }
                }
            }

            sampleScheduler.ScheduledStoryboardSamples.Sort((a, b) => a.Time.CompareTo(b.Time));
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
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: Preload error {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        // 调度函数基于索引推进
        private void updateSamples()
        {
            if (!playback.IsPlaying || currentTrack == null) return;

            // 简化：使用底层 track 的 CurrentTime 作为逻辑时间来驱动样本触发。
            double physicalTime = currentTrack.CurrentTime;

            // 回绕检测：若物理时间回绕（小于上一次记录的时间超出阈值），视为一次循环已发生，重置样本触发状态。
            if (playback.LastTrackTime != 0 && physicalTime + 200 < playback.LastTrackTime)
            {
                // 重置命中音效触发标记
                for (int i = 0; i < sampleScheduler.ScheduledHitSounds.Count; i++)
                {
                    var s = sampleScheduler.ScheduledHitSounds[i];
                    s.HasTriggered = false;
                    sampleScheduler.ScheduledHitSounds[i] = s;
                }

                // 重置 storyboard 样本触发标记
                for (int i = 0; i < sampleScheduler.ScheduledStoryboardSamples.Count; i++)
                {
                    var sb = sampleScheduler.ScheduledStoryboardSamples[i];
                    sb.HasTriggered = false;
                    sampleScheduler.ScheduledStoryboardSamples[i] = sb;
                }

                // 重新定位下一触发索引（从当前物理时间附近开始查找）
                sampleScheduler.NextHitSoundIndex = findNextValidIndex(sampleScheduler.ScheduledHitSounds, 0, physicalTime - trigger_tolerance);
                sampleScheduler.NextStoryboardSampleIndex = findNextValidIndex(sampleScheduler.ScheduledStoryboardSamples, 0, physicalTime - trigger_tolerance);

                // 清理已停止的活动通道并归还资源
                cleanupInactiveChannels();
            }

            double logicalTime = physicalTime;

            if (!currentTrack.IsRunning)
            {
                if (currentTrack.IsDisposed)
                {
                    StopPreview();
                    return;
                }

                currentTrack.Start();
            }

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
            sampleScheduler.NextStoryboardSampleIndex = findNextValidIndex(sampleScheduler.ScheduledStoryboardSamples, sampleScheduler.NextStoryboardSampleIndex,
                logicalTimeForEvents - trigger_tolerance);

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

            cleanupInactiveChannels();
            playback.LastTrackTime = logicalTimeForEvents;
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

        // 仅使用谱面资源解析样本，不走皮肤与全局样本库。
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

            // 3. 写缓存（即使 null 也缓存，避免重复磁盘尝试；预加载阶段写入，触发阶段复用）
            sampleScheduler.StoryboardSampleCache[normalizedPath] = selected;

            return (selected, chosenKey, tried);
        }

        private void clearEnhancedElements()
        {
            // 停止并释放所有仍在播放的样本通道，避免依赖最终化器来回收短期通道
            foreach (var channel in sampleScheduler.ActiveChannels)
            {
                channel.Stop();
                if (!channel.IsDisposed && !channel.ManualFree)
                    channel.Dispose();
            }

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
            prepareHitSounds(beatmap, playback.PreviewEndTime);
            prepareStoryboardSamples(storyboard, playback.PreviewEndTime);
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

            public int LegacyLoopCount;
            public double LegacyLogicalOffset;

            public bool ShortBgmOneShotMode;
            public bool ShortBgmMutedAfterFirstLoop;

            public void ResetPlaybackProgress()
            {
                LastTrackTime = 0;
                LegacyLoopCount = 0;
                LegacyLogicalOffset = 0;
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
                LongestHitTime = 0;
                LongestStoryboardTime = 0;
                ResetIndices();
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
            ownsTrack = false;
            return beatmap.Track;
        }
    }
}
