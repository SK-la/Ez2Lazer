// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class ManiaModRemedy : Mod, IApplicableToDifficulty, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "Remedy";

        public override string Acronym => "RY";

        public override LocalisableString Description => EzManiaModStrings.Remedy_Description;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Perfect", $"{PerfectCount.Value}");
                yield return ("Great", $"{GreatCount.Value}");
                yield return ("Good", $"{GoodCount.Value}");
                yield return ("Ok", $"{OkCount.Value}");
                yield return ("Meh", $"{MehCount.Value}");
            }
        }

        public static int RemedyGreat;
        public static int RemedyGood;
        public static int RemedyOk;
        public static int RemedyMeh;
        public static int RemedyMiss;

        public HitWindows HitWindows { get; set; } = new ManiaHitWindows();

        [SettingSource("Perfect Count", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> PerfectCount { get; } = new Bindable<int?>();

        [SettingSource("Great Count", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> GreatCount { get; } = new Bindable<int?>();

        [SettingSource("Good Count", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> GoodCount { get; } = new Bindable<int?>();

        [SettingSource("Ok Count", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> OkCount { get; } = new Bindable<int?>();

        [SettingSource("Meh Count", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> MehCount { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
            {
                if (obj is Note note)
                    return new RemedyNote(note);

                if (obj is HoldNote hold)
                    return new RemedyHoldNote(hold);

                return obj;
            }).ToList();

            maniaBeatmap.HitObjects = hitObjects;
            RemedyGreat = PerfectCount.Value ?? 0;
            RemedyGood = GreatCount.Value ?? 0;
            RemedyOk = GoodCount.Value ?? 0;
            RemedyMeh = OkCount.Value ?? 0;
            RemedyMiss = MehCount.Value ?? 0;
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            foreach (var stage in maniaRuleset.Playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    column.RegisterPool<RemedyNote, RemedyDrawableNote>(10, 50);
                    column.RegisterPool<RemedyHeadNote, RemedyDrawableHoldNoteHead>(10, 50);
                    column.RegisterPool<RemedyTailNote, RemedyDrawableHoldNoteTail>(10, 50);
                }
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            HitWindows = new ManiaHitWindows();
            HitWindows.SetDifficulty(difficulty.OverallDifficulty);

            RemedyDrawableNote.HitWindows = HitWindows;
            RemedyDrawableHoldNoteHead.HitWindows = HitWindows;
            RemedyDrawableHoldNoteTail.HitWindows = HitWindows;
        }

        public partial class RemedyDrawableNote : DrawableNote
        {
            public static HitWindows HitWindows = new ManiaHitWindows();

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (userTriggered && RemedyGreat > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Perfect)))
                {
                    RemedyGreat--;
                    ApplyResult(GetCappedResult(HitResult.Perfect));
                }
                else if (userTriggered && RemedyGood > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Great)))
                {
                    RemedyGood--;
                    ApplyResult(GetCappedResult(HitResult.Great));
                }
                else if (userTriggered && RemedyOk > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Good)))
                {
                    RemedyOk--;
                    ApplyResult(GetCappedResult(HitResult.Good));
                }
                else if (userTriggered && RemedyMeh > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Ok)))
                {
                    RemedyMeh--;
                    ApplyResult(GetCappedResult(HitResult.Ok));
                }
                else if (userTriggered && RemedyMiss > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Meh))
                         && Math.Abs(timeOffset) <= Math.Abs(HitWindows.WindowFor(HitResult.Miss)))
                {
                    RemedyMiss--;
                    ApplyResult(GetCappedResult(HitResult.Meh));
                }
                else if (!userTriggered)
                {
                    if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    {
                        if (RemedyGreat > 0)
                        {
                            RemedyGreat--;
                            ApplyResult(GetCappedResult(HitResult.Perfect));
                        }
                        else if (RemedyGood > 0)
                        {
                            RemedyGood--;
                            ApplyResult(GetCappedResult(HitResult.Great));
                        }
                        else if (RemedyOk > 0)
                        {
                            RemedyOk--;
                            ApplyResult(GetCappedResult(HitResult.Good));
                        }
                        else if (RemedyMeh > 0)
                        {
                            RemedyMeh--;
                            ApplyResult(GetCappedResult(HitResult.Ok));
                        }
                        else if (RemedyMiss > 0)
                        {
                            RemedyMiss--;
                            ApplyResult(GetCappedResult(HitResult.Meh));
                        }
                        else
                        {
                            ApplyMinResult();
                        }
                    }
                }
                else
                {
                    base.CheckForResult(userTriggered, timeOffset);
                }
            }
        }

        public partial class RemedyDrawableHoldNoteHead : DrawableHoldNoteHead
        {
            public static HitWindows HitWindows = new ManiaHitWindows();

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (userTriggered && RemedyGreat > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Perfect)))
                {
                    RemedyGreat--;
                    ApplyResult(GetCappedResult(HitResult.Perfect));
                }
                else if (userTriggered && RemedyGood > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Great)))
                {
                    RemedyGood--;
                    ApplyResult(GetCappedResult(HitResult.Great));
                }
                else if (userTriggered && RemedyOk > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Good)))
                {
                    RemedyOk--;
                    ApplyResult(GetCappedResult(HitResult.Good));
                }
                else if (userTriggered && RemedyMeh > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Ok)))
                {
                    RemedyMeh--;
                    ApplyResult(GetCappedResult(HitResult.Ok));
                }
                else if (userTriggered && RemedyMiss > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Meh))
                         && Math.Abs(timeOffset) <= Math.Abs(HitWindows.WindowFor(HitResult.Miss)))
                {
                    RemedyMiss--;
                    ApplyResult(GetCappedResult(HitResult.Meh));
                }
                else if (!userTriggered)
                {
                    if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    {
                        if (RemedyGreat > 0)
                        {
                            RemedyGreat--;
                            ApplyResult(GetCappedResult(HitResult.Perfect));
                        }
                        else if (RemedyGood > 0)
                        {
                            RemedyGood--;
                            ApplyResult(GetCappedResult(HitResult.Great));
                        }
                        else if (RemedyOk > 0)
                        {
                            RemedyOk--;
                            ApplyResult(GetCappedResult(HitResult.Good));
                        }
                        else if (RemedyMeh > 0)
                        {
                            RemedyMeh--;
                            ApplyResult(GetCappedResult(HitResult.Ok));
                        }
                        else if (RemedyMiss > 0)
                        {
                            RemedyMiss--;
                            ApplyResult(GetCappedResult(HitResult.Meh));
                        }
                        else
                        {
                            ApplyMinResult();
                        }
                    }
                }
                else
                {
                    base.CheckForResult(userTriggered, timeOffset);
                }
            }
        }

        public partial class RemedyDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            public static HitWindows HitWindows = new ManiaHitWindows();

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (HoldNote.IsHolding.Value && userTriggered && RemedyGreat > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Perfect)))
                {
                    RemedyGreat--;
                    ApplyResult(GetCappedResult(HitResult.Perfect));
                }
                else if (HoldNote.IsHolding.Value && userTriggered && RemedyGood > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Great)))
                {
                    RemedyGood--;
                    ApplyResult(GetCappedResult(HitResult.Great));
                }
                else if (HoldNote.IsHolding.Value && userTriggered && RemedyOk > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Good)))
                {
                    RemedyOk--;
                    ApplyResult(GetCappedResult(HitResult.Good));
                }
                else if (HoldNote.IsHolding.Value && userTriggered && RemedyMeh > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Ok)))
                {
                    RemedyMeh--;
                    ApplyResult(GetCappedResult(HitResult.Ok));
                }
                else if (HoldNote.IsHolding.Value && userTriggered && RemedyMiss > 0 && Math.Abs(timeOffset) > Math.Abs(HitWindows.WindowFor(HitResult.Meh))
                         && Math.Abs(timeOffset) <= Math.Abs(HitWindows.WindowFor(HitResult.Miss)))
                {
                    RemedyMiss--;
                    ApplyResult(GetCappedResult(HitResult.Meh));
                }
                else if (!userTriggered)
                {
                    if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    {
                        if (RemedyGreat > 0)
                        {
                            RemedyGreat--;
                            ApplyResult(GetCappedResult(HitResult.Perfect));
                        }
                        else if (RemedyGood > 0)
                        {
                            RemedyGood--;
                            ApplyResult(GetCappedResult(HitResult.Great));
                        }
                        else if (RemedyOk > 0)
                        {
                            RemedyOk--;
                            ApplyResult(GetCappedResult(HitResult.Good));
                        }
                        else if (RemedyMeh > 0)
                        {
                            RemedyMeh--;
                            ApplyResult(GetCappedResult(HitResult.Ok));
                        }
                        else if (RemedyMiss > 0)
                        {
                            RemedyMiss--;
                            ApplyResult(GetCappedResult(HitResult.Meh));
                        }
                        else
                        {
                            ApplyMinResult();
                        }
                    }
                }
                else
                {
                    base.CheckForResult(userTriggered, timeOffset);
                }
            }
        }

        private class RemedyNote : Note
        {
            public RemedyNote(Note note)
            {
                StartTime = note.StartTime;
                Column = note.Column;
                Samples = note.Samples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
            }
        }

        private class RemedyHeadNote : HeadNote
        {
        }

        private class RemedyTailNote : TailNote
        {
        }

        private class RemedyHoldNote : HoldNote
        {
            public RemedyHoldNote(HoldNote hold)
            {
                StartTime = hold.StartTime;
                Duration = hold.Duration;
                Column = hold.Column;
                NodeSamples = hold.NodeSamples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                AddNested(Head = new RemedyHeadNote
                {
                    StartTime = StartTime,
                    Column = Column,
                    Samples = GetNodeSamples(0),
                });

                AddNested(Tail = new RemedyTailNote
                {
                    StartTime = EndTime,
                    Column = Column,
                    Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1),
                });

                AddNested(Body = new HoldNoteBody
                {
                    StartTime = StartTime,
                    Column = Column
                });
            }
        }
    }
}
