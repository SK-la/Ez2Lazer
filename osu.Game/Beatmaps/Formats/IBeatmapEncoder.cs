// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;

namespace osu.Game.Beatmaps.Formats
{
    /// <summary>
    /// Encodes an <see cref="IBeatmap"/> so the editor can save and snapshot undo states.
    /// </summary>
    public interface IBeatmapEncoder
    {
        void Encode(TextWriter writer);
    }
}
