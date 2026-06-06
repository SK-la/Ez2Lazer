// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Database
{
    [Flags]
    public enum EzRealmMetadataScope
    {
        Tags = 1,
        Xxy = 2,
        Pp = 4,
        All = Tags | Xxy | Pp,
    }
}
