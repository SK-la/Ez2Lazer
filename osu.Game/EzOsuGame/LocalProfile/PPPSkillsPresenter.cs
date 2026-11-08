// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
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

        public string SystemId => EzSkillSystems.PLAYER_PPP;

        public bool HasSliceChips => false;

        public bool HasSidePanel => false;

        public IReadOnlyList<int> GetSliceKeys(string username)
        {
            var skills = provider.GetPlayerSystemSkills(username, EzSkillSystems.UNSLICED_KEY, SystemId, rulesetOnlineId);
            return skills.Count > 0
                ? new[] { EzSkillSystems.UNSLICED_KEY }
                : Array.Empty<int>();
        }

        public EzTrackSkillsSnapshot Load(string username, int sliceKey)
        {
            // Values and row metadata come from one read; the panel only ever needs them together.
            var slice = provider.GetPlayerSystemSkillSlice(username, sliceKey, SystemId, rulesetOnlineId);
            var skills = slice.Values;

            if (skills.Count == 0)
                return EzTrackSkillsSnapshot.Empty;

            var meta = slice.Meta;

            var radar = new List<EzTrackSkillsAxis>(EzOsuSkillAxisExtensions.RadarAxes.Length);
            var bars = new List<EzTrackSkillsAxis>(EzOsuSkillAxisExtensions.All.Length);

            foreach (var axis in EzOsuSkillAxisExtensions.All)
            {
                string skillId = axis.ToPlayerSkillId();
                double value = skills.GetValueOrDefault(skillId, 0);
                if (!double.IsFinite(value) || value < display_min)
                    continue;

                var chip = axis.Chip();
                var entry = new EzTrackSkillsAxis(skillId, chip.Name, value, chip.AccentHex);
                bars.Add(entry);

                if (axis.Meta().InRadar)
                    radar.Add(entry);
            }

            double overall = skills.GetValueOrDefault(EzPPPSkillAxis.Aim.ToPlayerSkillId(), 0);
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
            if (EzOsuSkillAxisExtensions.TryParse(skillId, out var axis))
                return axis.Chip().Name;

            return skillId;
        }

        public void ConfigureSidePanel(EzHUDDanDualPanel danPanel, BindableInt selectedSlice, string username)
        {
        }
    }
}
