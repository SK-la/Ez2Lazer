// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayInputNotes
{
    /// <summary>
    /// What a replay press hit, which decides whether the overlay note is coloured as a whole or split into head and non-head.
    /// </summary>
    public enum EzReplayInputNoteKind
    {
        /// <summary>No judged object was hit by this press.</summary>
        None,

        /// <summary>A plain <see cref="Objects.Note"/> was hit: the whole note takes the press result.</summary>
        Note,

        /// <summary>A <see cref="Objects.HoldNote"/> head was hit: head and non-head are coloured separately.</summary>
        Hold,
    }

    /// <summary>
    /// One press-to-release span drawn on top of a replay column.
    /// </summary>
    public sealed class EzReplayInputNote
    {
        public int Column { get; init; }

        /// <summary>The press time.</summary>
        public double StartTime { get; init; }

        /// <summary>The release time; equal to <see cref="StartTime"/> for tap-style notes.</summary>
        public double EndTime { get; init; }

        /// <summary>
        /// Whether the note is drawn as an editor tap note (single body, no head/tail blocks) instead of a held span.
        /// </summary>
        public bool IsTapStyle { get; init; }

        public EzReplayInputNoteKind Kind { get; init; }

        /// <summary>The judgement of the press, or of the whole tap.</summary>
        public HitResult? HeadResult { get; init; }

        /// <summary>The judgement of the hold's tail (or its break), used for the non-head part of <see cref="EzReplayInputNoteKind.Hold"/>.</summary>
        public HitResult? NonHeadResult { get; init; }

        /// <summary>Time from which <see cref="NonHeadResult"/> may be revealed; the tail judgement or the break happens at this time.</summary>
        public double NonHeadRevealTime { get; init; }

        /// <summary>Whether the hold's body broke; the non-head part is then shown as ignored.</summary>
        public bool BodyBroken { get; init; }

        public double Duration => EndTime > StartTime ? EndTime - StartTime : 0;
    }
}
