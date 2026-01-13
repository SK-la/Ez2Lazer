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

        [Description("Beatmania IIDX Style(NoAction)")]
        IIDX = 2,

        [Description("O2JAM Style")]
        O2Jam = 3,

        [Description("Malody Style")]
        Malody = 4,

        [Description("Classic Style(NoAction)")]
        Classic = 5,
    }
}
