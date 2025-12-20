using System;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Screens.Play;
using osu.Framework.Timing;
using osu.Framework.Allocation;
using osu.Game.LAsEzExtensions.Select;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class DuplicateVirtualTrack : EzPreviewTrackManager
    {
        private readonly WorkingBeatmap workingBeatmap;
        private readonly ManiaModDuplicate mod;
        private float? originalGameplayTrackVolume;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        public DuplicateVirtualTrack(WorkingBeatmap workingBeatmap, ManiaModDuplicate mod)
        {
            this.workingBeatmap = workingBeatmap;
            this.mod = mod;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (mod.ResolvedSegmentLength <= 0 || mod.ResolvedCutTimeStart is null)
                return;

            if (workingBeatmap.Track != null)
            {
                originalGameplayTrackVolume = (float)workingBeatmap.Track.Volume.Value;
                workingBeatmap.Track.Volume.Value = 0f; // 静音原始游戏音轨，仅在游戏内生效
            }

            OverridePreviewStartTime = mod.ResolvedCutTimeStart;
            OverridePreviewDuration = mod.ResolvedSegmentLength;
            OverrideLoopCount = mod.Time.Value;
            OverrideLoopInterval = mod.BreakTime.Value * 1000;
            OverrideLooping = true; // we handle looping manually when interval > 0
            ExternalClock = gameplayClock;

            EnableHitSounds = false; // mod 下不需要在虚拟音轨里重复敲击音效

            StartPreview(workingBeatmap);
        }

        protected override Track? CreateTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;
            string? audioFile = workingBeatmap.BeatmapInfo?.Metadata?.AudioFile;
            if (string.IsNullOrEmpty(audioFile))
                return beatmap.Track;

            ownsTrack = true;
            return AudioManager.Tracks.Get(workingBeatmap.BeatmapSetInfo.GetPathForFile(audioFile));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (originalGameplayTrackVolume.HasValue && workingBeatmap.Track != null)
                workingBeatmap.Track.Volume.Value = originalGameplayTrackVolume.Value;

            base.Dispose(isDisposing);
        }
    }
}
