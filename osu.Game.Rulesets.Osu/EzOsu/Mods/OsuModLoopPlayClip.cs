// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.EzOsu.Mods
{
    public class OsuModLoopPlayClip : ModLoopPlayClip,
                                      IApplicableAfterBeatmapConversion
    {
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);

            var osuBeatmap = (OsuBeatmap)beatmap;

            osuBeatmap.Breaks.Clear();

            double breakTime = BreakTime.Value * 1000;

            var (cutTimeStart, cutTimeEnd, length) = ResolveSliceTimesForBeatmap(beatmap);

            var selectedPart = osuBeatmap.HitObjects.Where(h => h.StartTime > cutTimeStart && h.GetEndTime() < cutTimeEnd).ToList();

            var newPart = new List<OsuHitObject>();

            for (int timeIndex = 0; timeIndex < LoopCount.Value; timeIndex++)
            {
                double offset = timeIndex * (breakTime + length);

                foreach (var note in selectedPart)
                {
                    double baseOffset = offset - cutTimeStart;

                    if (note is HitCircle circle)
                    {
                        var nc = new HitCircle
                        {
                            StartTime = circle.StartTime + baseOffset,
                            Samples = circle.Samples.ToList(),
                            Position = circle.Position,
                        };

                        newPart.Add(nc);
                    }
                    else if (note is Slider slider)
                    {
                        var ns = new Slider
                        {
                            StartTime = slider.StartTime + baseOffset,
                            Path = slider.Path,
                            RepeatCount = slider.RepeatCount,
                            Samples = slider.Samples.ToList(),
                            Position = slider.Position,
                            SliderVelocityMultiplier = slider.SliderVelocityMultiplier,
                        };

                        if (slider.NodeSamples != null)
                            ns.NodeSamples = slider.NodeSamples.Select(n => (IList<HitSampleInfo>)n.ToList()).ToList();

                        newPart.Add(ns);
                    }
                    else if (note is Spinner spinner)
                    {
                        var ns = new Spinner
                        {
                            StartTime = spinner.StartTime + baseOffset,
                            Duration = spinner.Duration,
                        };

                        newPart.Add(ns);
                    }
                    else
                    {
                        // fallback: attempt to create a shallow instance of the same type and copy StartTime/Samples
                        var type = note.GetType();

                        try
                        {
                            var inst = (OsuHitObject?)Activator.CreateInstance(type);

                            if (inst != null)
                            {
                                inst.StartTime = note.StartTime + baseOffset;
                                inst.Samples = note.Samples.ToList();
                                newPart.Add(inst);
                            }
                        }
                        catch
                        {
                            // ignore and fallback to skipping
                        }

                        // As a last resort, skip adding this unknown object to avoid corrupting timing data.
                    }
                }
            }

            // Ensure derived timing (slider velocity/endtime, nested objects) are populated
            foreach (var h in newPart)
            {
                try
                {
                    h.ApplyDefaults(osuBeatmap.ControlPointInfo, osuBeatmap.Difficulty);
                }
                catch
                {
                    // Ignore per-object apply failures to avoid breaking the whole mod application.
                }
            }

            osuBeatmap.HitObjects = newPart;
        }
    }
}
