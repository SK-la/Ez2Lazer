// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Ez layout JSON file names. A file is not a <see cref="GlobalSkinnableContainers"/> value.
    /// The three names that already exist as skin areas still map back onto that enum so the
    /// container can line up with the screen's skin lookup.
    /// </summary>
    public static class EzLayoutFiles
    {
        public const string SONG_SELECT = "SongSelect.json";

        public const string MAIN_HUD = "MainHUDComponents.json";

        public const string PLAYFIELD = "Playfield.json";

        public static GlobalSkinnableContainerLookup? SkinLookup(string layoutFile, RulesetInfo? ruleset)
        {
            GlobalSkinnableContainers? area = layoutFile switch
            {
                SONG_SELECT => GlobalSkinnableContainers.SongSelect,
                MAIN_HUD => GlobalSkinnableContainers.MainHUDComponents,
                PLAYFIELD => GlobalSkinnableContainers.Playfield,
                _ => null,
            };

            return area is GlobalSkinnableContainers known
                ? new GlobalSkinnableContainerLookup(known, ruleset)
                : null;
        }
    }
}
