// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Screens;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public abstract partial class ManiaKeyCounterDisplay : Container
    {
        [Resolved]
        protected StageDefinition StageDefinition { get; private set; } = null!;

        [Resolved]
        protected InputCountController Controller { get; private set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        protected readonly FillFlowContainer<KeyCounter> KeyFlow;

        private readonly IBindableList<InputTrigger> triggers = new BindableList<InputTrigger>();
        private IBindable<double> columnWidth = null!;
        private IBindable<double> specialFactor = null!;

        protected ManiaKeyCounterDisplay()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = KeyFlow = new FillFlowContainer<KeyCounter>
            {
                Direction = FillDirection.Horizontal,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(0),
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            triggers.BindTo(Controller.Triggers);
            triggers.BindCollectionChanged(triggersChanged, true);

            columnWidth.BindValueChanged(_ => updateCounterWidths());
            specialFactor.BindValueChanged(_ => updateCounterWidths());
        }

        private void updateCounterWidths()
        {
            foreach (var counter in KeyFlow)
            {
                float width = (float)columnWidth.Value;
                int index = KeyFlow.IndexOf(counter);

                if (ezSkinConfig.IsSpecialColumn(StageDefinition.Columns, index))
                    width *= (float)specialFactor.Value;

                counter.Width = width;
            }
        }

        private void triggersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            KeyFlow.Clear();
            foreach (var trigger in Controller.Triggers)
                KeyFlow.Add(CreateCounter(trigger));

            updateCounterWidths();
        }

        protected abstract KeyCounter CreateCounter(InputTrigger trigger);
    }
}
