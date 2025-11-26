// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public static class O2HitObject
    {
        public const double COOL = 7500.0;
        public const double GOOD = 22500.0;
        public const double BAD = 31250.0;
        public const double DEFAULT_BPM = 200;

        // TODO: 💊缺少UI显示，以及合适的开关
        public static bool PillActivated; // = ManiaModO2Judgement.PillMode.Value;
        public static int Pill;
        public static int CoolCombo;
        public static double CoolRange => 7500.0 / NowBeatmapBPM;
        public static double GoodRange => 22500.0 / NowBeatmapBPM;
        public static double BadRange => 31250.0 / NowBeatmapBPM;
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
            if (O2HitObject.PillActivated)
            {
                double offset = Math.Abs(timeOffset);

                if (offset <= O2HitObject.CoolRange)
                {
                    O2HitObject.CoolCombo++;

                    if (O2HitObject.CoolCombo >= 15)
                    {
                        O2HitObject.CoolCombo -= 15;

                        if (O2HitObject.Pill < 5)
                            O2HitObject.Pill++;
                    }
                }
                else if (offset > O2HitObject.CoolRange && offset <= O2HitObject.GoodRange)
                    O2HitObject.CoolCombo = 0;
                else if (offset > O2HitObject.GoodRange && offset <= O2HitObject.BadRange)
                {
                    O2HitObject.CoolCombo = 0;

                    if (O2HitObject.Pill > 0)
                    {
                        O2HitObject.Pill--;

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
            if (O2HitObject.PillActivated)
            {
                double offset = Math.Abs(timeOffset);

                if (offset <= O2HitObject.CoolRange)
                {
                    O2HitObject.CoolCombo++;

                    if (O2HitObject.CoolCombo >= 15)
                    {
                        O2HitObject.CoolCombo -= 15;

                        if (O2HitObject.Pill < 5)
                            O2HitObject.Pill++;
                    }
                }
                else if (offset > O2HitObject.CoolRange && offset <= O2HitObject.GoodRange)
                    O2HitObject.CoolCombo = 0;
                else if (offset > O2HitObject.GoodRange && offset <= O2HitObject.BadRange)
                {
                    O2HitObject.CoolCombo = 0;

                    if (O2HitObject.Pill > 0)
                    {
                        O2HitObject.Pill--;

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
            if (O2HitObject.PillActivated)
            {
                double offset = Math.Abs(timeOffset);

                if (offset <= O2HitObject.CoolRange)
                {
                    O2HitObject.CoolCombo++;

                    if (O2HitObject.CoolCombo >= 15)
                    {
                        O2HitObject.CoolCombo -= 15;

                        if (O2HitObject.Pill < 5)
                            O2HitObject.Pill++;
                    }
                }
                else if (offset > O2HitObject.CoolRange && offset <= O2HitObject.GoodRange)
                    O2HitObject.CoolCombo = 0;
                else if (offset > O2HitObject.GoodRange && offset <= O2HitObject.BadRange)
                {
                    O2HitObject.CoolCombo = 0;

                    if (O2HitObject.Pill > 0)
                    {
                        O2HitObject.Pill--;

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

    public class O2HoldNote : HoldNote
    {
        public O2HoldNote(HoldNote hold)
        {
            StartTime = hold.StartTime;
            Duration = hold.Duration;
            Column = hold.Column;
            NodeSamples = hold.NodeSamples;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            AddNested(Head = new O2LNHead
            {
                StartTime = StartTime,
                Column = Column,
                Samples = GetNodeSamples(0)
            });

            AddNested(Tail = new O2LNTail
            {
                StartTime = EndTime,
                Column = Column,
                Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
            });

            AddNested(Body = new HoldNoteBody
            {
                StartTime = StartTime,
                Column = Column
            });
        }

        public override double MaximumJudgementOffset => base.MaximumJudgementOffset / TailNote.RELEASE_WINDOW_LENIENCE;
    }
}
