// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Taiko.EzTaiko.ReplayJudge.Judgement
{
    /// <summary>
    /// DrumRoll / Swell 判定纯函数（TTL-001）：Session Mapping 入口，对齐 Drawable 语义。
    /// </summary>
    public static class TaikoRollingJudgement
    {
        public static bool IsTickHit(double pressTime, double tickStartTime, double hitWindow)
            => Math.Abs(pressTime - tickStartTime) <= hitWindow;

        public static HitResult TickResult(bool hit, HitResult maxResult, HitResult minResult)
            => hit ? maxResult : minResult;

        public static bool IsValidSwellPress(bool isCentre, bool? lastWasCentre, bool mustAlternate)
        {
            if (!mustAlternate || lastWasCentre == null)
                return true;

            return lastWasCentre != isCentre;
        }

        public static HitResult SwellBodyResult(int hitCount, int requiredHits, HitResult maxResult, HitResult minResult)
            => hitCount >= requiredHits ? maxResult : minResult;

        public static HitResult StrongNestedResult(bool parentHit, HitResult maxResult, HitResult minResult)
            => parentHit ? maxResult : minResult;

        public static bool IsCentreAction(TaikoAction action)
            => action is TaikoAction.LeftCentre or TaikoAction.RightCentre;
    }
}
