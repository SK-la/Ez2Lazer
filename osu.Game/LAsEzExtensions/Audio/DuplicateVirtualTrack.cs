// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
        public static bool DuplicateEnabled { get; set; }

        private IApplyToLoopPlay? overrideProvider;

        private bool startRequested;
        private bool started;
        private IWorkingBeatmap? pendingBeatmap;

        private BindableDouble? beatmapTrackMuteAdjustment;
        private Track? mutedOriginalTrack;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        [Resolved(canBeNull: true)]
        private BeatmapManager? beatmapManager { get; set; }

        public void SetOverrideProvider(IApplyToLoopPlay provider)
        {
            overrideProvider = provider;
        }

        public void StartPreview(IWorkingBeatmap beatmap)
        {
            if (!DuplicateEnabled || overrideProvider == null)
            {
                return;
            }

            pendingBeatmap = beatmap;
            startRequested = true;

            var overrides = overrideProvider.GetOverrides(beatmap);
            ApplyOverrides(overrides);

            OverrideLooping = overrides.ForceLooping;
            ExternalClock = gameplayClock;
            ExternalClockStartTime = overrides.StartTime ?? OverridePreviewStartTime;
            EnableHitSounds = overrides.EnableHitSounds;

            // 重置循环状态
            ResetLoopState();

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
                    catch
                    {
                    }

                    beatmapTrackMuteAdjustment = new BindableDouble(0);
                    beatmap.Track.AddAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);
                    mutedOriginalTrack = beatmap.Track;
                }
            }

            // 不直接开播：等待本 Drawable 完成依赖注入，并在 gameplay 时钟 running 时再开始。
            //（选歌界面无 gameplayClock，则下一帧启动即可）
            started = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            // 尝试移除之前添加的音量调整，确保不会在 Dispose 后仍保持静音。
            try
            {
                if (beatmapTrackMuteAdjustment != null && mutedOriginalTrack != null)
                    mutedOriginalTrack.RemoveAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);
            }
            catch
            {
            }

            beatmapTrackMuteAdjustment = null;
            mutedOriginalTrack = null;

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

            return acquireIndependentTrack(beatmap, out ownsTrack) ?? beatmap.Track;
        }

        private Track? acquireIndependentTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
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

            Track?[] candidates = new[]
            {
                hasBeatmapTrackStore && !string.IsNullOrEmpty(rawFileStorePath) ? beatmapManager!.BeatmapTrackStore.Get(rawFileStorePath) : null,
                hasBeatmapTrackStore && !string.IsNullOrEmpty(standardisedFileStorePath) ? beatmapManager!.BeatmapTrackStore.Get(standardisedFileStorePath) : null,
                !string.IsNullOrEmpty(rawFileStorePath) ? AudioManager.Tracks.Get(rawFileStorePath) : null,
                !string.IsNullOrEmpty(standardisedFileStorePath) ? AudioManager.Tracks.Get(standardisedFileStorePath) : null,
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

            // 恢复之前添加的音量调整（如果存在）
            try
            {
                if (beatmapTrackMuteAdjustment != null && mutedOriginalTrack != null)
                    mutedOriginalTrack.RemoveAdjustment(AdjustableProperty.Volume, beatmapTrackMuteAdjustment);
            }
            catch
            {
            }

            beatmapTrackMuteAdjustment = null;
            mutedOriginalTrack = null;

            base.StopPreviewInternal(reason);

            // 重置 DuplicateVirtualTrack 特有的状态
            startRequested = false;
            started = false;
            pendingBeatmap = null;
        }
    }
}
