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

        public EzLocalBeatmapPreviewTrack(IWorkingBeatmap workingBeatmap)
        {
            this.workingBeatmap = workingBeatmap;
        }

        protected override Track? GetTrack()
        {
            workingBeatmap.LoadTrack();
            workingBeatmap.PrepareTrackForPreview(true);
            return workingBeatmap.Track;
        }
    }
}
