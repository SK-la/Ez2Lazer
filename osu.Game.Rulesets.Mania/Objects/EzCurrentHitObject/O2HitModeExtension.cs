// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    // 代码改编自YuLiangSSS提供的ManiaModO2Judgement
    public static class O2HitModeExtension
    {
        public const double COOL = 7500.0;
        public const double GOOD = 22500.0;
        public const double BAD = 31250.0;
        public const double DEFAULT_BPM = 200;

        // TODO: 💊缺少UI显示，以及合适的开关
        public static bool PillActivated; // = ManiaModO2Judgement.PillMode.Value;
        public static int Pill;
        public static int CoolCombo;
        public static double CoolRange => COOL / NowBeatmapBPM;
        public static double GoodRange => GOOD / NowBeatmapBPM;
        public static double BadRange => BAD / NowBeatmapBPM;
        public static double NowBeatmapBPM = 200;
    }

    public partial class O2DrawableNote : DrawableNote
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
            {
                bool flowControl = PillCheck(timeOffset);
                if (!flowControl) return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }

        public bool PillCheck(double timeOffset)
        {
            if (O2HitModeExtension.PillActivated)
            {
                double offset = Math.Abs(timeOffset);

                if (offset <= O2HitModeExtension.CoolRange)
                {
                    O2HitModeExtension.CoolCombo++;

                    if (O2HitModeExtension.CoolCombo >= 15)
                    {
                        O2HitModeExtension.CoolCombo -= 15;

                        if (O2HitModeExtension.Pill < 5)
                            O2HitModeExtension.Pill++;
                    }
                }
                else if (offset > O2HitModeExtension.CoolRange && offset <= O2HitModeExtension.GoodRange)
                    O2HitModeExtension.CoolCombo = 0;
                else if (offset > O2HitModeExtension.GoodRange && offset <= O2HitModeExtension.BadRange)
                {
                    O2HitModeExtension.CoolCombo = 0;

                    if (O2HitModeExtension.Pill > 0)
                    {
                        O2HitModeExtension.Pill--;

                        ApplyResult(GetCappedResult(HitResult.Perfect));
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public partial class O2DrawableHoldNoteHead : DrawableHoldNoteHead
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
            {
                bool flowControl = PillCheck(timeOffset);
                if (!flowControl) return;
            }

            base.CheckForResult(userTriggered, timeOffset);
        }

        public bool PillCheck(double timeOffset)
        {
            if (O2HitModeExtension.PillActivated)
            {
                double offset = Math.Abs(timeOffset);

                if (offset <= O2HitModeExtension.CoolRange)
                {
                    O2HitModeExtension.CoolCombo++;

                    if (O2HitModeExtension.CoolCombo >= 15)
                    {
                        O2HitModeExtension.CoolCombo -= 15;

                        if (O2HitModeExtension.Pill < 5)
                            O2HitModeExtension.Pill++;
                    }
                }
                else if (offset > O2HitModeExtension.CoolRange && offset <= O2HitModeExtension.GoodRange)
                    O2HitModeExtension.CoolCombo = 0;
                else if (offset > O2HitModeExtension.GoodRange && offset <= O2HitModeExtension.BadRange)
                {
                    O2HitModeExtension.CoolCombo = 0;

                    if (O2HitModeExtension.Pill > 0)
                    {
                        O2HitModeExtension.Pill--;

                        ApplyResult(GetCappedResult(HitResult.Perfect));
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public partial class O2DrawableHoldNoteTail : DrawableHoldNoteTail
    {
        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered)
            {
                bool flowControl = PillCheck(timeOffset);
                if (!flowControl) return;
            }

            base.CheckForResult(userTriggered, timeOffset * TailNote.RELEASE_WINDOW_LENIENCE);
        }

        public bool PillCheck(double timeOffset)
        {
            if (O2HitModeExtension.PillActivated)
            {
                double offset = Math.Abs(timeOffset);

                if (offset <= O2HitModeExtension.CoolRange)
                {
                    O2HitModeExtension.CoolCombo++;

                    if (O2HitModeExtension.CoolCombo >= 15)
                    {
                        O2HitModeExtension.CoolCombo -= 15;

                        if (O2HitModeExtension.Pill < 5)
                            O2HitModeExtension.Pill++;
                    }
                }
                else if (offset > O2HitModeExtension.CoolRange && offset <= O2HitModeExtension.GoodRange)
                    O2HitModeExtension.CoolCombo = 0;
                else if (offset > O2HitModeExtension.GoodRange && offset <= O2HitModeExtension.BadRange)
                {
                    O2HitModeExtension.CoolCombo = 0;

                    if (O2HitModeExtension.Pill > 0)
                    {
                        O2HitModeExtension.Pill--;

                        ApplyResult(GetCappedResult(HitResult.Perfect));
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public class O2Note : Note
    {
        public O2Note(Note note)
        {
            StartTime = note.StartTime;
            Column = note.Column;
            Samples = note.Samples;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
        }
    }

    public class O2LNHead : HeadNote
    {
    }

    public class O2LNTail : TailNote
    {
        public override double MaximumJudgementOffset => base.MaximumJudgementOffset / RELEASE_WINDOW_LENIENCE;
    }
}
