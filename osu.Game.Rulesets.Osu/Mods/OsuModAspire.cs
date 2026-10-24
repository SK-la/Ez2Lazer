// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Plays an aspire beatmap the way osu!stable does, either watched or played by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aspire maps are built on quirks of osu!stable's parser. Reproducing them in a way that survives normal lazer
    /// play would change the rules for every map, so the parts that alter gameplay only take effect while this mod is
    /// active: sliders ending far past the map collapse to zero length instead of keeping an unbounded object alive,
    /// and their paths are never approximated.
    /// </para>
    /// <para>
    /// <see cref="Autoplay"/> selects between watching a generated replay and playing the beatmap directly. Without this
    /// mod, such sliders keep their literal duration and the beatmap behaves exactly as it did before.
    /// </para>
    /// </remarks>
    public class OsuModAspire : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder, ICreateReplayData, IConditionalReplayGeneration
    {
        /// <summary>
        /// How far past the last hitobject a slider may still end and be considered part of the map.
        /// </summary>
        /// <remarks>
        /// Generous enough for a slider that legitimately runs to the end of the audio, while excluding the sliders
        /// aspire maps use to reach days or years past the map.
        /// </remarks>
        public const double END_TIME_MARGIN = 60_000;

        public override string Name => "Aspire";

        public override string Acronym => "ASP";

        public override IconUsage? Icon => OsuIcon.ModAutoplay;

        public override ModType Type => ModType.Automation;

        public override LocalisableString Description => "Play aspire maps the way osu!stable loads them.";

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => false;

        public override bool ValidForMultiplayerAsFreeMod => false;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAutoplay),
            typeof(ModCinema),
            typeof(ModRelax),
            typeof(ModAdaptiveSpeed),
        };

        [SettingSource("Autoplay", "Watch a perfect automated play instead of playing the beatmap yourself.")]
        public BindableBool Autoplay { get; } = new BindableBool(true);

        /// <summary>
        /// Applied before other conversion mods so replacements of <see cref="Slider"/> keep the limit.
        /// </summary>
        public int ApplyOrder => -1;

        public bool ReplayGenerationEnabled => Autoplay.Value;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var sliders = beatmap.HitObjects.OfType<Slider>().ToList();

            if (sliders.Count == 0)
                return;

            // Sliders that outlast the map collapse, so an autoplay replay stays proportional to the map rather than
            // to durations measured in days. Everything else keeps its literal duration.
            double lastStartTime = beatmap.HitObjects.Max(h => h.StartTime);
            double limit = lastStartTime + END_TIME_MARGIN;

            foreach (var slider in sliders)
                slider.EndTimeLimit = limit;
        }

        public ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            => new ModReplayData(new OsuAspireAutoGenerator(beatmap, mods).Generate(), new ModCreatedUser { Username = "Aspire" });
    }
}
