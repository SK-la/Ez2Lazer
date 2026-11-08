// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.Pattern;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>Track Skills for mania: key chips, SunnySR/pattern axes, Dan DualPanel.</summary>
    public sealed class ManiaTrackSkillsPresenter : IEzTrackSkillsPresenter
    {
        private readonly EzSkillProvider provider;

        public ManiaTrackSkillsPresenter(EzSkillProvider provider)
        {
            this.provider = provider;
        }

        public string SystemId => EzSkillSystems.PLAYER_MINA;

        public bool HasSliceChips => true;

        public bool HasSidePanel => true;

        public IReadOnlyList<int> GetSliceKeys(string username)
            => provider.GetPlayerSsrKeyCounts(username);

        public EzTrackSkillsSnapshot Load(string username, int sliceKey)
        {
            if (sliceKey <= 0)
                return EzTrackSkillsSnapshot.Empty;

            var ssr = provider.GetPlayerSsrSnapshot(username, sliceKey);
            var modeEntries = provider.GetSkillModeEntries(username, sliceKey);

            var radar = modeEntries
                        .Select(e => new EzTrackSkillsAxis(e.SkillId, e.DisplayName, e.Value, e.AccentHex))
                        .ToList();

            var bars = new List<EzTrackSkillsAxis>(radar.Count + 1);

            if (ssr.Overall >= EzPatternRatings.DISPLAY_MIN)
            {
                var overallMeta = EzMinaSkillAxis.Overall.Meta();
                bars.Add(new EzTrackSkillsAxis(
                    EzMinaSkillAxis.Overall.ToSsrSkillId(),
                    overallMeta.DisplayName,
                    ssr.Overall,
                    overallMeta.AccentHex));
            }

            bars.AddRange(radar);

            return new EzTrackSkillsSnapshot
            {
                HeaderTitle = EzSettingsProfile.LOCAL_PROFILE_SKILL_RATING.Format(sliceKey),
                Overall = ssr.Overall,
                PlaysAnalyzed = ssr.AnalyzedPlays,
                Provisional = ssr.Provisional,
                Stale = ssr.Stale,
                RadarAxes = radar,
                BarAxes = bars,
            };
        }

        public LocalisableString ResolveDisplayName(string skillId)
        {
            if (EzPatternRatings.TryParseSkillId(skillId, out string patternId)
                && EzManiaRCAxisExtensions.TryParse(patternId, out var patternAxis))
            {
                return patternAxis.Meta().DisplayName;
            }

            return EzMinaSkillAxisExtensions.TryParse(skillId, out var axis)
                ? axis.Chip().Name
                : skillId;
        }

        public void ConfigureSidePanel(EzHUDDanDualPanel danPanel, BindableInt selectedSlice, string username)
        {
            danPanel.TargetUsername.UnbindBindings();
            danPanel.TargetUsername.Value = username;
            danPanel.KeyCount.BindTo(selectedSlice);
            danPanel.DataSource.Value = EzDanPanelDataSource.Player;
            danPanel.DualLayout.Value = EzDanPanelDualLayout.Auto;
            danPanel.ShowClearCounts.Value = true;
        }
    }
}
