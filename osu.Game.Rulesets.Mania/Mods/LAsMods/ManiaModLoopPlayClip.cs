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
using osu.Game.LAsEzExtensions.Configuration;
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
                                        IApplicableAfterBeatmapConversion, IHasApplyOrder
    {
        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModRateAdjust),
            typeof(ModTimeRamp),
            typeof(ModAdaptiveSpeed),
            typeof(ManiaModConstantSpeed),
            typeof(ModNoFail),
        };

        public int ApplyOrder => 1000;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                // 复用基础设置并补充一些本规则集特有显示
                foreach (var (s, v) in base.SettingDescription)
                    yield return (s, v);

                // 基类中未包含的额外信息
                // Seed 可能为 null
                yield return ((LocalisableString)"Seed", Seed.Value.HasValue ? Seed.Value.Value.ToString() : "None");
                yield return ((LocalisableString)"Randomize Columns", (Rand.Value ? "On" : "Off"));
                yield return ((LocalisableString)"Mirror", (Mirror.Value ? "On" : "Off"));
            }
        }

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

            var (cutTimeStart, cutTimeEnd, length) = ResolveSliceTimesForBeatmap(beatmap);

            double breakTime;

            try
            {
                var timing = beatmap.ControlPointInfo.TimingPointAt(cutTimeStart);
                double quarterMs = timing.BeatLength / 4.0;
                breakTime = quarterMs * Math.Max(1, BreakQuarter.Value);
            }
            catch
            {
                breakTime = 250 * Math.Max(1, BreakQuarter.Value);
            }

            var cutObjectList = maniaBeatmap.HitObjects.Where(h => h.StartTime > cutTimeStart && h.GetEndTime() < cutTimeEnd).ToList();

            var newPart = new List<ManiaHitObject>();

            for (int timeIndex = 0; timeIndex < LoopCount.Value; timeIndex++)
            {
                double offset = timeIndex * (breakTime + length);

                // 统一基准偏移：将原始对象时间相对于切片起点平移，然后根据循环索引叠加循环间隔
                double baseOffset = offset - cutTimeStart;

                var obj = new List<ManiaHitObject>();

                foreach (var note in cutObjectList)
                {
                    if (note.GetEndTime() != note.StartTime)
                    {
                        var hold = new HoldNote
                        {
                            Column = note.Column,
                            StartTime = note.StartTime + (float)baseOffset,
                            EndTime = note.GetEndTime() + (float)baseOffset,
                        };

                        // 尝试复制样本信息（若存在）以保持音效一致性
                        try
                        {
                            if (note is HoldNote hn && hn.NodeSamples != null)
                                hold.NodeSamples = hn.NodeSamples.Select(n => (IList<HitSampleInfo>)n.ToList()).ToList();
                        }
                        catch
                        {
                        }

                        obj.Add(hold);
                    }
                    else
                    {
                        var n = new Note
                        {
                            Column = note.Column,
                            StartTime = note.StartTime + (float)baseOffset,
                            Samples = note.Samples,
                        };

                        obj.Add(n);
                    }
                }

                if (Rand.Value)
                {
                    var shuffledColumns = Enumerable.Range(0, maniaBeatmap.TotalColumns).OrderBy(_ => rng.Next()).ToList();
                    obj.OfType<ManiaHitObject>().ForEach(h => h.Column = shuffledColumns[h.Column]);
                }

                if (Mirror.Value)
                {
                    // 保留原有行为：如需实现镜像，请在此添加逻辑
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
