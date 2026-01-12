// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public enum EzMUGHitMode
    {
        [Description("Lazer Style")]
        Lazer,

        [Description("EZ2AC Style")]
        EZ2AC,

        [Description("Beatmania IIDX Style(NoAction)")]
        IIDX,

        [Description("Melody Style")]
        Melody,

        [Description("O2JAM Style")]
        O2Jam,

        [Description("Classic Style(NoAction)")]
        Classic,
    }
}
