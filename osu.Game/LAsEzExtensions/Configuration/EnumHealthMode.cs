// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public enum EnumHealthMode
    {
        [Description("Lazer")]
        Lazer,

        [Description("O2Jam Easy")]
        O2JamEasy,

        [Description("O2Jam Normal")]
        O2JamNormal,

        [Description("O2Jam Hard")]
        O2JamHard,

        [Description("Ez2Ac(NoActive)")]
        Ez2Ac,

        [Description("IIDX(NoActive)")]
        IIDX,
    }
}
