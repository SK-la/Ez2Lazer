// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Screens.Select;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// Bridges <see cref="BmsLampStore"/> into osu.Game's panel accent system: when
    /// SongSelect renders a BMS panel it asks this provider for the accent colour,
    /// and we return the lamp colour for that beatmap. Non-BMS panels return null
    /// so they keep the default star-rating colour.
    /// </summary>
    /// <remarks>
    /// This type is optional at the engine level: if a fork omits <see cref="osu.Game.Screens.Select.IPanelAccentColourProvider"/>
    /// from panels or never caches a provider, behaviour degrades to vanilla star-rating accents only.
    /// BMS gameplay does not reference this class.
    /// </remarks>
    public class BmsLampAccentColourProvider : IPanelAccentColourProvider
    {
        private const string bms_ruleset_short_name = "bms";

        private readonly BmsLampStore lampStore;

        public BmsLampAccentColourProvider(BmsLampStore lampStore)
        {
            this.lampStore = lampStore;
        }

        public Color4? GetAccentColourFor(BeatmapInfo? beatmap)
        {
            if (beatmap?.Ruleset == null)
                return null;

            if (!string.Equals(beatmap.Ruleset.ShortName, bms_ruleset_short_name, System.StringComparison.OrdinalIgnoreCase))
                return null;

            var lamp = lampStore.GetLamp(beatmap);
            return lampStore.Scheme.GetLampColour(lamp);
        }
    }
}
