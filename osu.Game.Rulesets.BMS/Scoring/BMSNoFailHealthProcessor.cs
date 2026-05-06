// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.Scoring
{
    /// <summary>
    /// BMS health processor that never fails and has no passive drain.
    /// Health changes only come from judgement results.
    /// </summary>
    public partial class BMSNoFailHealthProcessor : HealthProcessor
    {
        public BMSNoFailHealthProcessor(double drainStartTime)
        {
        }

        protected override bool CheckDefaultFailCondition(JudgementResult result) => false;
    }
}
