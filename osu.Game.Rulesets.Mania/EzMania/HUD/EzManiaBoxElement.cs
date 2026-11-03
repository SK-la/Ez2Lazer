// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzManiaBoxElement : EzBoxElement
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_LABEL), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_DESCRIPTION))]
        public BindableBool MatchManiaPanelWidth { get; } = ManiaPlayfieldLayoutHelper.CreateDefaultMatchPanelWidthBindable();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_LABEL), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_DESCRIPTION))]
        public BindableBool MatchManiaHitPosition { get; } = new BindableBool();

        private Bindable<double> columnWidth = null!;
        private Bindable<double> specialFactor = null!;
        private Bindable<ColumnWidthStyle> columnWidthStyle = null!;
        private Bindable<bool> hitPositionGlobalEnable = null!;
        private Bindable<double> hitPosition = null!;
        private Ez2ConfigManager ezSkinConfig = null!;

        [Resolved(canBeNull: true)]
        private InputCountController? inputCountController { get; set; }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezSkinConfig)
        {
            this.ezSkinConfig = ezSkinConfig;
            columnWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            columnWidthStyle = ezSkinConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle);
            hitPositionGlobalEnable = ezSkinConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable);
            hitPosition = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            columnWidth.BindValueChanged(_ => updateWidth());
            specialFactor.BindValueChanged(_ => updateWidth());
            columnWidthStyle.BindValueChanged(_ => updateWidth());
            MatchManiaPanelWidth.BindValueChanged(onMatchPanelWidthChanged, true);
            BoxWidth.BindValueChanged(_ => updateWidth());

            hitPositionGlobalEnable.BindValueChanged(_ => updateHeight());
            hitPosition.BindValueChanged(_ => updateHeight());
            MatchManiaHitPosition.BindValueChanged(onMatchHitPositionChanged, true);
            BoxHeight.BindValueChanged(_ => updateHeight());

            ezSkinConfig.ColumnTypeChanged += (_, __, ___) => updateWidth();
            skin.SourceChanged += onSkinChanged;

            if (MatchManiaPanelWidth.Value || MatchManiaHitPosition.Value)
                applyFlatCornerOnce();
        }

        private void onMatchPanelWidthChanged(ValueChangedEvent<bool> changed)
        {
            if (changed.NewValue)
                applyFlatCornerOnce();

            updateWidth();
        }

        private void onMatchHitPositionChanged(ValueChangedEvent<bool> changed)
        {
            if (changed.NewValue)
                applyFlatCornerOnce();

            updateHeight();
        }

        private void onSkinChanged()
        {
            updateWidth();
            updateHeight();
        }

        private void applyFlatCornerOnce()
        {
            // One-time convenience when enabling a match toggle; the setting stays editable afterwards.
            CornerRadius.Value = 0;
        }

        private void updateWidth()
        {
            if (MatchManiaPanelWidth.Value && tryGetManiaPanelWidth(out float panelWidth))
            {
                Width = panelWidth;
                return;
            }

            Width = BoxWidth.Value;
        }

        private void updateHeight()
        {
            if (MatchManiaHitPosition.Value && tryGetManiaHitPosition(out float hitPositionHeight))
            {
                Height = hitPositionHeight;
                return;
            }

            Height = BoxHeight.Value;
        }

        private bool tryGetManiaPanelWidth(out float panelWidth)
        {
            panelWidth = 0;

            int keyMode = inputCountController?.Triggers.Count ?? 0;
            int displayColumns = ManiaPlayfieldLayoutHelper.GetDisplayColumnCount(keyMode);

            if (displayColumns <= 0)
                return false;

            panelWidth = ManiaPlayfieldLayoutHelper.CalculatePanelTotalWidth(
                keyMode,
                displayColumns,
                skin,
                skinManager,
                ezSkinConfig,
                mobileAdjust: 1f,
                applyGlobalWidthSettings: true);

            return panelWidth > 0;
        }

        private bool tryGetManiaHitPosition(out float hitPositionHeight)
        {
            hitPositionHeight = ManiaPlayfieldLayoutHelper.GetHitPosition(
                skin,
                hitPositionGlobalEnable.Value,
                hitPosition.Value);

            return hitPositionHeight > 0;
        }
    }
}
