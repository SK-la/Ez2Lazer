// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
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
    /// Song Select 增强预览：在 <see cref="osu.Game.Screens.Select.SongSelect"/> 中于 BGM（<c>MusicController</c>）之上
    /// 调度谱面 note 音效与 storyboard 样本。BGM 循环与预览起点由 SongSelect 调用
    /// <see cref="IWorkingBeatmap.PrepareTrackForPreview"/> 配置；本类不负责主音乐轨。
    /// </summary>
    public partial class EzPreviewTrackManager : CompositeDrawable
    {
        public BindableBool EnabledBindable { get; } = new BindableBool();

        // 预览时，谱面中存在足够多的 beatmap note sample 才启用 hitsound 预览，避免把 1-5 个固定音效误判成 keysound。
        private const int hitsound_threshold = 10;
        private const double tick_ms = 8; // 调度间隔与事件触发容差（~120fps）
        private const double preload_lookahead_ms = 2000;
        private const int max_cached_beatmaps = 3; // LRU 缓存：最多保留最近 3 首谱面的调度表

        private readonly SampleSchedulerState sampleScheduler = new SampleSchedulerState();
        private readonly PlaybackState playback = new PlaybackState();

        private Track? currentTrack;
        private IWorkingBeatmap? currentBeatmap;
        private ScheduledDelegate? updateDelegate;
        private readonly Queue<Action> pendingPreloadActions = new Queue<Action>();
        private int prepareGeneration;

        // LRU 缓存：按 BeatmapID 缓存调度数据，避免跨难度复用。
        private readonly LinkedList<string> beatmapAccessOrder = new LinkedList<string>();
        private readonly Dictionary<string, BeatmapSampleCache> sampleCache = new Dictionary<string, BeatmapSampleCache>();

        /// <summary>
        /// 覆盖预览起点时间（毫秒）。若为 null，则使用谱面元数据的 PreviewTime。
        /// 供未来 Mod / 编辑器场景使用；SongSelect 默认不设置。
        /// </summary>
        public double? OverridePreviewStartTime { get; set; }

        private bool previewMainAudioAvailable;
        private bool previewHitSoundsEnabled;
        private bool previewStoryboardEnabled;

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

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// 为指定谱面启动预览。若命中音效数量低于阈值且无 storyboard 样本，返回 false（仅 BGM）。
        /// </summary>
        public bool StartPreview(IWorkingBeatmap beatmap)
        {
            if (!EnabledBindable.Value)
                return false;

            StopPreview();
            currentBeatmap = beatmap;
            currentTrack = prepareTrack(beatmap);
            previewMainAudioAvailable = currentTrack is not null and not TrackVirtual;

            if (currentTrack == null)
                Logger.Log("EzPreviewTrackManager: currentTrack is null (falling back?)", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

            playback.ResetPlaybackProgress();

            var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);
            previewHitSoundsEnabled = fastCheckShouldPreviewHitSounds(playableBeatmap, hitsound_threshold);
            previewStoryboardEnabled = hasStoryboardSamples(beatmap.Storyboard);

            if (!previewHitSoundsEnabled && !previewStoryboardEnabled)
                return false;

            startEnhancedPreview(beatmap, playableBeatmap);
            return true;
        }

        public void StopPreview() => stopPreviewInternal();

        private void stopPreviewInternal()
        {
            playback.IsPlaying = false;
            updateDelegate?.Cancel();
            updateDelegate = null;

            prepareGeneration++;
            clearPendingPreloadActions();

            stopActiveChannels();
            saveCurrentBeatmapToCache();

            currentBeatmap = null;
            currentTrack = null;
            previewMainAudioAvailable = false;
            previewHitSoundsEnabled = false;
            previewStoryboardEnabled = false;
            playback.ResetPlaybackProgress();
        }

        private bool fastCheckShouldPreviewHitSounds(IBeatmap beatmap, int threshold)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var obj in beatmap.HitObjects)
            {
                var stack = new Stack<HitObject>();
                stack.Push(obj);

                while (stack.Count > 0)
                {
                    var ho = stack.Pop();

                    foreach (var sm in ho.Samples)
                    {
                        if (!sm.UseBeatmapSamples)
                            continue;

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

        private void startEnhancedPreview(IWorkingBeatmap beatmap, IBeatmap playableBeatmap)
        {
            try
            {
                playback.PreviewStartTime = OverridePreviewStartTime ?? beatmap.BeatmapInfo.Metadata.PreviewTime;

                if (playback.PreviewStartTime < 0 || playback.PreviewStartTime > (currentTrack?.Length ?? 0))
                {
                    playback.PreviewStartTime = previewMainAudioAvailable
                        ? (currentTrack?.Length ?? 0) * 0.4
                        : 0;
                }

                double trackTimelineEndTime = currentTrack?.Length ?? 0;
                double trackLoopLength = Math.Max(1, trackTimelineEndTime - playback.PreviewStartTime);
                double scheduleWindowStart = playback.PreviewStartTime - tick_ms;
                double longestHitTimeEstimate = playableBeatmap.GetLastObjectTime();
                double longestStoryboardTimeEstimate = beatmap.Storyboard?.LatestEventTime ?? 0;
                double scheduleWindowEnd = Math.Max(
                    Math.Max(playback.PreviewStartTime + trackLoopLength, longestHitTimeEstimate),
                    longestStoryboardTimeEstimate) + tick_ms;

                bool cacheHit = restoreFromCache(beatmap);

                if (!cacheHit)
                {
                    if (previewHitSoundsEnabled)
                    {
                        prepareHitSounds(playableBeatmap, scheduleWindowStart, scheduleWindowEnd);
                        sampleScheduler.LongestHitTime = longestHitTimeEstimate;
                    }
                    else
                    {
                        sampleScheduler.ScheduledHitSounds.Clear();
                        sampleScheduler.LongestHitTime = 0;
                    }

                    if (previewStoryboardEnabled)
                    {
                        prepareStoryboardSamples(beatmap.Storyboard, scheduleWindowStart, scheduleWindowEnd);
                        sampleScheduler.LongestStoryboardTime = longestStoryboardTimeEstimate;
                    }
                    else
                    {
                        sampleScheduler.ScheduledStoryboardSamples.Clear();
                        sampleScheduler.LongestStoryboardTime = 0;
                    }
                }

                resetScheduledTriggers();

                double longestEventTime = Math.Max(
                    previewHitSoundsEnabled ? sampleScheduler.LongestHitTime : 0,
                    previewStoryboardEnabled ? sampleScheduler.LongestStoryboardTime : 0);

                double mainAudioEndTime = previewMainAudioAvailable ? trackTimelineEndTime : 0;

                playback.PreviewEndTime = Math.Max(trackTimelineEndTime, longestEventTime);

                if (playback.PreviewEndTime <= playback.PreviewStartTime)
                    playback.PreviewEndTime = Math.Max(trackTimelineEndTime, playback.PreviewStartTime + 1);

                playback.TrackLoopLength = Math.Max(1, trackTimelineEndTime - playback.PreviewStartTime);
                playback.ShortBgmOneShotMode = previewMainAudioAvailable && mainAudioEndTime + tick_ms < playback.PreviewEndTime;
                playback.ResetPlaybackProgress();
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

                playback.IsPlaying = true;
                updateDelegate = Scheduler.AddDelayed(updateSamples, tick_ms, true);
                updateSamples();
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: startEnhancedPreview error: {ex}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                clearEnhancedElements();
            }
        }

        private void prepareHitSounds(IBeatmap beatmap, double windowStart, double windowEnd)
        {
            sampleScheduler.ScheduledHitSounds.Clear();

            foreach (var ho in beatmap.HitObjects)
                schedule(ho);

            sampleScheduler.ScheduledHitSounds.Sort((a, b) => a.Time.CompareTo(b.Time));

            void schedule(HitObject ho)
            {
                if (ho.Samples.Any() && ho.StartTime >= windowStart && ho.StartTime <= windowEnd)
                {
                    sampleScheduler.ScheduledHitSounds.Add(new ScheduledHitSound
                    {
                        Time = ho.StartTime,
                        Samples = ho.Samples.ToArray(),
                        HasTriggered = false
                    });
                }

                foreach (var n in ho.NestedHitObjects)
                    schedule(n);
            }
        }

        private void prepareStoryboardSamples(Storyboard? storyboard, double windowStart, double windowEnd)
        {
            sampleScheduler.ScheduledStoryboardSamples.Clear();

            if (storyboard?.Layers == null)
                return;

            foreach (var layer in storyboard.Layers)
            {
                foreach (var element in layer.Elements)
                {
                    if (element is StoryboardSampleInfo s && s.StartTime >= windowStart && s.StartTime <= windowEnd)
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

        private void clearPendingPreloadActions()
        {
            pendingPreloadActions.Clear();
            sampleScheduler.PreloadQueuedKeys.Clear();
        }

        private void enqueueLookaheadPreload(double logicalTime)
        {
            double windowEnd = logicalTime + preload_lookahead_ms;
            int generation = prepareGeneration;

            if (previewHitSoundsEnabled)
            {
                int index = findNextValidIndex(
                    sampleScheduler.ScheduledHitSounds,
                    sampleScheduler.NextHitSoundIndex,
                    logicalTime - tick_ms);

                while (index < sampleScheduler.ScheduledHitSounds.Count)
                {
                    var hs = sampleScheduler.ScheduledHitSounds[index];

                    if (hs.Time > windowEnd)
                        break;

                    foreach (var info in hs.Samples)
                    {
                        if (!info.UseBeatmapSamples)
                            continue;

                        string? key = getHitSampleCacheKey(info);

                        if (string.IsNullOrEmpty(key))
                            continue;

                        if (sampleScheduler.HitSampleCache.ContainsKey(key) || !sampleScheduler.PreloadQueuedKeys.Add(key))
                            continue;

                        var captured = info;
                        pendingPreloadActions.Enqueue(() =>
                        {
                            if (generation == prepareGeneration)
                                resolveHitSample(captured);
                        });
                    }

                    index++;
                }
            }

            if (previewStoryboardEnabled)
            {
                int index = findNextValidIndex(
                    sampleScheduler.ScheduledStoryboardSamples,
                    sampleScheduler.NextStoryboardSampleIndex,
                    logicalTime - tick_ms);

                while (index < sampleScheduler.ScheduledStoryboardSamples.Count)
                {
                    var sb = sampleScheduler.ScheduledStoryboardSamples[index];

                    if (sb.Time > windowEnd)
                        break;

                    string key = sb.Sample.Path.Replace('\\', '/');

                    if (sampleScheduler.StoryboardSampleCache.ContainsKey(key) || !sampleScheduler.PreloadQueuedKeys.Add(key))
                    {
                        index++;
                        continue;
                    }

                    var captured = sb.Sample;
                    pendingPreloadActions.Enqueue(() =>
                    {
                        if (generation == prepareGeneration)
                            resolveStoryboardSample(captured);
                    });

                    index++;
                }
            }
        }

        private void processPreloadQueue()
        {
            if (pendingPreloadActions.Count == 0)
                return;

            try
            {
                int count = 0;

                while (count < 3 && pendingPreloadActions.Count > 0)
                {
                    pendingPreloadActions.Dequeue().Invoke();
                    count++;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: Preload error {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                clearPendingPreloadActions();
            }
        }

        private void updateSamples()
        {
            if (!playback.IsPlaying || currentTrack == null)
                return;

            double physicalTime = currentTrack.CurrentTime;

            if (!currentTrack.IsRunning)
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

            double schedulerTime = Time.Current;
            double elapsed = Math.Max(0, schedulerTime - playback.LastLogicalClockTime);

            playback.LastLogicalClockTime = schedulerTime;
            playback.LogicalClockTime += elapsed;

            double logicalTime;

            if (previewMainAudioAvailable)
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

                double trackTime = playback.PreviewStartTime + playback.TrackLoopCount * playback.TrackLoopLength
                                                             + Math.Max(0, physicalTime - playback.PreviewStartTime);

                logicalTime = Math.Max(trackTime, playback.LogicalClockTime);
            }
            else
            {
                logicalTime = playback.LogicalClockTime;
            }

            if (logicalTime > playback.PreviewEndTime + tick_ms)
            {
                restartPreviewCycle();
                return;
            }

            enqueueLookaheadPreload(logicalTime);
            processPreloadQueue();

            if (previewHitSoundsEnabled)
                processScheduledEvents(sampleScheduler.ScheduledHitSounds, ref sampleScheduler.NextHitSoundIndex, logicalTime, triggerHitSound);

            if (previewStoryboardEnabled)
                processScheduledEvents(sampleScheduler.ScheduledStoryboardSamples, ref sampleScheduler.NextStoryboardSampleIndex, logicalTime, triggerStoryboardSample);

            cleanupInactiveChannels();
            playback.LastTrackTime = physicalTime;
        }

        private void processScheduledEvents<T>(List<T> list, ref int nextIndex, double logicalTime, Action<T> trigger)
            where T : struct, ITimedScheduleEntry
        {
            nextIndex = findNextValidIndex(list, nextIndex, logicalTime - tick_ms);

            while (nextIndex < list.Count)
            {
                var entry = list[nextIndex];

                if (entry.HasTriggered)
                {
                    nextIndex++;
                    continue;
                }

                if (entry.Time > logicalTime + tick_ms)
                    break;

                if (Math.Abs(entry.Time - logicalTime) <= tick_ms)
                {
                    trigger(entry);
                    entry.HasTriggered = true;
                    list[nextIndex] = entry;
                    nextIndex++;
                }
                else if (entry.Time < logicalTime - tick_ms)
                {
                    entry.HasTriggered = true;
                    list[nextIndex] = entry;
                    nextIndex++;
                }
                else
                {
                    break;
                }
            }
        }

        private void cleanupInactiveChannels()
        {
            for (int i = sampleScheduler.ActiveChannels.Count - 1; i >= 0; i--)
            {
                var channel = sampleScheduler.ActiveChannels[i];

                if (channel.Playing)
                    continue;

                if (sampleScheduler.ActiveChannelStartTimes.TryGetValue(channel, out double startedAt)
                    && Time.Current - startedAt < tick_ms + 2)
                {
                    continue;
                }

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
                sampleScheduler.ActiveChannelStartTimes.Remove(channel);
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
            sampleScheduler.ActiveChannelStartTimes.Clear();
        }

        private void resetScheduledTriggers(bool stopChannels = false)
        {
            if (stopChannels)
                stopActiveChannels();

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

            resetScheduledTriggers(stopChannels: true);
            sampleScheduler.ResetIndices(playback.PreviewStartTime);
            playback.ResetPlaybackProgress();
            playback.ResetLogicalClock(playback.PreviewStartTime, Time.Current);

            currentTrack.Volume.Value = 1f;
            currentTrack.Seek(playback.PreviewStartTime);
        }

        private void triggerHitSound(ScheduledHitSound scheduled)
        {
            if (scheduled.Samples.Length == 0)
                return;

            try
            {
                foreach (var info in scheduled.Samples)
                {
                    var sample = resolveHitSample(info);

                    if (sample == null)
                        continue;

                    var channel = sample.GetChannel();

                    if (info.Volume > 0)
                    {
                        double vol = Math.Clamp(info.Volume / 100.0, 0, 1);
                        channel.Volume.Value = (float)vol;
                    }

                    channel.Play();
                    sampleScheduler.ActiveChannels.Add(channel);
                    sampleScheduler.ActiveChannelStartTimes[channel] = Time.Current;
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerHitSound error: {ex}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        private static string? getHitSampleCacheKey(HitSampleInfo info)
        {
            if (!info.UseBeatmapSamples)
                return null;

            return info.LookupNames.FirstOrDefault() ?? info.Name;
        }

        private ISample? resolveHitSample(HitSampleInfo info)
        {
            string? key = getHitSampleCacheKey(info);

            if (string.IsNullOrEmpty(key))
                return null;

            if (sampleScheduler.HitSampleCache.TryGetValue(key, out var cached))
                return cached;

            ISample? sample = currentBeatmap?.Skin.GetSample(info);
            sampleScheduler.HitSampleCache[key] = sample;
            return sample;
        }

        private void triggerStoryboardSample(ScheduledStoryboardSample scheduled) => triggerStoryboardSample(scheduled.Sample);

        private void triggerStoryboardSample(StoryboardSampleInfo sampleInfo)
        {
            try
            {
                var sample = resolveStoryboardSample(sampleInfo);

                if (sample == null)
                    return;

                var channel = sample.GetChannel();

                if (sampleInfo.Volume > 0)
                {
                    double vol = Math.Clamp(sampleInfo.Volume / 100.0, 0, 1);
                    channel.Volume.Value = (float)vol;
                }

                channel.Play();
                sampleScheduler.ActiveChannels.Add(channel);
                sampleScheduler.ActiveChannelStartTimes[channel] = Time.Current;
            }
            catch (Exception ex)
            {
                Logger.Log($"EzPreviewTrackManager: triggerStoryboardSample error: {ex}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        private ISample? resolveStoryboardSample(StoryboardSampleInfo info)
        {
            string normalizedPath = info.Path.Replace('\\', '/');

            if (sampleScheduler.StoryboardSampleCache.TryGetValue(normalizedPath, out var cached))
                return cached;

            ISample? sample = currentBeatmap?.Skin.GetSample(info);
            sampleScheduler.StoryboardSampleCache[normalizedPath] = sample;
            return sample;
        }

        private void clearEnhancedElements()
        {
            stopActiveChannels();
            sampleScheduler.Reset();
            playback.ShortBgmOneShotMode = false;
            playback.ShortBgmMutedAfterFirstLoop = false;
        }

        private void saveCurrentBeatmapToCache()
        {
            if (currentBeatmap == null)
                return;

            string? beatmapCacheKey = getBeatmapCacheKey(currentBeatmap.BeatmapInfo);

            if (string.IsNullOrEmpty(beatmapCacheKey))
                return;

            if (!sampleCache.TryGetValue(beatmapCacheKey, out var cache))
            {
                cache = new BeatmapSampleCache();
                sampleCache[beatmapCacheKey] = cache;
            }

            cache.ScheduledHitSounds = sampleScheduler.ScheduledHitSounds.ToArray();
            cache.ScheduledStoryboardSamples = sampleScheduler.ScheduledStoryboardSamples.ToArray();
            cache.LongestHitTime = sampleScheduler.LongestHitTime;
            cache.LongestStoryboardTime = sampleScheduler.LongestStoryboardTime;

            beatmapAccessOrder.Remove(beatmapCacheKey);
            beatmapAccessOrder.AddFirst(beatmapCacheKey);

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

        private bool restoreFromCache(IWorkingBeatmap beatmap)
        {
            string? beatmapCacheKey = getBeatmapCacheKey(beatmap.BeatmapInfo);

            if (string.IsNullOrEmpty(beatmapCacheKey))
                return false;

            if (!sampleCache.TryGetValue(beatmapCacheKey, out var cache))
                return false;

            sampleScheduler.ScheduledHitSounds.Clear();
            sampleScheduler.ScheduledHitSounds.AddRange(cache.ScheduledHitSounds);

            sampleScheduler.ScheduledStoryboardSamples.Clear();
            sampleScheduler.ScheduledStoryboardSamples.AddRange(cache.ScheduledStoryboardSamples);

            sampleScheduler.LongestHitTime = cache.LongestHitTime;
            sampleScheduler.LongestStoryboardTime = cache.LongestStoryboardTime;

            beatmapAccessOrder.Remove(beatmapCacheKey);
            beatmapAccessOrder.AddFirst(beatmapCacheKey);

            return true;
        }

        private static string? getBeatmapCacheKey(IBeatmapInfo? beatmapInfo)
        {
            if (beatmapInfo == null)
                return null;

            if (beatmapInfo is BeatmapInfo localBeatmap && localBeatmap.ID != Guid.Empty)
                return localBeatmap.ID.ToString();

            if (!string.IsNullOrWhiteSpace(beatmapInfo.MD5Hash))
                return beatmapInfo.MD5Hash;

            return beatmapInfo.GetDisplayTitle();
        }

        private interface ITimedScheduleEntry
        {
            double Time { get; }
            bool HasTriggered { get; set; }
        }

        private struct ScheduledHitSound : ITimedScheduleEntry
        {
            public double Time;
            public HitSampleInfo[] Samples;
            public bool HasTriggered;

            double ITimedScheduleEntry.Time => Time;

            bool ITimedScheduleEntry.HasTriggered
            {
                get => HasTriggered;
                set => HasTriggered = value;
            }
        }

        private struct ScheduledStoryboardSample : ITimedScheduleEntry
        {
            public double Time;
            public StoryboardSampleInfo Sample;
            public bool HasTriggered;

            double ITimedScheduleEntry.Time => Time;

            bool ITimedScheduleEntry.HasTriggered
            {
                get => HasTriggered;
                set => HasTriggered = value;
            }
        }

        private static int findNextValidIndex<T>(List<T> list, int startIndex, double minTime)
            where T : ITimedScheduleEntry
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
            public readonly Dictionary<string, ISample?> HitSampleCache = new Dictionary<string, ISample?>();
            public readonly Dictionary<string, ISample?> StoryboardSampleCache = new Dictionary<string, ISample?>();
            public readonly HashSet<string> PreloadQueuedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<SampleChannel> ActiveChannels = new List<SampleChannel>();
            public readonly Dictionary<SampleChannel, double> ActiveChannelStartTimes = new Dictionary<SampleChannel, double>();

            public int NextHitSoundIndex;
            public int NextStoryboardSampleIndex;

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
                ActiveChannelStartTimes.Clear();
                ScheduledHitSounds.Clear();
                ScheduledStoryboardSamples.Clear();
                HitSampleCache.Clear();
                StoryboardSampleCache.Clear();
                PreloadQueuedKeys.Clear();
                LongestHitTime = 0;
                LongestStoryboardTime = 0;
                ResetIndices(0);
            }
        }

        private sealed class BeatmapSampleCache
        {
            public ScheduledHitSound[] ScheduledHitSounds { get; set; } = Array.Empty<ScheduledHitSound>();
            public ScheduledStoryboardSample[] ScheduledStoryboardSamples { get; set; } = Array.Empty<ScheduledStoryboardSample>();
            public double LongestHitTime { get; set; }
            public double LongestStoryboardTime { get; set; }
        }

        private Track? prepareTrack(IWorkingBeatmap beatmap)
        {
            var beatmapTrack = beatmap.Track;

            if (beatmapTrack is TrackVirtual)
                beatmapTrack.Length = getVirtualTimelineLength(beatmap);

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

            double beatLength = beatmap.Beatmap.ControlPointInfo.TimingPointAt(lastRelevantTime).BeatLength;

            if (double.IsNaN(beatLength) || double.IsInfinity(beatLength) || beatLength <= 0)
                beatLength = 1000;

            double tailPadding = Math.Max(beatLength * 4, 1000);
            return Math.Max(lastRelevantTime + tailPadding, previewTime + tailPadding);
        }
    }
}
