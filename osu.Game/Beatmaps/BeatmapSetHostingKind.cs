// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// How beatmap set content is stored and resolved at runtime.
    /// </summary>
    public enum BeatmapSetHostingKind
    {
        /// <summary>
        /// Content lives in the game <c>files/</c> store (standard import).
        /// </summary>
        Internal = 0,

        /// <summary>
        /// Content is read from <see cref="BeatmapSetInfo.ExternalContentRoot"/> on disk.
        /// </summary>
        External = 1,
    }
}
