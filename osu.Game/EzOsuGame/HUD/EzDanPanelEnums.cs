// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// Data sources for <see cref="EzHUDDanDualPanel"/>, mirroring Skill-radar A / B / AB.
    /// </summary>
    public enum EzDanPanelDataSource
    {
        /// <summary>Chart MSD only (radar primary / yellow layer).</summary>
        [Description("Chart")]
        Chart,

        /// <summary>Player SSR / dan only (radar secondary / green layer).</summary>
        [Description("Player")]
        Player,

        /// <summary>Chart + player together.</summary>
        [Description("Both")]
        Both,
    }

    /// <summary>
    /// How RC and LN lists are arranged relative to each other.
    /// </summary>
    public enum EzDanPanelDualLayout
    {
        /// <summary>Horizontal when wide enough, otherwise vertical.</summary>
        [Description("Auto")]
        Auto,

        /// <summary>RC | LN side-by-side; axis chips stack vertically inside each list.</summary>
        [Description("Horizontal")]
        Horizontal,

        /// <summary>RC above LN; axis chips flow horizontally inside each list.</summary>
        [Description("Vertical")]
        Vertical,
    }
}
