// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Overlays;
using osuTK;

namespace osu.Game.Overlays.Mods
{
    public partial class ModSelectOverlay
    {
        protected float ColumnFlowShearX { get; private set; } = EzModLayoutResolver.DEFAULT_COLUMN_SHEAR;

        protected void SetEzColumnFlowShearX(float shearX)
        {
            ColumnFlowShearX = EzModLayoutResolver.NormaliseColumnShear(shearX);
            applyEzColumnFlowShear();
        }

        private void applyEzColumnFlowShear()
        {
            columnFlow.Shear = new Vector2(ColumnFlowShearX, 0);
            applyEzColumnFlowShearToColumns();
            Scheduler.AddDelayed(applyEzColumnFlowShearToColumns, 50);
            Scheduler.AddDelayed(applyEzColumnFlowShearToColumns, 250);
        }

        private void applyEzColumnFlowShearToColumns()
        {
            foreach (var dim in columnFlow)
            {
                if (dim.Column is ModColumn modColumn)
                    modColumn.ApplyEzLayoutShearToContent(ColumnFlowShearX);
                else
                    dim.Column.ApplyEzLayoutShear(ColumnFlowShearX);
            }
        }
    }

    public abstract partial class ModSelectColumn
    {
        internal void ApplyEzLayoutShear(float shearX)
        {
            var counter = new Vector2(-shearX, 0);
            triangles.Shear = counter;
            headerText.Shear = counter;
        }
    }

    public partial class ModColumn
    {
        internal void ApplyEzLayoutShearToContent(float shearX)
        {
            ApplyEzLayoutShear(shearX);

            foreach (var panel in ItemsFlow.Children.OfType<ModSelectPanel>())
                panel.ApplyEzLayoutShear(shearX);

            if (this is EzModGroupColumn groupColumn)
                groupColumn.ApplyEzLayoutShearExtras(shearX);
        }
    }

    public abstract partial class ModSelectPanel
    {
        private bool usesShearMargin;

        internal void ApplyEzLayoutShear(float shearX)
        {
            var counter = new Vector2(-shearX, 0);

            titleText.Shear = counter;
            descriptionText.Shear = counter;

            if (titleText.Margin.Left != 0 || usesShearMargin)
            {
                usesShearMargin = true;
                titleText.Margin = titleText.Margin with { Left = -18 * shearX };
            }

            foreach (var icon in SwitchContainer.Children)
                icon.Shear = counter;
        }
    }
}
