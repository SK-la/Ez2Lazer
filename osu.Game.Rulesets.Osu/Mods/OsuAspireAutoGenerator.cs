// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// <see cref="OsuAutoGenerator"/> that keeps aspire sliders trackable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aspire maps contain sliders covering tens of thousands of playfield units within a few hundred milliseconds.
    /// Replay positions are interpolated linearly between frames while the follow circle follows the curve, so at a
    /// tick's judgement time the interpolated cursor can be thousands of units away even though the surrounding frames
    /// sit exactly on the curve. Tracking then reads as false for that sample, the tick is missed, and the resulting
    /// health loss fails the play.
    /// </para>
    /// <para>
    /// Frames are added after generation rather than during it on purpose: while a button is held, generating a
    /// hitobject discards every existing frame within its own time range, which would remove frames added for an
    /// overlapping slider. Densifying every frame instead would also multiply the replay by orders of magnitude on
    /// sliders that have no ticks at all.
    /// </para>
    /// </remarks>
    internal class OsuAspireAutoGenerator : OsuAutoGenerator
    {
        public OsuAspireAutoGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            : base(beatmap, mods)
        {
        }

        public override Replay Generate()
        {
            Replay replay = base.Generate();

            if (Frames.Count < 2)
                return replay;

            foreach (var slider in Beatmap.HitObjects.OfType<Slider>())
            {
                if (slider.Duration <= 0)
                    continue;

                foreach (var nested in slider.NestedHitObjects)
                {
                    if (nested is not SliderTick and not SliderRepeat)
                        continue;

                    addFrameOnCurve(slider, nested.StartTime);
                }
            }

            return replay;
        }

        private void addFrameOnCurve(Slider slider, double time)
        {
            int index = findInsertionIndex(time);

            // Already sampled exactly at this time.
            if (index < Frames.Count && Frames[index].Time == time)
                return;

            double progress = Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1);
            var position = slider.StackedPositionAt(progress);

            var frame = new OsuReplayFrame(time, position);

            if (index > 0 && Frames[index - 1] is OsuReplayFrame previous)
            {
                foreach (var action in previous.Actions)
                    frame.Actions.Add(action);
            }

            Frames.Insert(index, frame);
        }

        /// <summary>Index of the first frame at or after <paramref name="time"/>.</summary>
        private int findInsertionIndex(double time)
        {
            int low = 0;
            int high = Frames.Count;

            while (low < high)
            {
                int middle = (low + high) / 2;

                if (Frames[middle].Time < time)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }
    }
}
