// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Database
{
    [Flags]
    public enum EzRealmMetadataScope
    {
        Tags = 1,
        SunnySR = 2,
        PP = 4,
        MSD = 8,
        ChartSkillInfo = 16,
        ChartDan = 32,
        PPP = 64,
        All = Tags | SunnySR | PP | MSD | ChartSkillInfo | ChartDan | PPP,
    }
}
