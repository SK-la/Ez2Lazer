// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Statistics;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    public partial class EzManiaScoreHitResultCountGraph : EzScoreHitResultCountGraph
    {
        public EzManiaScoreHitResultCountGraph(ScoreInfo score)
            : base(score)
        {
            RulesetInstance = (ManiaRuleset)score.Ruleset.CreateInstance();
        }
    }
}
