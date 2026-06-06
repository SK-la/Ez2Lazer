// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzDataRebuildSettingsSection : FillFlowContainer
    {
        public EzDataRebuildSettingsSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
        }

        [BackgroundDependencyLoader]
        private void load(
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzAnalysisWarmupProcessor? analysisWarmupProcessor,
            IDialogOverlay? dialogOverlay,
            INotificationOverlay? notifications)
        {
            var rebuildTarget = new Bindable<EzDataRebuildTarget>(EzDataRebuildTarget.RealmAll);
            var maintenanceHandler = new EzDataRebuildMaintenanceHandler(backgroundDataStoreProcessor, analysisWarmupProcessor, dialogOverlay, notifications);

            var executeButton = new DangerousSettingsButton
            {
                Text = EzSettingsStrings.DATA_REBUILD_EXECUTE,
                TooltipText = EzSettingsStrings.DATA_REBUILD_EXECUTE_TOOLTIP,
                Keywords = new[] { "realm", "tag", "xxy", "pp", "metadata", "backfill", "force", "recalculate", "sqlite", "rebuild", "maintenance", "execute" },
            };

            void updateExecuteButtonState()
            {
                executeButton.Enabled.Value = maintenanceHandler.CanExecute(rebuildTarget.Value);
            }

            executeButton.Action = () => maintenanceHandler.RequestExecute(rebuildTarget.Value);
            rebuildTarget.BindValueChanged(_ => updateExecuteButtonState(), true);

            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<EzDataRebuildTarget>
                {
                    Caption = EzSettingsStrings.DATA_REBUILD_TARGET,
                    HintText = EzSettingsStrings.DATA_REBUILD_TARGET_TOOLTIP,
                    Current = rebuildTarget,
                })
                {
                    Keywords = new[] { "realm", "tag", "xxy", "pp", "metadata", "backfill", "force", "recalculate", "sqlite", "rebuild", "maintenance" }
                },
                executeButton,
            });
        }
    }
}
