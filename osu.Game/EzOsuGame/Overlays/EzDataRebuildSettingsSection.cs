// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Database;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public static partial class EzDataRebuildSettingsSection
    {
        private static readonly string[] rebuild_keywords =
        {
            "realm", "sqlite", "msd", "csi", "dan", "skill", "sunny", "ppp", "pp", "tags", "rebuild", "maintenance", "backfill",
            "技能", "数据", "维护", "补算", "重算",
        };

        private static readonly string[] skill_data_keywords =
        {
            "realm", "msd", "csi", "dan", "skill", "status", "stale", "missing", "unrateable",
            "official", "sunny", "ssr", "ppp", "mina", "analysis", "ruleset",
            "技能", "分析", "状态", "缺失", "过期", "补算", "规则集",
        };

        public static void AddTo(
            SettingsSubsection subsection,
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzChartSkillStore? skillStore,
            EzPlayerSkillStore? playerStore,
            EzLocalProfileStore? localProfileStore = null,
            EzDataRebuildDialog? rebuildDialog = null)
        {
            subsection.Add(new SettingsButtonV2
            {
                Text = EzSettingsStrings.DATA_REBUILD_OPEN,
                TooltipText = EzSettingsStrings.DATA_REBUILD_OPEN_TOOLTIP,
                Keywords = rebuild_keywords,
                Action = () => rebuildDialog?.ShowMaintenance(),
            });

            addSkillDataStatus(subsection, backgroundDataStoreProcessor, skillStore, playerStore, localProfileStore);
        }

        /// <summary>
        /// Note readout + refresh button for the persisted analysis data of every ruleset. Measurement runs only when
        /// the user taps refresh.
        /// </summary>
        private static void addSkillDataStatus(
            SettingsSubsection subsection,
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzChartSkillStore? skillStore,
            EzPlayerSkillStore? playerStore,
            EzLocalProfileStore? localProfileStore)
        {
            var statusNote = new EzSkillDataStatusNote(skillStore, playerStore, backgroundDataStoreProcessor, localProfileStore);

            var refreshButton = new SettingsButtonV2
            {
                Text = EzSettingsStrings.SKILL_DATA_STATUS_REFRESH,
                TooltipText = EzSettingsStrings.SKILL_DATA_STATUS_REFRESH_TOOLTIP,
                Keywords = skill_data_keywords,
            };

            refreshButton.Enabled.Value = skillStore != null;
            refreshButton.Action = statusNote.Measure;

            // Keep the button disabled while a measurement is in flight.
            statusNote.Measuring.BindValueChanged(m => refreshButton.Enabled.Value = skillStore != null && !m.NewValue);

            subsection.Add(refreshButton);
            subsection.Add(statusNote);
        }
    }
}
