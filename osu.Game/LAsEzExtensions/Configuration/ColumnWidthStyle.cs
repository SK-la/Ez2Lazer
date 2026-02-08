// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public enum ColumnWidthStyle
    {
        [Description("EzStylePro Only")]
        EzStyleProOnly = 0,

        [Description("Global (全局)")]
        GlobalWidth = 1,

        [Description("Global Total (全局总宽度)")]
        GlobalTotalWidth = 2,
    }
}
