// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio.Track;
using osu.Game.Audio;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Audio
{
    /// <summary>
    /// Preview track backed by a local <see cref="IWorkingBeatmap"/> instead of the online preview CDN.
    /// </summary>
    public partial class EzLocalBeatmapPreviewTrack : PreviewTrack
    {
        private readonly IWorkingBeatmap workingBeatmap;

        private bool usesSharedBeatmapTrack;

        public EzLocalBeatmapPreviewTrack(IWorkingBeatmap workingBeatmap)
        {
            this.workingBeatmap = workingBeatmap;
        }

        protected override Track? GetTrack()
        {
            workingBeatmap.LoadTrack();
            workingBeatmap.PrepareTrackForPreview(true);
            usesSharedBeatmapTrack = true;
            return workingBeatmap.Track;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Stop();

                // The preview borrows the gameplay WorkingBeatmap track; disposing it would break song select / replay.
                if (usesSharedBeatmapTrack)
                    return;
            }

            base.Dispose(isDisposing);
        }
    }
}
