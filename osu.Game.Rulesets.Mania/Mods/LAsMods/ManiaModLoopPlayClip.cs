// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModLoopPlayClip : ModLoopPlayClip,
                                        IApplicableToDrawableRuleset<ManiaHitObject>,
                                        IApplicableAfterBeatmapConversion
    {
        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModRateAdjust),
            typeof(ModTimeRamp),
            typeof(ModAdaptiveSpeed),
            typeof(ManiaModConstantSpeed),
            typeof(ModNoFail),
        };

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            if (!ConstantSpeed.Value)
                return;

            if (drawableRuleset is DrawableManiaRuleset maniaRuleset)
                maniaRuleset.VisualisationMethod = ScrollVisualisationMethod.Constant;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            maniaBeatmap.Breaks.Clear();

            var (cutTimeStart, cutTimeEnd) = GetEffectiveCutTimeMs();

            double breakTime = BreakTime.Value * 1000;

            // 改为最少一个非空设置
            var minTimeBeatmap = maniaBeatmap.HitObjects.MinBy(h => h.StartTime);
            var maxTimeBeatmap = maniaBeatmap.HitObjects.MaxBy(h => h.GetEndTime());
            cutTimeStart ??= minTimeBeatmap?.StartTime;
            cutTimeEnd ??= maxTimeBeatmap?.GetEndTime();

            // IMPORTANT: compute length only after null defaults have been applied.
            // Otherwise, when both cut times are null (default settings and no global A/B range),
            // this mod would early-return and appear to have no effect (and thus no analysis change).
            double? length = cutTimeEnd - cutTimeStart;

            var selectedPart = maniaBeatmap.HitObjects.Where(h => h.StartTime > cutTimeStart && h.GetEndTime() < cutTimeEnd).ToList();

            if (length is null || length <= 0)
            {
                SetResolvedCut(null, null);
                return;
            }

            SetResolvedCut(cutTimeStart, cutTimeEnd);

            var newPart = new List<ManiaHitObject>();

            for (int timeIndex = 0; timeIndex < LoopCount.Value; timeIndex++)
            {
                if (timeIndex == 0)
                {
                    if (Rand.Value)
                    {
                        var shuffledColumns = Enumerable.Range(0, maniaBeatmap.TotalColumns).OrderBy(_ => rng.Next()).ToList();
                        selectedPart.ForEach(h => h.Column = shuffledColumns[h.Column]);
                    }

                    if (Mirror.Value)
                    {
                    }

                    // 调整时间从切片起点开始
                    foreach (var note in selectedPart)
                    {
                        note.StartTime -= (float)cutTimeStart!;
                        if (note is HoldNote holdNote)
                            holdNote.EndTime -= (float)cutTimeStart;
                    }

                    newPart.AddRange(selectedPart);
                    continue;
                }

                var obj = new List<ManiaHitObject>();

                foreach (var note in selectedPart)
                {
                    if (note.GetEndTime() != note.StartTime)
                    {
                        obj.Add(new HoldNote
                        {
                            Column = note.Column,
                            StartTime = note.StartTime + timeIndex * (breakTime + (double)length),
                            EndTime = note.GetEndTime() + timeIndex * (breakTime + (double)length),
                            NodeSamples = [note.Samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        obj.Add(new Note
                        {
                            Column = note.Column,
                            StartTime = note.StartTime + timeIndex * (breakTime + (double)length),
                            Samples = note.Samples,
                        });
                    }
                }

                if (Rand.Value)
                {
                    var shuffledColumns = Enumerable.Range(0, maniaBeatmap.TotalColumns).OrderBy(_ => rng.Next()).ToList();
                    obj.OfType<ManiaHitObject>().ForEach(h => h.Column = shuffledColumns[h.Column]);
                }

                newPart.AddRange(obj);
            }

            // Ensure derived timing (hold end times, slider spans, etc.) are populated for new objects.
            foreach (var h in newPart)
            {
                try
                {
                    h.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty);
                }
                catch
                {
                }
            }

            maniaBeatmap.HitObjects = newPart;
        }
    }
}
