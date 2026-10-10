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

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>Track Skills for osu: PP+-shaped player axes, no key chips or Dan.</summary>
    public sealed class OsuTrackSkillsPresenter : IEzTrackSkillsPresenter
    {
        private const double display_min = 0.01;

        private readonly EzSkillProvider provider;

        public OsuTrackSkillsPresenter(EzSkillProvider provider)
        {
            this.provider = provider;
        }

        public string SystemId => EzSkillSystems.PLAYER_PPPLUS;

        public bool HasSliceChips => false;

        public bool HasSidePanel => false;

        public IReadOnlyList<int> GetSliceKeys(string username)
        {
            var skills = provider.GetPlayerSystemSkills(username, EzSkillSystems.OSU_SLICE_KEY, SystemId);
            return skills.Count > 0
                ? new[] { EzSkillSystems.OSU_SLICE_KEY }
                : Array.Empty<int>();
        }

        public EzTrackSkillsSnapshot Load(string username, int sliceKey)
        {
            var skills = provider.GetPlayerSystemSkills(username, sliceKey, SystemId);
            if (skills.Count == 0)
                return EzTrackSkillsSnapshot.Empty;

            var meta = provider.GetPlayerSystemSkillMeta(username, sliceKey, SystemId);

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

            double overall = skills.GetValueOrDefault(EzOsuSkillAxis.Aim.ToPlayerSkillId(), 0);
            if (!double.IsFinite(overall) || overall < display_min)
                overall = bars.Select(static a => a.Value).DefaultIfEmpty(0).Max();

            return new EzTrackSkillsSnapshot
            {
                HeaderTitle = EzSettingsProfile.LOCAL_PROFILE_OSU_SKILL_RATING,
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
            foreach (var def in provider.Registry.GetSystem(SystemId)?.Skills
                                ?? Array.Empty<EzSkillDefinition>())
            {
                if (def.SkillId == skillId)
                    return def.DisplayName;
            }

            return EzOsuSkillAxisExtensions.TryParse(skillId, out var axis)
                ? axis.Chip().Name
                : skillId;
        }

        public void ConfigureSidePanel(EzHUDDanDualPanel danPanel, BindableInt selectedSlice, string username)
        {
        }
    }
}