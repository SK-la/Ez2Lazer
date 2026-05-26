// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Provides the mania gameplay modes that were locked in at the start of a play session.
    /// </summary>
    public interface IManiaGameplayModeSnapshot
    {
        int HitMode { get; }

        int HealthMode { get; }
    }
}
