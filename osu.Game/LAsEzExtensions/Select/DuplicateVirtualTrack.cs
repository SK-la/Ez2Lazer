// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Select
{
    public partial class DuplicateVirtualTrack : EzPreviewTrackManager
    {
        public IPreviewOverrideProvider? OverrideProvider { get; set; }
        public PreviewOverrideSettings? PendingOverrides { get; set; }

        private bool startRequested;
        private bool started;
        private IWorkingBeatmap? pendingBeatmap;

        private double? beatmapTrackVolumeBeforeMute;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        [Resolved(canBeNull: true)]
        private BeatmapManager? beatmapManager { get; set; }

        public new void StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            pendingBeatmap = beatmap;
            startRequested = true;

            var overrides = PendingOverrides ?? OverrideProvider?.GetPreviewOverrides(beatmap);

            if (overrides != null)
                ApplyOverrides(overrides);

            OverrideLooping = overrides?.ForceLooping ?? OverrideLooping;
            ExternalClock = gameplayClock;
            ExternalClockStartTime = overrides?.PreviewStart ?? OverridePreviewStartTime;
            EnableHitSounds = overrides?.EnableHitSounds ?? true;

            // gameplay 下不要把 MasterGameplayClockContainer 从真实 beatmap.Track “断开”。
            // 断开会导致：
            // 1) 变速 Mod（HT/DT/RateAdjust）对 gameplay 时钟不生效（TrackVirtual 不一定按 Tempo/Frequency 推进时间）。
            // 2) SubmittingPlayer 的播放校验会持续报 "System audio playback is not working"。
            // 这里改为：保留 beatmap.Track 作为时钟来源，但将其静音，避免听到整首歌。
            if (gameplayClock != null && beatmap.Track != null)
            {
                beatmapTrackVolumeBeforeMute ??= beatmap.Track.Volume.Value;
                beatmap.Track.Volume.Value = 0;
            }

            // 不直接开播：等待本 Drawable 完成依赖注入，并在 gameplay 时钟 running 时再开始。
            //（选歌界面无 gameplayClock，则下一帧启动即可）
            started = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            // 尽可能恢复 beatmap.Track 的音量，避免退出/切换后一直静音。
            if (pendingBeatmap?.Track != null && beatmapTrackVolumeBeforeMute != null)
                pendingBeatmap.Track.Volume.Value = beatmapTrackVolumeBeforeMute.Value;

            base.Dispose(isDisposing);
        }

        protected override void UpdateAfterChildren()
        {
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

            // 由 EzPreviewTrackManager 负责外部时钟驱动的切片/循环/间隔（Seek/Stop/Start）。
            // 在 gameplay 场景下必须使用独立 Track 实例：
            // - 原 beatmap.Track 可能仍在播放整首歌，需要 Stop()
            // - 同时也避免对原 Track 的音量/调整影响到切片播放。
            if (gameplayClock != null)
            {
                string audioFile = beatmap.BeatmapInfo.Metadata.AudioFile;

                if (!string.IsNullOrEmpty(audioFile) && beatmap.BeatmapInfo.BeatmapSet is BeatmapSetInfo beatmapSet)
                {
                    string? rawFileStorePath = beatmapSet.GetPathForFile(audioFile);
                    string? standardisedFileStorePath = rawFileStorePath;

                    // 部分存储 API 可能返回 Windows 风格的路径分隔符。
                    if (!string.IsNullOrEmpty(standardisedFileStorePath))
                        standardisedFileStorePath = standardisedFileStorePath.ToStandardisedPath();

                    if (!string.IsNullOrEmpty(rawFileStorePath) || !string.IsNullOrEmpty(standardisedFileStorePath))
                    {
                        // 为了兼容性，raw/standardised 两种路径都尝试。
                        // 优先选择看起来“已正确解码”的 Track（通常 Length > 0）。
                        static bool isProbablyValidTrack(Track? t) => t != null && t.Length > 0;

                        static void ensureTrackLengthPopulated(Track track)
                        {
                            if (!track.IsLoaded || track.Length == 0)
                            {
                                // 强制填充 Length（参考 WorkingBeatmap.PrepareTrackForPreview() 的处理）。
                                track.Seek(track.CurrentTime);
                            }
                        }

                        bool hasBeatmapTrackStore = beatmapManager?.BeatmapTrackStore != null;

                        Track? beatmapStoreRaw = hasBeatmapTrackStore && !string.IsNullOrEmpty(rawFileStorePath)
                            ? beatmapManager!.BeatmapTrackStore.Get(rawFileStorePath)
                            : null;

                        Track? beatmapStoreStandardised = hasBeatmapTrackStore && !string.IsNullOrEmpty(standardisedFileStorePath)
                            ? beatmapManager!.BeatmapTrackStore.Get(standardisedFileStorePath)
                            : null;

                        Track? globalStoreRaw = !string.IsNullOrEmpty(rawFileStorePath)
                            ? AudioManager.Tracks.Get(rawFileStorePath)
                            : null;

                        Track? globalStoreStandardised = !string.IsNullOrEmpty(standardisedFileStorePath)
                            ? AudioManager.Tracks.Get(standardisedFileStorePath)
                            : null;
                        Track? newTrack;

                        if (isProbablyValidTrack(beatmapStoreRaw))
                        {
                            newTrack = beatmapStoreRaw;
                        }
                        else if (isProbablyValidTrack(beatmapStoreStandardised))
                        {
                            newTrack = beatmapStoreStandardised;
                        }
                        else if (isProbablyValidTrack(globalStoreRaw))
                        {
                            newTrack = globalStoreRaw;
                        }
                        else if (isProbablyValidTrack(globalStoreStandardised))
                        {
                            newTrack = globalStoreStandardised;
                        }
                        else
                        {
                            // 可能尚未完成懒加载：按优先级顺序回退选择即可。
                            newTrack = beatmapStoreRaw ?? beatmapStoreStandardised ?? globalStoreRaw ?? globalStoreStandardised;
                        }

                        if (newTrack != null)
                        {
                            ensureTrackLengthPopulated(newTrack);

                            // 重要：游戏内的变速 Mod（DT/HT/RateAdjust 等）会把调整应用到
                            // GameplayClockContainer.AdjustmentsFromMods 上，并由 MasterGameplayClockContainer 绑定到主音轨。
                            // 但在 gameplay 场景下 DuplicateVirtualTrack 使用的是“独立的 Track 实例”，
                            // 所以这里必须把同一套 adjustments 绑定到新 Track，确保音频变速与游戏时钟/下落判定保持一致。
                            if (gameplayClockContainer != null)
                                newTrack.BindAdjustments(gameplayClockContainer.AdjustmentsFromMods);

                            // 同步用户的播放速率调整（若存在）。
                            if (gameplayClockContainer is MasterGameplayClockContainer master)
                                newTrack.AddAdjustment(AdjustableProperty.Frequency, master.UserPlaybackRate);

                            ownsTrack = true;
                            return newTrack;
                        }
                    }
                }
            }

            return beatmap.Track;
        }
    }
}
