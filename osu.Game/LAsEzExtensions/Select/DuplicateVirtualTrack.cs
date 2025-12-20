using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Game.Beatmaps;
using osu.Game.Screens.Play;
using osu.Framework.Timing;
using osu.Game.Rulesets.Mods;
using osu.Framework.Logging;

namespace osu.Game.LAsEzExtensions.Select
{
    public partial class DuplicateVirtualTrack : EzPreviewTrackManager
    {
        public IPreviewOverrideProvider? OverrideProvider { get; set; }
        public PreviewOverrideSettings? PendingOverrides { get; set; }

        private float? originalGameplayTrackVolume;
        private Track? originalGameplayTrack;
        private bool started;
        private IWorkingBeatmap? pendingBeatmap;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        public new void StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            pendingBeatmap = beatmap;

            var overrides = PendingOverrides ?? OverrideProvider?.GetPreviewOverrides(beatmap);

            if (overrides != null)
                ApplyOverrides(overrides);

            OverrideLooping = overrides?.ForceLooping ?? OverrideLooping;
            ExternalClock = gameplayClock;
            EnableHitSounds = overrides?.EnableHitSounds ?? true;

            // 不直接开播，等待游戏时钟启动信号再播；若无时钟则立即播（选歌界面）。
            if (gameplayClock != null)
            {
                started = false;
            }
            else
            {
                started = true;
                base.StartPreview(beatmap, forceEnhanced);
            }
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // 当有 gameplay 时钟且第一次进入 running 状态时再启动切片播放，避免准备时间被抢占。
            if (!started && gameplayClock != null)
            {
                if (gameplayClock.IsRunning)
                {
                    started = true;
                    if (pendingBeatmap != null)
                        base.StartPreview(pendingBeatmap, false);
                }
            }
        }

        protected override Track? CreateTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;
            Track? track = null;

            // 尝试从文件资源创建独立音轨实例。
            string audioFile = beatmap?.BeatmapInfo?.Metadata?.AudioFile ?? string.Empty;

            if (!string.IsNullOrEmpty(audioFile) && audioManager?.Tracks != null)
            {
                string? resourceName = beatmap?.Beatmap?.BeatmapInfo?.BeatmapSet?.GetPathForFile(audioFile) ?? audioFile;

                try
                {
                    track = audioManager.Tracks.Get(resourceName);
                    ownsTrack = track != null;
                }
                catch (Exception ex)
                {
                    Logger.Log($"DuplicateVirtualTrack: Tracks.Get failed for {resourceName}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                    // 无法加载真实音轨时，退回到虚拟轨（无音但不影响游戏时钟）；不再回退到 beatmap.Track。
                    track = audioManager?.Tracks.GetVirtual(beatmap?.BeatmapInfo?.Length ?? 0);
                    ownsTrack = false;
                }
            }

            // 仅在有游戏时钟（进入玩法）时静音原始音轨；选歌场景下不静音。
            if (gameplayClock != null && beatmap?.Track != null)
            {
                originalGameplayTrack = beatmap.Track;
                originalGameplayTrackVolume = (float)beatmap.Track.Volume.Value;
                beatmap.Track.Volume.Value = 0f;
            }

            return track;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (originalGameplayTrackVolume.HasValue && originalGameplayTrack != null)
                originalGameplayTrack.Volume.Value = originalGameplayTrackVolume.Value;

            base.Dispose(isDisposing);
        }
    }
}
