// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public static partial class EzDataRebuildSettingsSection
    {
        private static readonly string[] skill_data_keywords =
        {
            "realm", "msd", "csi", "dan", "skill", "status", "stale", "missing", "unrateable",
            "技能", "状态", "缺失", "过期", "补算",
        };

        public static void AddTo(
            SettingsSubsection subsection,
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzAnalysisWarmupProcessor? analysisWarmupProcessor,
            EzSkillStore? skillStore,
            IDialogOverlay? dialogOverlay,
            INotificationOverlay? notifications)
        {
            var rebuildTarget = new Bindable<EzDataRebuildTarget>(EzDataRebuildTarget.RealmChartSkillChain);
            var maintenanceHandler = new EzDataRebuildMaintenanceHandler(backgroundDataStoreProcessor, analysisWarmupProcessor, dialogOverlay, notifications);

            var executeButton = new DangerousSettingsButtonV2
            {
                Text = EzSettingsStrings.DATA_REBUILD_EXECUTE,
                TooltipText = EzSettingsStrings.DATA_REBUILD_EXECUTE_TOOLTIP,
                Keywords = new[] { "realm", "tag", "xxy", "pp", "msd", "skill", "metadata", "backfill", "force", "recalculate", "sqlite", "rebuild", "maintenance", "execute", "score", "成绩" },
            };

            void updateExecuteButtonState()
            {
                executeButton.Enabled.Value = maintenanceHandler.CanExecute(rebuildTarget.Value);
            }

            executeButton.Action = () => maintenanceHandler.RequestExecute(rebuildTarget.Value);
            rebuildTarget.BindValueChanged(_ => updateExecuteButtonState(), true);

            subsection.Add(new SettingsItemV2(new FormEnumDropdown<EzDataRebuildTarget>
            {
                Caption = EzSettingsStrings.DATA_REBUILD_TARGET,
                HintText = EzSettingsStrings.DATA_REBUILD_TARGET_TOOLTIP,
                Current = rebuildTarget,
            })
            {
                Keywords = new[] { "realm", "tag", "xxy", "pp", "msd", "skill", "metadata", "backfill", "force", "recalculate", "sqlite", "rebuild", "maintenance", "score", "成绩" }
            });

            subsection.Add(executeButton);

            addSkillDataStatus(subsection, backgroundDataStoreProcessor, skillStore);
        }

        /// <summary>
        /// Note readout + refresh button for the chart skill chain. The readout measures itself as soon as the
        /// settings panel is shown; the button only re-measures on demand (e.g. once a backfill has finished).
        /// </summary>
        private static void addSkillDataStatus(
            SettingsSubsection subsection,
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzSkillStore? skillStore)
        {
            var statusNote = new EzSkillDataStatusNote(skillStore, backgroundDataStoreProcessor);

            var refreshButton = new SettingsButtonV2
            {
                Text = EzSettingsStrings.SKILL_DATA_STATUS_REFRESH,
                TooltipText = EzSettingsStrings.SKILL_DATA_STATUS_REFRESH_TOOLTIP,
                Keywords = skill_data_keywords,
            };

            refreshButton.Enabled.Value = skillStore != null;
            refreshButton.Action = statusNote.Measure;

            // The note also measures on load; keep the button disabled while a measurement is in flight so the
            // manual pass cannot stack on top of it.
            statusNote.Measuring.BindValueChanged(m => refreshButton.Enabled.Value = skillStore != null && !m.NewValue);

            subsection.Add(refreshButton);
            subsection.Add(statusNote);
        }
    }
}
