// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>Track Skills for osu: PP+-shaped player axes, no key chips or Dan.</summary>
    public sealed class PPPSkillsPresenter : IEzTrackSkillsPresenter
    {
        private const double display_min = 0.01;

        private readonly EzSkillProvider provider;
        private readonly int rulesetOnlineId;

        public PPPSkillsPresenter(EzSkillProvider provider, int rulesetOnlineId)
        {
            this.provider = provider;
            this.rulesetOnlineId = rulesetOnlineId;
        }

        public EzAnalysisSystem System => EzAnalysisSystem.PPP;

        public bool HasSliceChips => slicedByKeyMode;

        public bool HasSidePanel => false;

        /// <summary>
        /// Whether this ruleset's PP+ rows are stored per keymode.
        /// </summary>
        /// <remarks>
        /// Mania's PP+ axes are keyed by the column count a play was rated at, exactly like its Sunny rows; the other
        /// rulesets have one slice per player. The read has to ask for the same slice the write used.
        /// </remarks>
        private bool slicedByKeyMode => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        public IReadOnlyList<int> GetSliceKeys(string username)
        {
            if (slicedByKeyMode)
                return provider.GetPlayerKeyCounts(username, System, rulesetOnlineId);

            var skills = provider.GetPlayerSystemSkills(username, EzAxisIds.UNSLICED_KEY, System, rulesetOnlineId);
            return skills.Count > 0
                ? new[] { EzAxisIds.UNSLICED_KEY }
                : Array.Empty<int>();
        }

        public EzTrackSkillsSnapshot Load(string username, int sliceKey)
        {
            // Values and row metadata come from one read; the panel only ever needs them together.
            var slice = provider.GetPlayerSystemSkillSlice(username, sliceKey, System, rulesetOnlineId);
            var skills = slice.Values;

            if (skills.Count == 0)
                return EzTrackSkillsSnapshot.Empty;

            var meta = slice.Meta;

            // Axes come from the ruleset's own catalog, not from a hard-coded set: PP+ declares a different metric
            // list per ruleset (osu aim/tapping, taiko rhythm/colour, catch movement, mania strain).
            var specs = EzPPPSkillCatalog.MetricsFor(rulesetOnlineId);

            var radar = new List<EzTrackSkillsAxis>(specs.Count);
            var bars = new List<EzTrackSkillsAxis>(specs.Count);

            foreach (var spec in specs)
            {
                string skillId = spec.PlayerSkillId;
                double value = skills.GetValueOrDefault(skillId, 0);

                if (!double.IsFinite(value) || value < display_min)
                    continue;

                var entry = new EzTrackSkillsAxis(skillId, spec.DisplayName, value, spec.AccentHex);
                bars.Add(entry);

                if (spec.InRadar)
                    radar.Add(entry);
            }

            // The headline is the metric the catalog declares as the summary for this ruleset: osu's aim, and the
            // star rating for the others. Falling back to the largest bar keeps a partial row from showing zero.
            string overallId = EzPPPSkillCatalog.SummarySkillId(rulesetOnlineId);
            double overall = skills.GetValueOrDefault(overallId, 0);
            if (!double.IsFinite(overall) || overall < display_min)
                overall = bars.Select(static a => a.Value).DefaultIfEmpty(0).Max();

            return new EzTrackSkillsSnapshot
            {
                HeaderTitle = EzSettingsProfile.LOCAL_PROFILE_SKILL_RATING,
                Overall = overall,
                PlaysAnalyzed = meta.AnalyzedPlays,
                Provisional = meta.Provisional,
                Stale = meta.Stale,
                RadarAxes = radar,
                BarAxes = bars,
            };
        }

        public LocalisableString ResolveDisplayName(string skillId)
        {
            foreach (var spec in EzPPPSkillCatalog.MetricsFor(rulesetOnlineId))
            {
                if (string.Equals(spec.PlayerSkillId, skillId, StringComparison.Ordinal))
                    return spec.DisplayName;
            }

            if (EzOsuSkillAxisExtensions.TryParse(skillId, out var axis))
                return axis.Chip().Name;

            return skillId;
        }

        public void ConfigureSidePanel(EzHUDDanDualPanel danPanel, BindableInt selectedSlice, string username)
        {
        }
    }
}
