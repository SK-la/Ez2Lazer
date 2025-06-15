// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
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

        // private StageDefinition stage = null!;
        // private int keyCount;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        // public readonly KeyBindingContainer<ManiaAction> keyBindingContainer;

        public EzComKeyCounterDisplay() //(StageDefinition stageDefinition)
        {
            // this.stageDefinition = stageDefinition;
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
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            triggers.BindTo(controller.Triggers);
            triggers.BindCollectionChanged(triggersChanged, true);

            columnWidth.BindValueChanged(_ => updateWidths());
            specialFactor.BindValueChanged(_ => updateWidths());

            updateWidths();
        }

        private void updateWidths()
        {
            int keyCount = keyFlow.Count;

            if (keyCount <= 0)
                return;

            StageDefinition stage = new StageDefinition(keyCount);
            float totalWidth = 0;

            for (int i = 0; i < keyCount; i++)
            {
                float newWidth = (float)columnWidth.Value;

                if (stage.EzIsSpecialColumn(i))
                {
                    newWidth *= (float)specialFactor.Value;
                }

                keyFlow[i].Width = newWidth;
                totalWidth += newWidth;
            }

            Width = totalWidth;
        }

        private void triggersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            keyFlow.Clear();
            // var keyBindings = keyBindingContainer.DefaultKeyBindings.ToList();
            //
            // for (int i = 0; i < controller.Triggers.Count; i++)
            // {
            //     var trigger = controller.Triggers[i];
            //     // 这里假设 keyBindings[i] 和 trigger 顺序一致
            //     var keyName = keyBindings[i].KeyCombination.ToString();
            //     keyFlow.Add(new EzKeyCounter(trigger, keyName));
            // }

            foreach (var trigger in controller.Triggers)
                keyFlow.Add(new EzKeyCounter(trigger));
        }

        public bool UsesFixedAnchor { get; set; }
    }
}
