// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Plays a beatmap automatically while enabling the aspire-specific behaviour osu!stable exhibits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aspire maps are built on quirks of osu!stable's parser. Reproducing them in a way that survives normal lazer
    /// play would change the rules for every map, so the parts that alter gameplay only take effect while this mod is
    /// active: sliders whose duration cannot be represented on the timeline collapse to zero length instead of keeping
    /// an unbounded object alive, and their paths are never approximated.
    /// </para>
    /// <para>
    /// Without this mod, such a slider keeps its literal duration and the beatmap behaves exactly as it did before.
    /// </para>
    /// </remarks>
    public class OsuModAspireAutoplay : ModAutoplay, IApplicableAfterBeatmapConversion, IEzApplyOrder
    {
        /// <summary>
        /// How far past the last hitobject a slider may still end and be considered part of the map.
        /// </summary>
        /// <remarks>
        /// Generous enough for a slider that legitimately runs to the end of the audio, while excluding the sliders
        /// aspire maps use to reach days or years past the map.
        /// </remarks>
        public const double END_TIME_MARGIN = 60_000;

        public override string Name => "Aspire Autoplay";

        public override string Acronym => "AA";

        public override IconUsage? Icon => OsuIcon.ModAutoplay;

        public override LocalisableString Description => "Watch a perfect automated play of an aspire map, with its stable-exclusive quirks reproduced.";

        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(ModAutoplay) }).ToArray();

        /// <summary>
        /// Applied before other conversion mods so replacements of <see cref="Slider"/> keep the limit.
        /// </summary>
        public int ApplyOrder => -1;

        public override ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            => new ModReplayData(new OsuAutoGenerator(beatmap, mods).Generate(), new ModCreatedUser { Username = "Aspire" });

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var sliders = beatmap.HitObjects.OfType<Slider>().ToList();

            if (sliders.Count == 0)
                return;

            // Sliders that outlast the map collapse, so the autoplay replay stays proportional to the map rather than
            // to durations measured in days. Everything else keeps its literal duration.
            double lastStartTime = beatmap.HitObjects.Max(h => h.StartTime);
            double limit = lastStartTime + END_TIME_MARGIN;

            foreach (var slider in sliders)
                slider.EndTimeLimit = limit;
        }
    }
}
