// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    internal static class EzQuickRotationAudio
    {
        public static void StopGameplayAndMenuAudio(MusicController musicController, IBindable<WorkingBeatmap> beatmap)
        {
            musicController.Stop();

            if (beatmap.Value.TrackLoaded)
                StopTrack(beatmap.Value.Track);
        }

        public static void StopTrack(Track track)
        {
            if (!track.IsLoaded)
                return;

            track.Stop();
            track.Seek(0);
        }
    }
}
