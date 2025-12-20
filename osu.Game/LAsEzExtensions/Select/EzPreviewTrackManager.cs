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
        public bool IsPlaying => isPlaying && currentTrack?.IsRunning == true;
        private const int hitsound_threshold = 10;
        private const double preview_window_length = 10000; // 10s
        private const double scheduler_interval = 16; // ~60fps
        private const double trigger_tolerance = 35; // ms 容差
        private const double max_dynamic_preview_length = 30000; // 动态扩展最长 s
        private readonly List<ScheduledHitSound> scheduledHitSounds = new List<ScheduledHitSound>();

        private readonly List<ScheduledStoryboardSample> scheduledStoryboardSamples = new List<ScheduledStoryboardSample>();

        private readonly Dictionary<string, ISample?> storyboardSampleCache = new Dictionary<string, ISample?>();

        private readonly List<SampleChannel> activeChannels = new List<SampleChannel>();

        private int nextHitSoundIndex;
        private int nextStoryboardSampleIndex;

        private Track? currentTrack;

        private IWorkingBeatmap? currentBeatmap;
        private bool isPlaying;
        private double previewStartTime;
        private double previewEndTime; // 保存窗口结束，用于循环/动态窗口
        private double lastTrackTime; // 用于检测循环
        private double loopSegmentLength; // 短 BGM 循环段长度
        private int loopCount;
        private double logicalOffset; // 逻辑时间偏移（循环叠加）
        private bool shortBgmOneShotMode; // 短BGM一次性播放模式
        private bool shortBgmMutedAfterFirstLoop; // 标记已静音后续循环
        private ScheduledDelegate? updateDelegate;
        private Container audioContainer = null!;

        private ISampleStore sampleStore = null!;

        [Resolved]
        protected AudioManager AudioManager { get; private set; } = null!;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        public bool EnableHitSounds { get; set; } = true;
        public double? OverridePreviewStartTime { get; set; }
        public double? OverridePreviewDuration { get; set; }
        public bool? OverrideLooping { get; set; }
        public int? OverrideLoopCount { get; set; }
        public double? OverrideLoopInterval { get; set; }
        public IClock? ExternalClock { get; set; }

        /// <summary>
        /// When <see cref="ExternalClock"/> is provided, defines the reference time (in the external clock's time domain)
        /// at which this preview should be considered to start.
        /// This is primarily used to delay preview playback until a specific time is reached (e.g. gameplay time == cutStart).
        /// If null, the reference will be captured from the external clock at the time preview starts.
        /// </summary>
        public double? ExternalClockStartTime { get; set; }

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

        public void ResetOverrides()
        {
            OverridePreviewStartTime = null;
            OverridePreviewDuration = null;
            OverrideLoopCount = null;
            OverrideLoopInterval = null;
            OverrideLooping = null;
            EnableHitSounds = true;
        }

        private bool ownsCurrentTrack;
        private bool useExternalLooping;
        private double loopInterval;
        private int effectiveLoopCount;

        private double externalClockStartReference;
        private bool externalClockStartCaptured;
        private double lastReferenceTime;

        #region Disposal

        protected override void Dispose(bool isDisposing)
        {
            StopPreview();
            base.Dispose(isDisposing);
        }

        #endregion

        /// <summary>
        /// Starts enhanced preview for the given beatmap.
        /// Will fall back to standard BGM-only preview if hitsound count is below threshold.
        /// </summary>
        /// <param name="beatmap">The beatmap to preview</param>
        /// <param name="forceEnhanced">Force enhanced preview regardless of hitsound count</param>
        public void StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            if (isPlaying && currentBeatmap == beatmap)
                return;

            StopPreview();

            currentBeatmap = beatmap;
            currentTrack = CreateTrack(beatmap, out ownsCurrentTrack);
            externalClockStartCaptured = false;
            externalClockStartReference = 0;

            bool enableEnhanced = forceEnhanced || fastCheckShouldUseEnhanced(beatmap, hitsound_threshold);

            if (!enableEnhanced)
            {
                startStandardPreview(beatmap);
                return;
            }

            startEnhancedPreview(beatmap);
        }

        /// <summary>
        /// Stops the current preview and cleans up resources.
        /// </summary>
        public void StopPreview()
        {
            stopPreviewInternal("manual");
        }

        private void stopPreviewInternal(string reason)
        {
            Logger.Log($"EzPreviewTrackManager: Stopping preview (reason={reason})", LoggingTarget.Runtime);
            isPlaying = false;
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
            lastTrackTime = 0;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Get sample store for playing audio samples
            sampleStore = AudioManager.Samples;

            // Create audio container for proper audio sample management
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
            catch
            {
                return false;
            }

            static void collect(HitObject ho, HashSet<HitSampleInfo> s)
            {
                foreach (var sm in ho.Samples) s.Add(sm);
                foreach (var n in ho.NestedHitObjects) collect(n, s);
            }
        }

        /// <summary>
        /// Starts standard BGM-only preview.
        /// </summary>
        private void startStandardPreview(IWorkingBeatmap beatmap)
        {
            beatmap.PrepareTrackForPreview(true);
            previewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;

            if (previewStartTime < 0 || previewStartTime > currentTrack?.Length)
                previewStartTime = (currentTrack?.Length ?? 0) * 0.4;

            previewEndTime = OverridePreviewDuration.HasValue
                ? previewStartTime + Math.Max(0, OverridePreviewDuration.Value)
                : previewStartTime + preview_window_length;

            loopSegmentLength = Math.Max(1, previewEndTime - previewStartTime);
            loopInterval = Math.Max(0, OverrideLoopInterval ?? 0);
            effectiveLoopCount = OverrideLoopCount ?? 1;
            // When any slice/loop overrides are provided, the track must be driven manually (Stop/Seek) via updateSamples().
            // Track.Looping/RestartPoint alone cannot enforce Duration/LoopCount/LoopInterval.
            useExternalLooping = ExternalClock != null
                                 || loopInterval > 0
                                 || OverrideLoopCount.HasValue
                                 || OverridePreviewDuration.HasValue
                                 || (OverrideLooping.HasValue && !OverrideLooping.Value);

            if (currentTrack != null)
            {
                currentTrack.Seek(previewStartTime);
                currentTrack.Looping = !useExternalLooping && (OverrideLooping ?? false);
                currentTrack.RestartPoint = previewStartTime;
            }

            currentTrack?.Start();
            isPlaying = true;

            if (useExternalLooping)
            {
                updateDelegate = Scheduler.AddDelayed(updateSamples, scheduler_interval, true);
                updateSamples();
            }
        }

        /// <summary>
        /// Starts enhanced preview with BGM, hitsounds, and storyboard samples.
        /// </summary>
        private void startEnhancedPreview(IWorkingBeatmap beatmap)
        {
            double longestHitTime = 0; // 修复作用域：提前声明
            double longestStoryboardTime = 0;

            lastReferenceTime = 0;

            void collectLongest(HitObject ho)
            {
                longestHitTime = Math.Max(longestHitTime, ho.StartTime);
                foreach (var n in ho.NestedHitObjects) collectLongest(n);
            }

            try
            {
                var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

                beatmap.PrepareTrackForPreview(true);
                previewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;
                if (previewStartTime < 0 || previewStartTime > (currentTrack?.Length ?? 0))
                    previewStartTime = (currentTrack?.Length ?? 0) * 0.4;

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
                double defaultEnd = previewStartTime + preview_window_length;
                double dynamicEnd = defaultEnd;

                if (currentTrack != null)
                {
                    double segmentAfterStart = Math.Max(1, currentTrack.Length - previewStartTime);
                    if (segmentAfterStart < preview_window_length * 0.6 && longestEventTime > defaultEnd)
                        dynamicEnd = Math.Min(previewStartTime + max_dynamic_preview_length, longestEventTime);
                }

                double segmentLength = OverridePreviewDuration.HasValue
                    ? Math.Max(0, OverridePreviewDuration.Value)
                    : dynamicEnd - previewStartTime;

                loopSegmentLength = Math.Max(1, segmentLength);
                effectiveLoopCount = OverrideLoopCount ?? int.MaxValue;
                loopInterval = Math.Max(0, OverrideLoopInterval ?? 0);

                if (effectiveLoopCount == int.MaxValue)
                    previewEndTime = previewStartTime + loopSegmentLength;
                else
                    previewEndTime = previewStartTime + effectiveLoopCount * (loopSegmentLength + loopInterval) - loopInterval;

                lastTrackTime = previewStartTime;
                loopCount = 0;
                logicalOffset = 0;
                useExternalLooping = ExternalClock != null
                                     || loopInterval > 0
                                     || OverrideLoopCount.HasValue
                                     || OverridePreviewDuration.HasValue
                                     || (OverrideLooping.HasValue && !OverrideLooping.Value);
                shortBgmOneShotMode = false;
                shortBgmMutedAfterFirstLoop = false;

                // 判定一次性短BGM模式：原始音轨长度 <2s 且 谱面事件覆盖长度 > 10s
                if (currentTrack != null && currentTrack.Length < 2000 && longestEventTime >= previewStartTime + preview_window_length)
                    shortBgmOneShotMode = true;

                prepareHitSounds(playableBeatmap, previewEndTime);
                prepareStoryboardSamples(beatmap.Storyboard, previewEndTime);
                preloadSamples();

                if (scheduledHitSounds.Count == 0 && scheduledStoryboardSamples.Count == 0)
                {
                    clearEnhancedElements();
                    startStandardPreview(beatmap);
                    return;
                }

                nextHitSoundIndex = 0;
                nextStoryboardSampleIndex = 0;

                currentTrack?.Seek(previewStartTime);

                if (currentTrack != null)
                {
                    currentTrack.Looping = !useExternalLooping && (OverrideLooping ?? true);
                    currentTrack.RestartPoint = previewStartTime;
                }

                currentTrack?.Start();
                isPlaying = true;

                updateDelegate = Scheduler.AddDelayed(updateSamples, scheduler_interval, true);
                updateSamples();
            }
            catch
            {
                clearEnhancedElements();
                startStandardPreview(beatmap);
            }
        }

        // 改为接受 endTime 参数
        private void prepareHitSounds(IBeatmap beatmap, double previewEndTime)
        {
            if (!EnableHitSounds)
            {
                scheduledHitSounds.Clear();
                return;
            }

            scheduledHitSounds.Clear();
            foreach (var ho in beatmap.HitObjects)
                schedule(ho, previewEndTime);
            scheduledHitSounds.Sort((a, b) => a.Time.CompareTo(b.Time));
            // 移除成功日志

            void schedule(HitObject ho, double end)
            {
                if (ho.StartTime >= previewStartTime && ho.StartTime <= end && ho.Samples.Any())
                {
                    scheduledHitSounds.Add(new ScheduledHitSound
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
            scheduledStoryboardSamples.Clear();
            if (storyboard?.Layers == null) return;

            foreach (var layer in storyboard.Layers)
            {
                foreach (var element in layer.Elements)
                {
                    if (element is StoryboardSampleInfo s && s.StartTime >= previewStartTime && s.StartTime <= previewEndTime)
                    {
                        scheduledStoryboardSamples.Add(new ScheduledStoryboardSample
                        {
                            Time = s.StartTime,
                            Sample = s,
                            HasTriggered = false
                        });
                    }
                }
            }

            scheduledStoryboardSamples.Sort((a, b) => a.Time.CompareTo(b.Time));
            // 移除成功日志
        }

        // 样本预加载：去重后调用一次 GetChannel() 以确保缓存 / 文件读取
        private void preloadSamples()
        {
            try
            {
                var uniqueHitInfos = new HashSet<string?>();

                foreach (var s in scheduledHitSounds.SelectMany(h => h.Samples))
                {
                    foreach (var sample in fetchSamplesForInfo(s, true))
                    {
                        string? key = sample?.ToString();

                        if (key != null && uniqueHitInfos.Add(key))
                        {
                            sample?.GetChannel().Stop();
                        }
                    }
                }

                var uniqueStoryboard = new HashSet<string>();

                foreach (var sb in scheduledStoryboardSamples)
                {
                    // 通过统一的 fetchStoryboardSample 进行预热
                    var fetched = fetchStoryboardSample(sb.Sample, true);
                    if (fetched.sample != null && uniqueStoryboard.Add(fetched.chosenKey))
                        fetched.sample.GetChannel().Stop();
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
            if (!isPlaying || currentTrack == null) return;

            if (!tryGetLogicalTime(out double logicalTime, out bool inBreak))
            {
                stopPreviewInternal("loop-end");
                return;
            }

            if (inBreak)
            {
                if (currentTrack.IsRunning)
                    currentTrack.Stop();
                if (Math.Abs(currentTrack.CurrentTime - logicalTime) > trigger_tolerance)
                    currentTrack.Seek(logicalTime);
                lastTrackTime = logicalTime;
                activeChannels.RemoveAll(c => !c.Playing);
                return;
            }

            if (!currentTrack.IsRunning)
                currentTrack.Start();

            if (Math.Abs(currentTrack.CurrentTime - logicalTime) > trigger_tolerance)
                currentTrack.Seek(logicalTime);

            double logicalTimeForEvents = logicalTime;
            bool withinWindow = logicalTimeForEvents <= previewEndTime + trigger_tolerance;

            nextHitSoundIndex = findNextValidIndex(scheduledHitSounds, nextHitSoundIndex, logicalTimeForEvents - trigger_tolerance);

            while (withinWindow && nextHitSoundIndex < scheduledHitSounds.Count)
            {
                var hs = scheduledHitSounds[nextHitSoundIndex];

                if (hs.HasTriggered)
                {
                    nextHitSoundIndex++;
                    continue;
                }

                if (hs.Time > logicalTime + trigger_tolerance) break;

                if (Math.Abs(hs.Time - logicalTime) <= trigger_tolerance)
                {
                    triggerHitSound(hs.Samples);
                    hs.HasTriggered = true;
                    scheduledHitSounds[nextHitSoundIndex] = hs;
                    nextHitSoundIndex++;
                }
                else if (hs.Time < logicalTime - trigger_tolerance)
                {
                    // 已错过（比如用户 Seek）
                    hs.HasTriggered = true;
                    scheduledHitSounds[nextHitSoundIndex] = hs;
                    nextHitSoundIndex++;
                }
                else break;
            }

            // 同样优化 storyboard samples
            nextStoryboardSampleIndex = findNextValidIndex(scheduledStoryboardSamples, nextStoryboardSampleIndex, logicalTimeForEvents - trigger_tolerance);

            while (withinWindow && nextStoryboardSampleIndex < scheduledStoryboardSamples.Count)
            {
                var sb = scheduledStoryboardSamples[nextStoryboardSampleIndex];

                if (sb.HasTriggered)
                {
                    nextStoryboardSampleIndex++;
                    continue;
                }

                if (sb.Time > logicalTime + trigger_tolerance) break;

                if (Math.Abs(sb.Time - logicalTime) <= trigger_tolerance)
                {
                    triggerStoryboardSample(sb.Sample);
                    sb.HasTriggered = true;
                    scheduledStoryboardSamples[nextStoryboardSampleIndex] = sb;
                    nextStoryboardSampleIndex++;
                }
                else if (sb.Time < logicalTime - trigger_tolerance)
                {
                    sb.HasTriggered = true;
                    scheduledStoryboardSamples[nextStoryboardSampleIndex] = sb;
                    nextStoryboardSampleIndex++;
                }
                else break;
            }

            lastTrackTime = logicalTimeForEvents;
            activeChannels.RemoveAll(c => !c.Playing);
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
                        activeChannels.Add(channelInner);
                        playedAny = true;
                        break; // 只需播放命中链中的首个可用样本
                    }

                    if (!playedAny)
                        Logger.Log($"EzPreviewTrackManager: Miss hitsound {info.Bank}-{info.Name}", LoggingTarget.Runtime);
                }
            }
            catch (Exception)
            {
                // 错误保持静默或可选记录，这里不记录成功日志
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
                    Logger.Log($"EzPreviewTrackManager: Miss storyboard sample {sampleInfo.Path} (tried: {string.Join("|", tried)})", LoggingTarget.Runtime);
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
                activeChannels.Add(channel);
                // Logger.Log($"EzPreviewTrackManager: Played storyboard sample {sampleInfo.Path} <- {chosenKey}", LoggingTarget.Runtime);
            }
            catch (Exception)
            {
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
            if (storyboardSampleCache.TryGetValue(normalizedPath, out var cached) && cached != null)
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
            storyboardSampleCache[normalizedPath] = selected;

            return (selected, chosenKey, tried);
        }

        private void clearEnhancedElements()
        {
            // Stop all active sample channels
            foreach (var channel in activeChannels)
                channel.Stop();
            activeChannels.Clear();

            scheduledHitSounds.Clear();
            scheduledStoryboardSamples.Clear();
            storyboardSampleCache.Clear();
            nextHitSoundIndex = 0;
            nextStoryboardSampleIndex = 0;
            shortBgmOneShotMode = false;
            shortBgmMutedAfterFirstLoop = false;
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
            logicalTime = previewStartTime;
            inBreak = false;

            if (!useExternalLooping)
                return legacyTrackLogicalTime(out logicalTime, out inBreak);

            double referenceTime = ExternalClock?.CurrentTime ?? currentTrack?.CurrentTime ?? 0;

            // If the gameplay clock is paused (or effectively paused, i.e. time is not advancing), keep audio paused as well.
            // Without this, updateSamples() will keep seeking to a fixed time while the audio track continues,
            // resulting in an audible "stutter" (the same tiny snippet repeating).
            if (ExternalClock != null)
            {
                // Some clocks may report IsRunning=true even while paused (but time stops advancing).
                // Treat a near-zero delta as paused once we've observed at least one reference time.
                const double paused_delta_epsilon = 0.5; // ms

                if (!ExternalClock.IsRunning
                    || (lastReferenceTime != 0 && Math.Abs(referenceTime - lastReferenceTime) <= paused_delta_epsilon))
                {
                    inBreak = true;
                    logicalTime = lastTrackTime == 0 ? previewStartTime : lastTrackTime;
                    lastReferenceTime = referenceTime;
                    return true;
                }
            }

            if (!externalClockStartCaptured)
            {
                externalClockStartReference = ExternalClockStartTime ?? referenceTime;
                externalClockStartCaptured = true;
            }

            double timeSinceStart = referenceTime - externalClockStartReference;

            double segmentLen = loopSegmentLength + loopInterval;
            if (segmentLen <= 0)
                return false;

            if (timeSinceStart < 0)
            {
                inBreak = true; // keep paused until start
                logicalTime = previewStartTime;
                lastReferenceTime = referenceTime;
                return true;
            }

            double segment = Math.Floor(timeSinceStart / segmentLen);

            if (segment < 0)
            {
                inBreak = true;
                logicalTime = previewStartTime;
                lastReferenceTime = referenceTime;
                return true;
            }

            if (segment >= effectiveLoopCount)
                return false;

            double offset = timeSinceStart - segment * segmentLen;

            if (offset < loopSegmentLength)
            {
                logicalTime = previewStartTime + offset;
                lastReferenceTime = referenceTime;
                return true;
            }

            inBreak = true;
            logicalTime = previewEndTime;

            if (ExternalClock == null && useExternalLooping && inBreak && currentTrack != null && !currentTrack.IsRunning)
            {
                referenceTime += Clock.ElapsedFrameTime;
            }

            lastReferenceTime = referenceTime;
            return true;
        }

        private bool legacyTrackLogicalTime(out double logicalTime, out bool inBreak)
        {
            double physicalTime = currentTrack?.CurrentTime ?? 0;

            if (physicalTime + 200 < lastTrackTime)
            {
                loopCount++;
                logicalOffset = loopCount * loopSegmentLength;

                if (shortBgmOneShotMode && !shortBgmMutedAfterFirstLoop && currentTrack != null)
                {
                    currentTrack.Volume.Value = 0f;
                    shortBgmMutedAfterFirstLoop = true;
                }
            }

            logicalTime = physicalTime + logicalOffset;
            inBreak = false;
            return true;
        }

        protected virtual Track? CreateTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;
            return beatmap.Track;
        }
    }

    public class PreviewOverrideSettings
    {
        public double? PreviewStart { get; init; }
        public double? PreviewDuration { get; init; }
        public int? LoopCount { get; init; }
        public double? LoopInterval { get; init; }
        public bool? ForceLooping { get; init; }
        public bool EnableHitSounds { get; init; } = true;
    }
}
