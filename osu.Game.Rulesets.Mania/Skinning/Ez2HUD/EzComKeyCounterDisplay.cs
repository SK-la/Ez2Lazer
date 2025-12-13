// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Screens;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    public partial class EzComKeyCounterDisplay : Container, ISerialisableDrawable
    {
        private readonly FillFlowContainer<EzKeyCounter> keyFlow;
        private readonly IBindableList<InputTrigger> triggers = new BindableList<InputTrigger>();
        private IBindable<double> columnWidth = null!;
        private IBindable<double> specialFactor = null!;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        public EzComKeyCounterDisplay()
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
        private void load(EzSkinSettingsManager ezSkinConfig)
        {
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
            updateWidths();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            triggers.BindTo(controller.Triggers);
            triggers.BindCollectionChanged(triggersChanged, true);
            columnWidth.BindValueChanged(_ => updateWidths(), true);
            specialFactor.BindValueChanged(_ => updateWidths(), true);
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

            for (int i = 0; i < controller.Triggers.Count; i++)
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
