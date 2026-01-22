// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public partial class O2DrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                // 使用当前时间进行动态 BPM 计算
                bool cont = O2HitModeExtension.PillCheck(timeOffset, Time.Current, out bool _, out upgradeToPerfect);
                if (!cont) return;
            }

            // 此处有潜在的崩溃风险，与播放动画有关，待调查。
            // Replicate base implementation to allow attaching combo semantics overrides.
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNoteHead : DrawableHoldNoteHead
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                // 使用当前时间进行动态 BPM 计算
                bool cont = O2HitModeExtension.PillCheck(timeOffset, Time.Current, out bool _, out upgradeToPerfect);
                if (!cont) return;
            }

            // Replicate base implementation to allow attaching combo semantics overrides.
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNoteTail : DrawableHoldNoteTail
    {
        public override bool DisplayResult => false;

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            bool upgradeToPerfect = false;

            if (userTriggered)
            {
                // 使用当前时间进行动态 BPM 计算
                bool cont = O2HitModeExtension.PillCheck(timeOffset, Time.Current, out bool _, out upgradeToPerfect);
                if (!cont) return;
            }

            // Behaviour parity with previous implementation:
            // Previously we forwarded `timeOffset * RELEASE_WINDOW_LENIENCE` to base, which then divided by RELEASE_WINDOW_LENIENCE,
            // resulting in `timeOffset` being used for hit windows.
            double adjustedOffset = timeOffset;

            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(adjustedOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(adjustedOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);

            if (upgradeToPerfect)
                result = HitResult.Perfect;

            ApplyResult(static (r, state) =>
            {
                r.Type = state;

                // In O2Jam hit mode, Meh should break combo.
                if (state == HitResult.Meh)
                    r.IsComboHit = false;
            }, result);
        }
    }

    public partial class O2DrawableHoldNote : DrawableHoldNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (Tail.AllJudged)
            {
                if (Tail.IsHit)
                {
                    bool breakComboFromTailMeh = Tail.Result.Type == HitResult.Meh;

                    ApplyResult(static (r, breakCombo) =>
                    {
                        r.Type = r.Judgement.MaxResult;

                        // In O2Jam hit mode, a Meh on the tail should terminally break combo.
                        // Prevent the parent hold note result from immediately re-increasing combo afterwards.
                        if (breakCombo)
                            r.IsComboHit = false;
                    }, breakComboFromTailMeh);
                }
                else
                    MissForcefully();

                // Make sure that the hold note is fully judged by giving the body a judgement.
                if (!Body.AllJudged)
                    Body.TriggerResult(Tail.IsHit);

                // Important that this is always called when a result is applied.
                Result.ReportHoldState(Time.Current, false);
            }
        }
    }
}
