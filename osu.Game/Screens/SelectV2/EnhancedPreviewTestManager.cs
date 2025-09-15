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
using osu.Game.Skinning;

namespace osu.Game.Screens.SelectV2
{
    /// <summary>
    /// 简化的测试版本，用于调试和验证功能
    /// </summary>
    public partial class EnhancedPreviewTestManager : CompositeDrawable
    {
        private const int hitsound_threshold = 5; // 降低阈值便于测试
        private const double update_frequency = 16; // 16ms 更稳定
        private const double timing_tolerance = 100; // 更宽松的时间容差

        private Track? currentTrack;
        private readonly List<ScheduledHitSound> scheduledHitSounds = new List<ScheduledHitSound>();

        private IWorkingBeatmap? currentBeatmap;
        private bool isPlaying;
        private double previewStartTime;
        private ScheduledDelegate? updateDelegate;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        private ISampleStore sampleStore = null!;

        public bool IsPlaying => isPlaying && currentTrack?.IsRunning == true;

        private struct ScheduledHitSound
        {
            public double Time;
            public HitSampleInfo[] Samples;
            public bool HasTriggered;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            sampleStore = audioManager.Samples;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };

            Logger.Log("EnhancedPreviewTestManager: Loaded successfully", LoggingTarget.Runtime);
        }

        public void StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            Logger.Log("EnhancedPreviewTestManager: Starting preview", LoggingTarget.Runtime);

            if (isPlaying && currentBeatmap == beatmap)
                return;

            StopPreview();

            currentBeatmap = beatmap;
            currentTrack = beatmap.Track;

            if (!forceEnhanced && !shouldUseEnhancedPreview(beatmap))
            {
                Logger.Log("EnhancedPreviewTestManager: Using standard preview", LoggingTarget.Runtime);
                startStandardPreview(beatmap);
                return;
            }

            Logger.Log("EnhancedPreviewTestManager: Using enhanced preview", LoggingTarget.Runtime);
            startEnhancedPreview(beatmap);
        }

        private bool shouldUseEnhancedPreview(IWorkingBeatmap beatmap)
        {
            try
            {
                var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

                // 简单计算：统计有音效的 HitObject 数量
                int hitsoundCount = 0;

                foreach (var hitObject in playableBeatmap.HitObjects.Take(100)) // 只检查前100个对象
                {
                    if (hitObject.Samples.Any())
                        hitsoundCount++;
                }

                Logger.Log($"EnhancedPreviewTestManager: Found {hitsoundCount} hit objects with sounds", LoggingTarget.Runtime);
                return hitsoundCount >= hitsound_threshold;
            }
            catch (Exception ex)
            {
                Logger.Log($"EnhancedPreviewTestManager: Error checking hitsounds: {ex.Message}", LoggingTarget.Runtime);
                return false;
            }
        }

        private void startStandardPreview(IWorkingBeatmap beatmap)
        {
            beatmap.PrepareTrackForPreview(true);
            previewStartTime = beatmap.BeatmapInfo.Metadata.PreviewTime;

            if (previewStartTime < 0 || previewStartTime > (currentTrack?.Length ?? 0))
                previewStartTime = (currentTrack?.Length ?? 0) * 0.4;

            currentTrack?.Seek(previewStartTime);
            currentTrack?.Start();
            isPlaying = true;

            Logger.Log($"EnhancedPreviewTestManager: Started standard preview at {previewStartTime}ms", LoggingTarget.Runtime);
        }

        private void startEnhancedPreview(IWorkingBeatmap beatmap)
        {
            try
            {
                var playableBeatmap = beatmap.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset);

                beatmap.PrepareTrackForPreview(true);
                previewStartTime = beatmap.BeatmapInfo.Metadata.PreviewTime;

                if (previewStartTime < 0 || previewStartTime > (currentTrack?.Length ?? 0))
                    previewStartTime = (currentTrack?.Length ?? 0) * 0.4;

                prepareHitSounds(playableBeatmap);

                currentTrack?.Seek(previewStartTime);
                currentTrack?.Start();
                isPlaying = true;

                updateDelegate = Scheduler.AddDelayed(updateSamples, update_frequency, true);

                Logger.Log($"EnhancedPreviewTestManager: Started enhanced preview with {scheduledHitSounds.Count} hitsounds", LoggingTarget.Runtime);
            }
            catch (Exception ex)
            {
                Logger.Log($"EnhancedPreviewTestManager: Enhanced preview failed: {ex.Message}", LoggingTarget.Runtime);
                startStandardPreview(beatmap);
            }
        }

        private void prepareHitSounds(IBeatmap beatmap)
        {
            scheduledHitSounds.Clear();

            double previewEndTime = previewStartTime + 30000;
            Logger.Log($"EnhancedPreviewTestManager: Preparing hitsounds from {previewStartTime}ms to {previewEndTime}ms", LoggingTarget.Runtime);

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject.StartTime >= previewStartTime &&
                    hitObject.StartTime <= previewEndTime &&
                    hitObject.Samples.Any())
                {
                    scheduledHitSounds.Add(new ScheduledHitSound
                    {
                        Time = hitObject.StartTime,
                        Samples = hitObject.Samples.ToArray(),
                        HasTriggered = false
                    });

                    Logger.Log($"EnhancedPreviewTestManager: Scheduled hitsound at {hitObject.StartTime}ms with {hitObject.Samples.Count} samples", LoggingTarget.Runtime);
                }
            }

            scheduledHitSounds.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        private void updateSamples()
        {
            if (!isPlaying || currentTrack == null) return;

            double currentTime = currentTrack.CurrentTime;

            for (int i = 0; i < scheduledHitSounds.Count; i++)
            {
                var hitSound = scheduledHitSounds[i];

                if (hitSound.HasTriggered) continue;

                if (hitSound.Time > currentTime + timing_tolerance) break;

                if (Math.Abs(hitSound.Time - currentTime) <= timing_tolerance)
                {
                    Logger.Log($"EnhancedPreviewTestManager: Triggering hitsound at {currentTime}ms (scheduled: {hitSound.Time}ms)", LoggingTarget.Runtime);
                    triggerHitSound(hitSound.Samples);
                    hitSound.HasTriggered = true;
                    scheduledHitSounds[i] = hitSound;
                }
            }
        }

        private void triggerHitSound(HitSampleInfo[] samples)
        {
            Schedule(() =>
            {
                foreach (var sampleInfo in samples)
                {
                    try
                    {
                        // 尝试最简单的方法：直接从默认位置获取音效
                        ISample? sample = null;

                        // 首先尝试标准的游戏音效
                        string[] standardSamples = { "normal-hitnormal", "soft-hitnormal", "drum-hitnormal" };

                        foreach (string sampleName in standardSamples)
                        {
                            sample = sampleStore.Get($"Gameplay/{sampleName}");
                            if (sample != null) break;
                        }

                        // 如果找不到，尝试皮肤
                        sample ??= skinSource.GetSample(sampleInfo);

                        if (sample != null)
                        {
                            var channel = sample.GetChannel();

                            channel.Volume.Value = 0.5; // 固定音量便于测试
                            channel.Play();
                            Logger.Log("EnhancedPreviewTestManager: Played hitsound successfully", LoggingTarget.Runtime);
                        }
                        else
                        {
                            Logger.Log("EnhancedPreviewTestManager: Could not load any hitsound", LoggingTarget.Runtime);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"EnhancedPreviewTestManager: Error playing hitsound: {ex.Message}", LoggingTarget.Runtime);
                    }

                    break; // 只播放第一个音效进行测试
                }
            });
        }

        public void StopPreview()
        {
            Logger.Log("EnhancedPreviewTestManager: Stopping preview", LoggingTarget.Runtime);

            isPlaying = false;
            updateDelegate?.Cancel();
            updateDelegate = null;

            currentTrack?.Stop();
            scheduledHitSounds.Clear();

            currentBeatmap = null;
            currentTrack = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            StopPreview();
            base.Dispose(isDisposing);
        }
    }
}
