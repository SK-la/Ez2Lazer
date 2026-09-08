// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects
{
    /// <summary>
    /// EZ2AC LN 16 分 tick：只推动 combo，不计入 Kool/Cool 等基础判定档。
    /// 默认 Ignore；EZ2AC 由 <see cref="EzMania.ReplayJudge.ManiaEnvironmentJudgements"/> 绑成 HoldNoteTickJudgement。
    /// </summary>
    public class HoldNoteTick : ManiaHitObject
    {
        public override Judgement CreateJudgement() => new IgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
