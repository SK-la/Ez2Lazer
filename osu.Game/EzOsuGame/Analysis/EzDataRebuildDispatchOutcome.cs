// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>One action that could not be queued, with the reason the owning queue gave.</summary>
    public readonly record struct EzDataRebuildDispatchFailure(EzDataRebuildAction Action, EzDataRebuildDispatchResult Result);

    /// <summary>
    /// Result of dispatching a selection. Actions sharing a queue are dispatched once, so every action in the
    /// group resolves to the same <see cref="EzDataRebuildDispatchResult"/>.
    /// </summary>
    public sealed record EzDataRebuildDispatchOutcome(
        IReadOnlyList<EzDataRebuildAction> Queued,
        IReadOnlyList<EzDataRebuildDispatchFailure> Failures)
    {
        /// <summary>First failure, used when a caller wants one message for the whole selection.</summary>
        public EzDataRebuildDispatchResult FirstFailure =>
            Failures.Count == 0 ? EzDataRebuildDispatchResult.Queued : Failures[0].Result;
    }
}
