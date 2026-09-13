// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
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
            INotificationOverlay? notifications,
            Action<Action> runOnUpdateThread)
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

            addSkillDataStatus(subsection, backgroundDataStoreProcessor, skillStore, runOnUpdateThread);
        }

        /// <summary>
        /// Read-only status row + refresh button for the chart skill chain. The measurement enumerates
        /// every mania chart plus the three facet tables, so it runs on a background task and only the
        /// formatted line is marshalled back to the update thread.
        /// </summary>
        private static void addSkillDataStatus(
            SettingsSubsection subsection,
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzSkillStore? skillStore,
            Action<Action> runOnUpdateThread)
        {
            var statusText = new Bindable<string>(EzSettingsStrings.SKILL_DATA_STATUS_UNMEASURED);

            var refreshButton = new SettingsButtonV2
            {
                Text = EzSettingsStrings.SKILL_DATA_STATUS_REFRESH,
                TooltipText = EzSettingsStrings.SKILL_DATA_STATUS_REFRESH_TOOLTIP,
                Keywords = skill_data_keywords,
            };

            refreshButton.Enabled.Value = skillStore != null;
            refreshButton.Action = () =>
            {
                if (skillStore == null)
                    return;

                refreshButton.Enabled.Value = false;
                statusText.Value = EzSettingsStrings.SKILL_DATA_STATUS_MEASURING;

                Task.Run(() =>
                {
                    string line;

                    try
                    {
                        line = describeSkillDataStatus(skillStore.GetSkillDataStatus(),
                            backgroundDataStoreProcessor?.IsEzRealmMetadataBackfillRunning == true);
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"[EzSkills] Skill data status measurement failed: {e}",
                            Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                        line = EzSettingsStrings.SKILL_DATA_STATUS_FAILED;
                    }

                    // Marshalled back to the update thread: Realm reads are fine off-thread but the
                    // bindable feeds a drawable.
                    runOnUpdateThread(() =>
                    {
                        statusText.Value = line;
                        refreshButton.Enabled.Value = true;
                    });
                });
            };

            subsection.Add(refreshButton);

            subsection.Add(new SettingsItemV2(new FormTextBox
            {
                Caption = EzSettingsStrings.SKILL_DATA_STATUS,
                HintText = EzSettingsStrings.SKILL_DATA_STATUS_TOOLTIP,
                ReadOnly = true,
                Current = statusText,
            })
            {
                Keywords = skill_data_keywords,
            });
        }

        private static string describeSkillDataStatus(EzSkillDataStatus status, bool backfillRunning)
        {
            string describeFacet(string name, EzFacetStatus facet)
                => $"{name} {EzSettingsStrings.SKILL_DATA_STATUS_READY} {facet.Ready}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_UNRATEABLE} {facet.Unrateable}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_STALE} {facet.Stale}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_MISSING} {facet.Missing}";

            string pending = status.HasWorkToDo
                ? $"{EzSettingsStrings.SKILL_DATA_STATUS_PENDING} {status.TotalPending}"
                : EzSettingsStrings.SKILL_DATA_STATUS_ALL_CURRENT;

            string line = $"{EzSettingsStrings.SKILL_DATA_STATUS_CHARTS} {status.TotalCharts}"
                          + $" · {describeFacet("MSD", status.Msd)}"
                          + $" · {describeFacet("CSI", status.ChartSkillInfo)}"
                          + $" · {describeFacet("Dan", status.ChartDan)}"
                          + $" · {pending}";

            if (backfillRunning)
                line += EzSettingsStrings.SKILL_DATA_STATUS_RUNNING;

            return line;
        }
    }
}
