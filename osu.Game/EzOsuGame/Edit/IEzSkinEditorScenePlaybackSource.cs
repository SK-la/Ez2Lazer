// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Scene-local beatmap preview clock exposed to <see cref="Components.EzSkinEditorSceneBar"/> playback controls.
    /// </summary>
    public interface IEzSkinEditorScenePlaybackSource
    {
        bool IsActive { get; }

        double BeatmapMinTime { get; }

        double BeatmapMaxTime { get; }

        double CurrentTime { get; }

        bool IsPlaying { get; }

        void Seek(double time);

        void SetPlaying(bool playing);
    }
}
