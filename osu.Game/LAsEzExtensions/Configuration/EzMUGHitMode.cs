// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public enum EzMUGHitMode
    {
        [Description("Lazer Style")]
        Lazer = 0,

        [Description("EZ2AC Style")]
        EZ2AC = 1,

        [Description("O2JAM Style")]
        O2Jam = 2,

        [Description("IIDX Hard Style(Testing)")]
        IIDX_HD = 3,

        [Description("LR2 Hard Style(Testing)")]
        LR2_HD = 4,

        [Description("Raja Hard Style(Testing)")]
        Raja_NM = 5,

        [Description("/(NotAction)")]
        Malody = 6,

        [Description("/(NotAction)")]
        Classic = 7,
    }
}
