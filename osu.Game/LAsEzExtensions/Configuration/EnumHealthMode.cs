// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public enum EnumHealthMode
    {
        [Description("Lazer")]
        Lazer = 0,

        [Description("O2Jam Easy")]
        O2JamEasy = 1,

        [Description("O2Jam Normal")]
        O2JamNormal = 2,

        [Description("O2Jam Hard")]
        O2JamHard = 3,

        [Description("Ez2Ac(NoActive)")]
        Ez2Ac = 4,

        [Description("IIDX Hard(Testing)")]
        IIDX_HD = 5,

        [Description("LR2 Hard(Testing)")]
        LR2_HD = 6,

        [Description("raja normal(Testing)")]
        Raja_NM = 7,
    }
}
