// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
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
        private void load(OsuConfigManager config)
        {
            columnWidth = config.GetBindable<double>(OsuSetting.ColumnWidth);
            specialFactor = config.GetBindable<double>(OsuSetting.SpecialFactor);

            columnWidth.BindValueChanged(_ => updateWidths());
            specialFactor.BindValueChanged(_ => updateWidths());

            updateWidths();
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
            foreach (var trigger in controller.Triggers)
                keyFlow.Add(new EzKeyCounter(trigger));
        }

        public bool UsesFixedAnchor { get; set; }
    }
}
