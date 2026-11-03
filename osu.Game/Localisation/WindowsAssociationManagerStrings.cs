// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class WindowsAssociationManagerStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.WindowsAssociationManager";

        /// <summary>
        /// "Beatmap"
        /// </summary>
        public static LocalisableString OsuBeatmap => new TranslatableString(getKey(@"osu_beatmap"), @"Beatmap");

        /// <summary>
        /// "Replay"
        /// </summary>
        public static LocalisableString OsuReplay => new TranslatableString(getKey(@"osu_replay"), @"Replay");

        /// <summary>
        /// "Skin"
        /// </summary>
        public static LocalisableString OsuSkin => new TranslatableString(getKey(@"osu_skin"), @"Skin");

        /// <summary>
        /// "Ez2Lazer"
        /// </summary>
        public static LocalisableString OsuProtocol => new TranslatableString(getKey(@"osu_protocol"), @"Ez2Lazer");

        /// <summary>
        /// "Multiplayer"
        /// </summary>
        public static LocalisableString OsuMultiplayer => new TranslatableString(getKey(@"osu_multiplayer"), @"Multiplayer");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}