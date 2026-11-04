// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Per-skill algorithm version for PPP rows. Bump one constant to retire that skill id only.
    /// <see cref="EzOsuSkillAlgorithm.VERSION"/> stays unchanged.
    /// </summary>
    public static class EzPppSkillVersions
    {
        /// <summary>Facet stamp for a chart whose required skills are all at <see cref="Current"/>.</summary>
        public const int FACET_REVISION = 1;

        public const int CHART_AIM = 1;
        public const int CHART_JUMP_AIM = 1;
        public const int CHART_FLOW_AIM = 1;
        public const int CHART_PRECISION = 1;
        public const int CHART_SPEED = 1;
        public const int CHART_STAMINA = 1;
        public const int CHART_ACCURACY = 1;
        public const int CHART_STAR_RATING = 1;
        public const int CHART_ACCURACY_DIFFICULTY = 1;
        public const int CHART_RHYTHM = 1;
        public const int CHART_COLOUR = 1;
        public const int CHART_MOVEMENT = 1;
        public const int CHART_STRAIN = 1;

        public const int PLAYER_AIM = 1;
        public const int PLAYER_JUMP_AIM = 1;
        public const int PLAYER_FLOW_AIM = 1;
        public const int PLAYER_PRECISION = 1;
        public const int PLAYER_SPEED = 1;
        public const int PLAYER_STAMINA = 1;
        public const int PLAYER_ACCURACY = 1;
        public const int PLAYER_STAR_RATING = 1;
        public const int PLAYER_ACCURACY_DIFFICULTY = 1;
        public const int PLAYER_RHYTHM = 1;
        public const int PLAYER_COLOUR = 1;
        public const int PLAYER_MOVEMENT = 1;
        public const int PLAYER_STRAIN = 1;

        public static int Current(string skillId)
        {
            if (!EzPppSkillCatalog.TrySplit(skillId, out bool player, out string metric))
                return 0;

            if (player)
            {
                return metric switch
                {
                    "aim" => PLAYER_AIM,
                    "jump_aim" => PLAYER_JUMP_AIM,
                    "flow_aim" => PLAYER_FLOW_AIM,
                    "precision" => PLAYER_PRECISION,
                    "speed" => PLAYER_SPEED,
                    "stamina" => PLAYER_STAMINA,
                    "accuracy" => PLAYER_ACCURACY,
                    "star_rating" => PLAYER_STAR_RATING,
                    "accuracy_difficulty" => PLAYER_ACCURACY_DIFFICULTY,
                    "rhythm" => PLAYER_RHYTHM,
                    "colour" => PLAYER_COLOUR,
                    "movement" => PLAYER_MOVEMENT,
                    "strain" => PLAYER_STRAIN,
                    _ => 0,
                };
            }

            return metric switch
            {
                "aim" => CHART_AIM,
                "jump_aim" => CHART_JUMP_AIM,
                "flow_aim" => CHART_FLOW_AIM,
                "precision" => CHART_PRECISION,
                "speed" => CHART_SPEED,
                "stamina" => CHART_STAMINA,
                "accuracy" => CHART_ACCURACY,
                "star_rating" => CHART_STAR_RATING,
                "accuracy_difficulty" => CHART_ACCURACY_DIFFICULTY,
                "rhythm" => CHART_RHYTHM,
                "colour" => CHART_COLOUR,
                "movement" => CHART_MOVEMENT,
                "strain" => CHART_STRAIN,
                _ => 0,
            };
        }

        public static Dictionary<string, int> For(IEnumerable<string> skillIds)
        {
            ArgumentNullException.ThrowIfNull(skillIds);

            var versions = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string skillId in skillIds)
            {
                int version = Current(skillId);

                if (version > 0)
                    versions[skillId] = version;
            }

            return versions;
        }
    }
}
