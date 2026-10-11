// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzHUDKeyCounterDisplay : Container, ISerialisableDrawable
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_LAYOUT_LABEL), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_LAYOUT_DESCRIPTION))]
        public BindableBool MatchManiaHitPositionLayout { get; } = new BindableBool(true);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_LABEL), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_DESCRIPTION))]
        public BindableBool MatchManiaPanelWidth { get; } = ManiaPlayfieldLayoutHelper.CreateDefaultMatchPanelWidthBindable();

        private readonly FillFlowContainer<EzKeyCounter> keyFlow;
        private readonly IBindableList<InputTrigger> triggers = new BindableList<InputTrigger>();

        private IBindable<double> columnWidth = null!;
        private IBindable<double> specialFactor = null!;
        private Bindable<ColumnWidthStyle> columnWidthStyle = null!;
        private Bindable<bool> hitPositionGlobalEnable = null!;
        private Bindable<double> hitPosition = null!;

        private Anchor savedAnchor;
        private Anchor savedOrigin;
        private Vector2 savedPosition;
        private bool savedLayout;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public EzHUDKeyCounterDisplay()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.TopCentre;
            Child = keyFlow = new FillFlowContainer<EzKeyCounter>
            {
                Direction = FillDirection.Horizontal,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(0),
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            columnWidthStyle = ezSkinConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle);
            hitPositionGlobalEnable = ezSkinConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable);
            hitPosition = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition);
            updateWidth();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            triggers.BindTo(controller.Triggers);
            triggers.BindCollectionChanged(triggersChanged, true);

            columnWidth.BindValueChanged(_ => updateWidth(), true);
            specialFactor.BindValueChanged(_ => updateWidth(), true);
            columnWidthStyle.BindValueChanged(_ => updateWidth(), true);
            MatchManiaPanelWidth.BindValueChanged(_ => updateWidth(), true);

            hitPositionGlobalEnable.BindValueChanged(_ => updateHitPositionLayout());
            hitPosition.BindValueChanged(_ => updateHitPositionLayout());
            MatchManiaHitPositionLayout.BindValueChanged(_ => updateHitPositionLayout(), true);

            ezSkinConfig.ColumnTypeChanged += updateWidth;
            skin.SourceChanged += onSkinChanged;
        }

        private void onSkinChanged() => updateHitPositionLayout();

        private void updateHitPositionLayout()
        {
            if (MatchManiaHitPositionLayout.Value)
            {
                if (!savedLayout)
                {
                    savedAnchor = Anchor;
                    savedOrigin = Origin;
                    savedPosition = Position;
                    savedLayout = true;
                }

                Anchor = Anchor.BottomCentre;
                Origin = Anchor.TopCentre;

                ManiaPlayfieldLayoutHelper.ApplyHitPositionPlacement(
                    this,
                    ManiaPlayfieldLayoutHelper.GetHitPosition(skin, hitPositionGlobalEnable.Value, hitPosition.Value));
                return;
            }

            if (!savedLayout)
                return;

            Anchor = savedAnchor;
            Origin = savedOrigin;
            Position = savedPosition;
            savedLayout = false;
        }

        private void updateWidth(int arg1, int arg2, EzColumnType arg3)
        {
            updateWidth();
        }

        private void updateWidth()
        {
            int keyCount = keyFlow.Count;

            if (keyCount <= 0)
                return;

            int keyMode = controller.Triggers.Count;

            for (int i = 0; i < keyCount; i++)
            {
                var columnSize = ManiaPlayfieldLayoutHelper.CalculateColumnSize(
                    i,
                    keyMode,
                    skin,
                    skinManager,
                    ezSkinConfig,
                    mobileAdjust: 1f,
                    MatchManiaPanelWidth.Value);

                keyFlow[i].Width = columnSize.Width;
            }

            updateHitPositionLayout();
        }

        private void triggersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            keyFlow.Clear();

            int displayColumns = ManiaPlayfieldLayoutHelper.GetDisplayColumnCount(controller.Triggers.Count);

            if (displayColumns <= 0)
                return;

            int keyMode = controller.Triggers.Count;

            for (int i = 0; i < displayColumns; i++)
            {
                var columnSize = ManiaPlayfieldLayoutHelper.CalculateColumnSize(
                    i,
                    keyMode,
                    skin,
                    skinManager,
                    ezSkinConfig,
                    mobileAdjust: 1f,
                    MatchManiaPanelWidth.Value);

                if (columnSize.Width == 0)
                    continue;

                keyFlow.Add(new EzKeyCounter(controller.Triggers[i]));
            }

            updateWidth();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                ezSkinConfig.ColumnTypeChanged -= updateWidth;
                skin.SourceChanged -= onSkinChanged;
            }
        }

        public bool UsesFixedAnchor { get; set; }
    }
}
