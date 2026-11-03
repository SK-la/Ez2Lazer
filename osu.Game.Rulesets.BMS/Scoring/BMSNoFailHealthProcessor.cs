// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.Scoring
{
    /// <summary>
    /// BMS health processor that never fails by default and has no passive drain.
    /// Health changes only come from judgement results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// End-user behaviour matches <see cref="osu.Game.Rulesets.Mods.ModNoFail"/> for failure:
    /// <see cref="HealthProcessor.Health"/> can still decrease all the way to its minimum (0);
    /// the HUD reflects that, but the run is never aborted for "empty gauge". Play continues
    /// to the end of the chart and the score is settled as a normal completion (not a fail exit).
    /// </para>
    /// <para>
    /// Layer 1: returning <c>false</c> from <see cref="CheckDefaultFailCondition"/> prevents
    /// the default "health at or below minimum" path from calling
    /// <see cref="HealthProcessor.TriggerFailure"/>.
    /// </para>
    /// <para>
    /// Layer 2: <c>BmsPlayer.CheckModsAllowFailure</c> returns <c>false</c>, same idea as
    /// <c>ModNoFail.PerformFail()</c>, so the <see cref="HealthProcessor.Failed"/> handler never
    /// permits <see cref="HealthProcessor.HasFailed"/> even if a mod registers
    /// <see cref="HealthProcessor.FailConditions"/>.
    /// </para>
    /// </remarks>
    public partial class BMSNoFailHealthProcessor : HealthProcessor
    {
        public BMSNoFailHealthProcessor(double drainStartTime)
        {
        }

        protected override bool CheckDefaultFailCondition(JudgementResult result) => false;
    }
}
