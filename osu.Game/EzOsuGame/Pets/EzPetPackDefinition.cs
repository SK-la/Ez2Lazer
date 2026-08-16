// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Schema for <c>EzResources/Pets/&lt;pack&gt;/pet.json</c>.
    /// </summary>
    public class EzPetPackDefinition
    {
        public static readonly JsonSerializerSettings JSON_SETTINGS = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public string DefaultState { get; set; } = "idle";

        public Dictionary<string, EzPetClipDefinition> Clips { get; set; } = new Dictionary<string, EzPetClipDefinition>(StringComparer.Ordinal);

        public Dictionary<string, EzPetStateDefinition> States { get; set; } = new Dictionary<string, EzPetStateDefinition>(StringComparer.Ordinal);

        public List<EzPetStarBand> StarBands { get; set; } = new List<EzPetStarBand>();

        public List<EzPetRule> Rules { get; set; } = new List<EzPetRule>();

        public static EzPetPackDefinition Parse(string json)
        {
            var parsed = JsonConvert.DeserializeObject<EzPetPackDefinition>(json, JSON_SETTINGS);
            return parsed ?? new EzPetPackDefinition();
        }

        /// <summary>
        /// Star bands are half-open <c>[min, max)</c>. Returns null when no band matches.
        /// </summary>
        public string? MatchStarBand(double starRating)
        {
            foreach (var band in StarBands)
            {
                if (starRating >= band.Min && starRating < band.Max && !string.IsNullOrEmpty(band.Goto))
                    return band.Goto;
            }

            return null;
        }
    }

    public class EzPetClipDefinition
    {
        /// <summary>
        /// Optional action folder. Default is the clip id (<c>idle</c>, <c>starEasy</c>, …).
        /// Also accepts the clip id in snake_case (<c>star_easy</c>).
        /// </summary>
        public string Folder { get; set; } = string.Empty;

        /// <summary>
        /// Legacy alias for <see cref="Folder"/>. Ignored when it still contains a <c>{00}</c> template.
        /// </summary>
        public string Frames { get; set; } = string.Empty;

        public double Fps { get; set; } = 12;

        public bool Loop { get; set; }
    }

    public class EzPetStateDefinition
    {
        public string Clip { get; set; } = string.Empty;

        /// <summary>
        /// State to enter when a non-looping clip finishes.
        /// </summary>
        public string? Next { get; set; }
    }

    public class EzPetStarBand
    {
        public double Min { get; set; }

        public double Max { get; set; }

        public string Goto { get; set; } = string.Empty;
    }

    public class EzPetRule
    {
        /// <summary>
        /// hover / hoverEnd / click / gameplayEnter / combo / miss / idle
        /// </summary>
        public string When { get; set; } = string.Empty;

        public string Goto { get; set; } = string.Empty;

        public bool Interrupt { get; set; }

        /// <summary>
        /// <c>play</c> (default) switches clip via <see cref="Goto"/>;
        /// <c>show</c> makes the pet visible (optional <see cref="Goto"/>);
        /// <c>hide</c> hides the pet until a later <c>show</c> or leaving gameplay.
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Combo threshold (inclusive). Used when <see cref="When"/> is combo.
        /// </summary>
        public int? At { get; set; }

        /// <summary>
        /// Consecutive miss count. Used when <see cref="When"/> is miss. Absent = every miss.
        /// </summary>
        public int? Streak { get; set; }

        /// <summary>
        /// Idle seconds before this rule can fire. Used when <see cref="When"/> is idle.
        /// </summary>
        public double? AfterSeconds { get; set; }

        public EzPetVisibilityAction ResolveAction()
        {
            if (string.Equals(Action, "hide", StringComparison.OrdinalIgnoreCase))
                return EzPetVisibilityAction.Hide;

            if (string.Equals(Action, "show", StringComparison.OrdinalIgnoreCase))
                return EzPetVisibilityAction.Show;

            return EzPetVisibilityAction.Play;
        }
    }

    public enum EzPetVisibilityAction
    {
        Play,
        Show,
        Hide,
    }
}
