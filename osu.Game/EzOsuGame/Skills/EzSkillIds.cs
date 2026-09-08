// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Stable string IDs for skill systems and individual skills.
    /// MSD = chart difficulty axes; SSR = player skill axes; Dan is a separate system.
    /// </summary>
    public static class EzSkillSystems
    {
        public const string BEATMAP_MSD = "beatmap_msd";
        public const string PLAYER_SSR = "player_ssr";
        public const string DAN = "dan";
    }

    public static class EzSkillIds
    {
        public const string OVERALL = "overall";
        public const string STREAM = "stream";
        public const string JUMPSTREAM = "jumpstream";
        public const string HANDSTREAM = "handstream";
        public const string STAMINA = "stamina";
        public const string JACK_SPEED = "jack_speed";
        public const string CHORDJACK = "chordjack";
        public const string TECHNICAL = "technical";

        /// <summary>MinaCalc / Etterna skillset axes (Overall included as its own skill).</summary>
        public static readonly string[] MINA_SKILLSETS =
        {
            OVERALL,
            STREAM,
            JUMPSTREAM,
            HANDSTREAM,
            STAMINA,
            JACK_SPEED,
            CHORDJACK,
            TECHNICAL,
        };

        public static string Msd(string axis) => $"{EzSkillSystems.BEATMAP_MSD}.{axis}";
        public static string Ssr(string axis) => $"{EzSkillSystems.PLAYER_SSR}.{axis}";

        public static string Dan(int keyCount, string side) => $"{EzSkillSystems.DAN}.{keyCount}k.{side}";
    }

    /// <summary>
    /// Pre-release: corrective formula fixes keep this at 1 (no client shipped yet).
    /// After public release, bump when MinaCalc note conversion or aggregation semantics change.
    /// </summary>
    public static class EzManiaSkillAlgorithm
    {
        public const int VERSION = 1;
    }
}
