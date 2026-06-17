// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Ez HitMode 判定单源：note + hold 映射与 Session/Drawable 共用。
    /// Lazer/Classic 仍走 <see cref="Replicas.LazerNoteJudgementReplica"/> 双轨，不实现本接口。
    /// </summary>
    public interface IManiaHitModeJudgement : IManiaNoteJudgementStrategy, IManiaHoldJudgementStrategy
    {
    }
}
