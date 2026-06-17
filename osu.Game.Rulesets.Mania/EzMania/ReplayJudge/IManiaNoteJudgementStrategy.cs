// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public interface IManiaNoteJudgementStrategy
    {
        ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows);

        ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows);
    }
}
