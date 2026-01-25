// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.LAsEzExtensions.Audio
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

        // 外部时钟驱动（gameplay）模式下，过短的切片会导致高频 Seek/Restart，音频听感会明显“撕裂”。
        // 这里设置一个最小切片长度作为下限，避免 1ms 这类极端情况。
        private const double min_loop_length = 100; // ms

        // 在 gameplay 外部时钟驱动模式下，音频设备/解码缓冲会导致 Track.CurrentTime 与外部时钟存在微小漂移。
        // 若仍按 15ms 容差每帧 Seek，会产生明显的“撕裂/卡带”听感。
        // 因此对“音频轨道重同步”使用更宽容差，并加入冷却时间。
        private const double audio_resync_tolerance = 50; // ms
        private const double audio_resync_cooldown = 120; // ms

        private const double max_dynamic_preview_length = 60000; // 动态扩展最长 ms
        private readonly SampleSchedulerState sampleScheduler = new SampleSchedulerState();
        private readonly PlaybackState playback = new PlaybackState();
        public readonly Bindable<IBeatmap> Beatmap = new Bindable<IBeatmap>();

        private Track? currentTrack;
        private IWorkingBeatmap? currentBeatmap;
        private ScheduledDelegate? updateDelegate;
        private Container audioContainer = null!;
        private ISampleStore sampleStore = null!;

        [Resolved]
        protected AudioManager AudioManager { get; private set; } = null!;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        /// <summary>
        /// 覆盖预览起点时间（毫秒）。
        /// 若为 null，则使用谱面元数据的预览时间（PreviewTime）。
        /// </summary>
        public double? OverridePreviewStartTime { get; set; }

        /// <summary>
        /// 重置循环状态，用于在开始新预览时清除之前的循环进度。
        /// </summary>
        public void ResetLoopState()
        {
            playback.ResetPlaybackProgress();
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
        public bool StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            if (!EnabledBindable.Value)
                return false;

            StopPreview();

            currentBeatmap = beatmap;
            currentTrack = CreateTrack(beatmap, out ownsCurrentTrack);

            if (currentTrack == null)
                Logger.Log("EzPreviewTrackManager: currentTrack is null (falling back?)");

            playback.ResetPlaybackProgress();

            if (!forceEnhanced && !fastCheckShouldUseEnhanced(beatmap, hitsound_threshold)) return false;

            startEnhancedPreview(beatmap);
            return true;
        }

        public void StopPreview()
        {
            StopPreviewInternal("manual");
        }

        protected void StopPreviewInternal(string reason)
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

            clearEnhancedElements();
            currentBeatmap = null;
            currentTrack = null;
            playback.ResetPlaybackProgress();
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
            double longestHitTime = 0;
            double longestStoryboardTime = 0;

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

                // 判定一次性短BGM模式：原始音轨长度 <2s 且 谱面事件覆盖长度 > 10s
                if (currentTrack != null && currentTrack.Length < 2000 && longestEventTime >= playback.PreviewStartTime + preview_window_length)
                    playback.ShortBgmOneShotMode = true;

                prepareHitSounds(playableBeatmap, playback.PreviewEndTime);

                prepareStoryboardSamples(beatmap.Storyboard, playback.PreviewEndTime);
                // preloadSamples();

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
                Logger.Log($"EzPreviewTrackManager: startEnhancedPreview error: {ex}");
                clearEnhancedElements();
            }
        }

        private void prepareHitSounds(IBeatmap beatmap, double previewEndTime)
        {
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
                Logger.Log($"EzPreviewTrackManager: Preload error {ex.Message}");
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

                // 清理已停止的活动通道
                sampleScheduler.ActiveChannels.RemoveAll(c => !c.Playing);
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

            sampleScheduler.ActiveChannels.RemoveAll(c => !c.Playing);
            playback.LastTrackTime = logicalTimeForEvents;
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
                        // Logger.Log($"EzPreviewTrackManager: played channelHash={channelInner.GetHashCode()} volAfter={channelInner.Volume.Value:F3} playing={channelInner.Playing}");
                        sampleScheduler.ActiveChannels.Add(channelInner);
                        // playedAny = true;
                        break; // 只需播放命中链中的首个可用样本
                    }

                    // #if DEBUG
                    // if (!playedAny)
                    //     Logger.Log($"EzPreviewTrackManager: Miss hitsound {info.Bank}-{info.Name}");
                    // #endif
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerHitSound error: {ex}");
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
                // Logger.Log($"EzPreviewTrackManager: Played storyboard sample {sampleInfo.Path} <- {chosenKey}");
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerStoryboardSample error: {ex}");
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
                string withoutExt = Path.ChangeExtension(normalizedPath, null);

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
}
