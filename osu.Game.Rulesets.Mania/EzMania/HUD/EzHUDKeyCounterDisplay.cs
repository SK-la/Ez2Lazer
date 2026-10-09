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
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzHUDKeyCounterDisplay : Container, ISerialisableDrawable
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_LAYOUT_LABEL), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_LAYOUT_DESCRIPTION))]
        public BindableBool MatchManiaHitPositionLayout { get; } = new BindableBool(true);

        private readonly FillFlowContainer<EzKeyCounter> keyFlow;
        private readonly IBindableList<InputTrigger> triggers = new BindableList<InputTrigger>();
        private IBindable<double> columnWidth = null!;
        private IBindable<double> specialFactor = null!;
        private Bindable<bool> hitPositionGlobalEnable = null!;
        private Bindable<double> hitPosition = null!;

        private Anchor savedAnchor;
        private Anchor savedOrigin;
        private Vector2 savedPosition;
        private bool savedLayout;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        public EzHUDKeyCounterDisplay()
        {
            AutoSizeAxes = Axes.Y;

            Child = keyFlow = new FillFlowContainer<EzKeyCounter>
            {
                Direction = FillDirection.Horizontal,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(0),
            };
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezSkinConfig)
        {
            columnWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            hitPositionGlobalEnable = ezSkinConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable);
            hitPosition = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition);
            updateWidths();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            triggers.BindTo(controller.Triggers);
            triggers.BindCollectionChanged(triggersChanged, true);
            columnWidth.BindValueChanged(_ => updateWidths(), true);
            specialFactor.BindValueChanged(_ => updateWidths(), true);

            hitPositionGlobalEnable.BindValueChanged(_ => updateHitPositionLayout());
            hitPosition.BindValueChanged(_ => updateHitPositionLayout());
            MatchManiaHitPositionLayout.BindValueChanged(_ => updateHitPositionLayout(), true);
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

        private void updateWidths()
        {
            int keyCount = keyFlow.Count;

            if (keyCount <= 0)
                return;

            float totalWidth = 0;

            for (int i = 0; i < keyCount; i++)
            {
                float? widthS = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                        new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.ColumnWidth, i))
                                    ?.Value;

                float newWidth = widthS ?? (float)columnWidth.Value;

                keyFlow[i].Width = newWidth;
                totalWidth += newWidth;
            }

            Width = totalWidth;
        }

        private void triggersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            keyFlow.Clear();

            int displayColumns = ManiaEzColumnLayout.GetDisplayColumnCount(controller.Triggers.Count);

            if (displayColumns <= 0)
                return;

            for (int i = 0; i < displayColumns; i++)
            {
                float? widthS = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                        new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.ColumnWidth, i))
                                    ?.Value;

                if (widthS == 0)
                    continue;

                keyFlow.Add(new EzKeyCounter(controller.Triggers[i]));
            }

            // foreach (var trigger in controller.Triggers)
            //     keyFlow.Add(new EzKeyCounter(trigger));
        }

        public bool UsesFixedAnchor { get; set; }
    }
}
