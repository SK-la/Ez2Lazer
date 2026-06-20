// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public interface IManiaNoteJudgementStrategy
    {
        ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows);

        ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows);

        /// <summary>
        /// 在无 Session 上下文的 Graph/Race 展示层，对单个 <see cref="HitEvent"/>
        /// 重新判定。各实现类根据自身规则处理不同类型的 HitObject：
        /// <list type="bullet">
        ///   <item><see cref="Objects.TailNote"/>：保留原始结果（尾判依赖复杂的 hold 上下文，重判路径无法准确还原）。</item>
        ///   <item><see cref="Objects.HeadNote"/>：若该模式有 LN 头软化则应用软化。</item>
        ///   <item>普通 <see cref="Objects.Note"/>：等效 <see cref="EvaluatePress"/>。</item>
        /// </list>
        /// </summary>
        /// <param name="hitEvent">原始 HitEvent（含 TimeOffset、HitObject、Result）。</param>
        /// <param name="hitWindows">当前 HitMode 的判定窗口实例。</param>
        /// <returns>重判后的 HitResult，不可判定时返回 <see cref="HitResult.Miss"/>。</returns>
        HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows);
    }
}
