// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Audio
{
    public partial class DuplicateVirtualTrack : EzPreviewTrackManager
    {
        /// <summary>
        /// 通过外部Mods检查接口控制开关，默认关闭。
        /// </summary>
        public static bool DuplicateEnabled { get; set; } = false;

        public IApplyToLoopPlay? OverrideProvider { get; set; }
        public PreviewOverrideSettings? PendingOverrides { get; set; }

        private bool startRequested;
        private bool started;
        private IWorkingBeatmap? pendingBeatmap;

        private double? beatmapTrackVolumeBeforeMute;
        private Track? mutedOriginalTrack;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        [Resolved(canBeNull: true)]
        private BeatmapManager? beatmapManager { get; set; }

        public new void StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            if (!DuplicateEnabled)
            {
                return;
            }

            pendingBeatmap = beatmap;
            startRequested = true;

            var overrides = PendingOverrides ?? OverrideProvider?.GetPreviewOverrides(beatmap);

            if (overrides != null)
                ApplyOverrides(overrides);

            OverrideLooping = overrides?.ForceLooping ?? OverrideLooping;
            ExternalClock = gameplayClock;
            ExternalClockStartTime = overrides?.StartTime ?? OverridePreviewStartTime;
            EnableHitSounds = overrides?.EnableHitSounds ?? true;

            // 重置循环状态
            ResetLoopState();

            // gameplay 下不要把 MasterGameplayClockContainer 从真实 beatmap.Track “断开”。
            // 断开会导致：
            // 1) 变速 Mod（HT/DT/RateAdjust）对 gameplay 时钟不生效（TrackVirtual 不一定按 Tempo/Frequency 推进时间）。
            // 2) SubmittingPlayer 的播放校验会持续报 "System audio playback is not working"。
            // 这里改为：保留 beatmap.Track 作为时钟来源，但将其静音，避免听到整首歌。
            if (gameplayClock != null && beatmap.Track != null)
            {
                // 保存被静音的 Track 实例以及其原始音量，确保后续能正确恢复。
                if (mutedOriginalTrack == null || mutedOriginalTrack != beatmap.Track)
                {
                    beatmapTrackVolumeBeforeMute = beatmap.Track.Volume.Value;
                    mutedOriginalTrack = beatmap.Track;
                }

                beatmap.Track.Volume.Value = 0;
            }

            // 不直接开播：等待本 Drawable 完成依赖注入，并在 gameplay 时钟 running 时再开始。
            //（选歌界面无 gameplayClock，则下一帧启动即可）
            started = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            // 尽可能恢复被静音的原始 track 的音量，避免退出/切换后一直静音。
            if (mutedOriginalTrack != null && beatmapTrackVolumeBeforeMute != null)
            {
                mutedOriginalTrack.Volume.Value = beatmapTrackVolumeBeforeMute.Value;
                beatmapTrackVolumeBeforeMute = null;
                mutedOriginalTrack = null;
            }

            base.Dispose(isDisposing);
        }

        protected override void UpdateAfterChildren()
        {
            if (!DuplicateEnabled)
            {
                base.UpdateAfterChildren();
                return;
            }

            base.UpdateAfterChildren();

            if (started || !startRequested || pendingBeatmap == null)
                return;

            // 当有 gameplay 时钟且第一次进入 running 状态时再启动切片播放，避免准备时间被抢占。
            if (gameplayClock != null && !gameplayClock.IsRunning)
                return;

            started = true;
            base.StartPreview(pendingBeatmap, false);
        }

        protected override Track? CreateTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;

            if (!DuplicateEnabled)
                return beatmap.Track;

            // Only attempt independent track acquisition in gameplay context.
            if (gameplayClock == null)
                return beatmap.Track;

            return AcquireIndependentTrack(beatmap, out ownsTrack) ?? beatmap.Track;
        }

        private Track? AcquireIndependentTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
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

            Track?[] candidates = new Track?[]
            {
                hasBeatmapTrackStore && !string.IsNullOrEmpty(rawFileStorePath) ? beatmapManager!.BeatmapTrackStore.Get(rawFileStorePath) : null,
                hasBeatmapTrackStore && !string.IsNullOrEmpty(standardisedFileStorePath) ? beatmapManager!.BeatmapTrackStore.Get(standardisedFileStorePath) : null,
                !string.IsNullOrEmpty(rawFileStorePath) ? AudioManager.Tracks.Get(rawFileStorePath) : null,
                !string.IsNullOrEmpty(standardisedFileStorePath) ? AudioManager.Tracks.Get(standardisedFileStorePath) : null,
            };

            string[] candidateNames = new string[] { "beatmapStoreRaw", "beatmapStoreStandardised", "globalStoreRaw", "globalStoreStandardised" };

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

                    ownsTrack = true;
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

                ownsTrack = true;
                return t;
            }

            Logger.Log("DuplicateVirtualTrack: no candidate found, using beatmap.Track", LoggingTarget.Runtime);
            return null;
        }

        protected override void StopPreviewInternal(string reason)
        {
            if (!DuplicateEnabled)
            {
                base.StopPreviewInternal(reason);
                // also clear any pending state just in case
                startRequested = false;
                started = false;
                pendingBeatmap = null;
                return;
            }

            // 恢复被静音的原始 beatmap.Track 音量（如果我们在 StartPreview 时修改过）
            if (mutedOriginalTrack != null && beatmapTrackVolumeBeforeMute != null)
            {
                mutedOriginalTrack.Volume.Value = beatmapTrackVolumeBeforeMute.Value;
                beatmapTrackVolumeBeforeMute = null;
                mutedOriginalTrack = null;
            }

            base.StopPreviewInternal(reason);

            // 重置 DuplicateVirtualTrack 特有的状态
            startRequested = false;
            started = false;
            pendingBeatmap = null;
        }
    }
}
