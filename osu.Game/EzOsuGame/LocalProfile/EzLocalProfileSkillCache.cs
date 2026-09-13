// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Per-play SSR cache entry in <c>ez-local-profile.sqlite</c>. Mirrors the official star cache:
    /// a play already rated with the current algorithm is reused so a recompute only fills in missing plays.
    /// <see cref="HasSsr"/> false records a play the engine deliberately produced no vector for,
    /// so it is not retried on every run.
    /// </summary>
    public readonly record struct EzSsrPlayCacheRow(
        Guid ScoreId,
        string Username,
        string BeatmapHash,
        int KeyCount,
        double Rate,
        DateTimeOffset ScoredAt,
        bool HasSsr,
        EzSkillsetVector Vector,
        int AlgorithmVersion);

    /// <summary>
    /// Per-play Dan cache entry in <c>ez-local-profile.sqlite</c>.
    /// <see cref="Credited"/> false records a play that produced no dan credit, so its chart-dan estimate is not redone.
    /// </summary>
    public readonly record struct EzDanPlayCacheRow(
        Guid ScoreId,
        string Username,
        string BeatmapHash,
        int AlgorithmVersion,
        bool Credited,
        int KeyCount,
        string Side,
        double Rate,
        double CreditedDan,
        double Accuracy,
        DateTimeOffset ScoredAt);
}
