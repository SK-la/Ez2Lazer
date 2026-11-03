// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// Allows external adjustment of scroll density for rulesets that recompute their time range each frame.
    /// </summary>
    public interface IPreviewScrollDensityAdjustable
    {
        /// <summary>
        /// Values greater than 1 increase density (notes closer together).
        /// </summary>
        double PreviewDensityMultiplier { get; set; }
    }
}
