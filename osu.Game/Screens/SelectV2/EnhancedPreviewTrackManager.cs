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

namespace osu.Game.Screens.SelectV2
{
    /// <summary>
    /// An enhanced preview system that can play background music, hit sounds, and storyboard samples simultaneously during beatmap preview.
    /// </summary>
    public partial class EnhancedPreviewTrackManager : CompositeDrawable
    {
        public bool IsPlaying => isPlaying && currentTrack?.IsRunning == true;
        private const int hitsound_threshold = 10;
        private const double preview_window_length = 30000; // 30s
        private const double scheduler_interval = 16; // ~60fps
        private const double trigger_tolerance = 90; // ms 容差
        private const double bgm_duck_volume = 0.9; // 轻微压低主轨来突出打击音 (可后续做成设置)
        private readonly List<ScheduledHitSound> scheduledHitSounds = new List<ScheduledHitSound>();

        private readonly List<ScheduledStoryboardSample> scheduledStoryboardSamples = new List<ScheduledStoryboardSample>();

        // storyboard 样本缓存，预加载时写入，触发时优先命中
        private readonly Dictionary<string, ISample?> storyboardSampleCache = new Dictionary<string, ISample?>();

        // 运行时索引，避免每帧全表扫描
        private int nextHitSoundIndex;
        private int nextStoryboardSampleIndex;

        // 预加载统计
        private int uniqueHitsoundCount;

        private Track? currentTrack;

        private IWorkingBeatmap? currentBeatmap;
        private bool isPlaying;
        private double previewStartTime;
        private double previewEndTime; // 新增：保存窗口结束，用于循环时可能的逻辑扩展
        private double lastTrackTime;   // 新增：用于检测循环
        private ScheduledDelegate? updateDelegate;
        private Container audioContainer = null!;

        private ISampleStore sampleStore = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

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
            currentTrack = beatmap.Track;

            // 快速阈值判定（短路）
            bool enableEnhanced = forceEnhanced || fastCheckShouldUseEnhanced(beatmap, hitsound_threshold);

            if (!enableEnhanced)
            {
                Logger.Log("EnhancedPreviewTrackManager: Using standard preview (hitsound below threshold)", LoggingTarget.Runtime);
                startStandardPreview(beatmap);
                return;
            }

            Logger.Log("EnhancedPreviewTrackManager: Using enhanced preview", LoggingTarget.Runtime);
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
            Logger.Log($"EnhancedPreviewTrackManager: Stopping preview (reason={reason})", LoggingTarget.Runtime);
            isPlaying = false;
            updateDelegate?.Cancel();
            updateDelegate = null;

            if (currentTrack != null)
            {
                currentTrack.Volume.Value = 1f; // 恢复
                currentTrack.Stop();
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
            sampleStore = audioManager.Samples;

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
                    {
                        uniqueHitsoundCount = set.Count;
                        return true;
                    }
                }

                uniqueHitsoundCount = set.Count;
                Logger.Log($"EnhancedPreviewTrackManager: Unique hitsounds = {uniqueHitsoundCount}", LoggingTarget.Runtime);
                return set.Count >= threshold;
            }
            catch (Exception ex)
            {
                Logger.Log($"EnhancedPreviewTrackManager: fastCheck error {ex.Message}", LoggingTarget.Runtime);
                return false;
            }

            void collect(HitObject ho, HashSet<HitSampleInfo> s)
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
            previewStartTime = beatmap.BeatmapInfo.Metadata.PreviewTime;

            if (previewStartTime < 0 || previewStartTime > currentTrack?.Length)
                previewStartTime = (currentTrack?.Length ?? 0) * 0.4;

            currentTrack?.Seek(previewStartTime);
            currentTrack?.Start();
            isPlaying = true;
        }

        /// <summary>
        /// Starts enhanced preview with BGM, hitsounds, and storyboard samples.
        /// </summary>
        private void startEnhancedPreview(IWorkingBeatmap beatmap)
        {
            try
            {
                var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

                beatmap.PrepareTrackForPreview(true);
                previewStartTime = beatmap.BeatmapInfo.Metadata.PreviewTime;
                if (previewStartTime < 0 || previewStartTime > (currentTrack?.Length ?? 0))
                    previewStartTime = (currentTrack?.Length ?? 0) * 0.4;

                double previewEndTimes = previewStartTime + preview_window_length;
                previewEndTime = previewEndTimes; // 保存到字段
                lastTrackTime = previewStartTime;

                Logger.Log($"EnhancedPreviewTrackManager: Preview window {previewStartTime} - {previewEndTimes}", LoggingTarget.Runtime);

                prepareHitSounds(playableBeatmap, previewEndTimes);
                prepareStoryboardSamples(beatmap.Storyboard, previewEndTimes);
                preloadSamples(); // 预加载，减少首次卡顿 & 提升命中率

                if (scheduledHitSounds.Count == 0 && scheduledStoryboardSamples.Count == 0)
                {
                    Logger.Log("EnhancedPreviewTrackManager: No scheduled samples, fallback to standard preview", LoggingTarget.Runtime);
                    clearEnhancedElements();
                    startStandardPreview(beatmap);
                    return;
                }

                nextHitSoundIndex = 0;
                nextStoryboardSampleIndex = 0;

                currentTrack?.Seek(previewStartTime);

                if (currentTrack != null)
                {
                    currentTrack.Looping = true; // 维持原逻辑
                    currentTrack.RestartPoint = previewStartTime;
                }

                currentTrack?.Start();

                // 轻微压低主轨突出打击音（可选择式）
                if (currentTrack != null)
                    currentTrack.Volume.Value = bgm_duck_volume;

                isPlaying = true;

                // 启动调度
                updateDelegate = Scheduler.AddDelayed(updateSamples, scheduler_interval, true);
                // 启动即时执行一次，避免第一批错过
                updateSamples();

                Logger.Log($"EnhancedPreviewTrackManager: Enhanced preview started (hitsounds={scheduledHitSounds.Count}, storyboard={scheduledStoryboardSamples.Count})", LoggingTarget.Runtime);
            }
            catch (Exception ex)
            {
                Logger.Log($"EnhancedPreviewTrackManager: Enhanced preview failed: {ex.Message}", LoggingTarget.Runtime);
                clearEnhancedElements();
                startStandardPreview(beatmap);
            }
        }

        // 改为接受 endTime 参数
        private void prepareHitSounds(IBeatmap beatmap, double previewEndTime)
        {
            scheduledHitSounds.Clear();
            foreach (var ho in beatmap.HitObjects)
                schedule(ho, previewEndTime);
            scheduledHitSounds.Sort((a, b) => a.Time.CompareTo(b.Time));
            Logger.Log($"EnhancedPreviewTrackManager: Prepared {scheduledHitSounds.Count} hitsounds", LoggingTarget.Runtime);

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
            Logger.Log($"EnhancedPreviewTrackManager: Prepared {scheduledStoryboardSamples.Count} storyboard samples", LoggingTarget.Runtime);
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

                Logger.Log($"EnhancedPreviewTrackManager: Preloaded samples (hit={uniqueHitInfos.Count}, storyboard={uniqueStoryboard.Count})", LoggingTarget.Runtime);
            }
            catch (Exception ex)
            {
                Logger.Log($"EnhancedPreviewTrackManager: Preload error {ex.Message}", LoggingTarget.Runtime);
            }
        }

        // 调度函数基于索引推进
        private void updateSamples()
        {
            if (!isPlaying || currentTrack == null) return;

            double currentTime = currentTrack.CurrentTime;

            // 检测循环（Track 跳回起点）。允许少量倒退(>200ms)判断为 loop。
            if (currentTime + 200 < lastTrackTime)
            {
                // 重置所有触发标记，以便循环时再次播放。
                for (int i = 0; i < scheduledHitSounds.Count; i++)
                {
                    var hs = scheduledHitSounds[i];
                    hs.HasTriggered = false;
                    scheduledHitSounds[i] = hs;
                }

                for (int i = 0; i < scheduledStoryboardSamples.Count; i++)
                {
                    var sb = scheduledStoryboardSamples[i];
                    sb.HasTriggered = false;
                    scheduledStoryboardSamples[i] = sb;
                }

                nextHitSoundIndex = 0;
                nextStoryboardSampleIndex = 0;
                Logger.Log("EnhancedPreviewTrackManager: Detected loop -> reset scheduled elements", LoggingTarget.Runtime);
            }

            // hitsounds
            while (nextHitSoundIndex < scheduledHitSounds.Count)
            {
                var hs = scheduledHitSounds[nextHitSoundIndex];

                if (hs.HasTriggered)
                {
                    nextHitSoundIndex++;
                    continue;
                }

                if (hs.Time > currentTime + trigger_tolerance) break; // 未到触发窗口

                if (Math.Abs(hs.Time - currentTime) <= trigger_tolerance)
                {
                    triggerHitSound(hs.Samples);
                    hs.HasTriggered = true;
                    scheduledHitSounds[nextHitSoundIndex] = hs;
                    nextHitSoundIndex++;
                }
                else if (hs.Time < currentTime - trigger_tolerance)
                {
                    // 已错过，直接跳过（避免 Seek 导致错失）
                    hs.HasTriggered = true;
                    scheduledHitSounds[nextHitSoundIndex] = hs;
                    nextHitSoundIndex++;
                }
                else break;
            }

            // storyboard samples
            while (nextStoryboardSampleIndex < scheduledStoryboardSamples.Count)
            {
                var sb = scheduledStoryboardSamples[nextStoryboardSampleIndex];

                if (sb.HasTriggered)
                {
                    nextStoryboardSampleIndex++;
                    continue;
                }

                if (sb.Time > currentTime + trigger_tolerance) break;

                if (Math.Abs(sb.Time - currentTime) <= trigger_tolerance)
                {
                    triggerStoryboardSample(sb.Sample);
                    sb.HasTriggered = true;
                    scheduledStoryboardSamples[nextStoryboardSampleIndex] = sb;
                    nextStoryboardSampleIndex++;
                }
                else if (sb.Time < currentTime - trigger_tolerance)
                {
                    sb.HasTriggered = true;
                    scheduledStoryboardSamples[nextStoryboardSampleIndex] = sb;
                    nextStoryboardSampleIndex++;
                }
                else break;
            }

            lastTrackTime = currentTime; // 记录用于下次循环检测
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

                        var channel = sample.GetChannel();

                        double vol = info.Volume <= 0 ? 1.0 : info.Volume / 100.0;
                        channel.Volume.Value = (float)Math.Clamp(vol, 0, 1);
                        channel.Play();
                        playedAny = true;
                        // Logger.Log($"EnhancedPreviewTrackManager: Played hitsound {info.Name} ({string.Join(',', info.LookupNames)})", LoggingTarget.Runtime);
                        break; // 只需播放命中链中的首个可用样本
                    }

                    if (!playedAny)
                        Logger.Log($"EnhancedPreviewTrackManager: Miss hitsound {info.Bank}-{info.Name}", LoggingTarget.Runtime);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"EnhancedPreviewTrackManager: Error playing hitsound {ex.Message}", LoggingTarget.Runtime);
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
                var (sample, chosenKey, tried) = fetchStoryboardSample(sampleInfo);

                if (sample == null)
                {
                    Logger.Log($"EnhancedPreviewTrackManager: Miss storyboard sample {sampleInfo.Path} (tried: {string.Join("|", tried)})", LoggingTarget.Runtime);
                    return;
                }

                var channel = sample.GetChannel();
                double vol = sampleInfo.Volume <= 0 ? 1.0 : sampleInfo.Volume / 100.0;
                channel.Volume.Value = (float)Math.Clamp(vol, 0, 1);
                channel.Play();
                // Logger.Log($"EnhancedPreviewTrackManager: Played storyboard sample {sampleInfo.Path} <- {chosenKey}", LoggingTarget.Runtime);
            }
            catch (Exception ex)
            {
                Logger.Log($"EnhancedPreviewTrackManager: Error storyboard sample {ex.Message}", LoggingTarget.Runtime);
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
            scheduledHitSounds.Clear();
            scheduledStoryboardSamples.Clear();
            storyboardSampleCache.Clear();
            nextHitSoundIndex = 0;
            nextStoryboardSampleIndex = 0;
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
    }
}
