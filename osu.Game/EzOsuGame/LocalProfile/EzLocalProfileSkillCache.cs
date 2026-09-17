// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// One mania play of a player reduced to what the skill pass reads, instead of the whole-library
    /// <see cref="ScoreInfo"/> deep clone the pass used to build. A play the per-play cache answers is folded
    /// entirely from its cache row, so neither its beatmap nor its mods are touched; a play without one is resolved
    /// by id, in bounded windows, only once the pass actually needs it.
    /// </summary>
    public readonly record struct EzSkillPlayRow(
        Guid ScoreId,
        string BeatmapHash,
        DateTimeOffset ScoredAt,
        double Accuracy,
        bool Passed,
        ScoreRank Rank);

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
