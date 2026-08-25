// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public enum EzLocalProfileOnlinePullKind
    {
        [Description("BP（每批50）")]
        Best,

        [Description("玩过的图（每批50）")]
        MostPlayed,
    }
}
