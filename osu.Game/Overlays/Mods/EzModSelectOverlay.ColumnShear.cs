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
            ContentView.ApplyShear(ColumnFlowShearX);
        }
    }

    public abstract partial class ModSelectColumn
    {
        private float? ezLayoutShear;

        internal void ApplyEzLayoutShear(float shearX)
        {
            ezLayoutShear = shearX;
            var counter = new Vector2(-shearX, 0);
            headerBackground.Shear = WrapItems ? new Vector2(shearX, 0) : Vector2.Zero;
            triangles.Shear = WrapItems ? Vector2.Zero : counter;
            headerText.Shear = WrapItems ? Vector2.Zero : counter;

            foreach (var panel in ItemsFlow.Children.OfType<ModSelectPanel>())
            {
                panel.Shear = WrapItems ? new Vector2(shearX, 0) : Vector2.Zero;
                panel.ApplyEzLayoutShear(shearX);
            }

            foreach (var button in ItemsFlow.Children.OfType<AddPresetButton>())
            {
                button.Shear = WrapItems ? new Vector2(shearX, 0) : Vector2.Zero;
                button.ApplyEzLayoutShear(shearX);
            }

            if (this is EzModGroupColumn groupColumn)
                groupColumn.ApplyEzLayoutShearExtras(WrapItems ? 0 : shearX);
        }

        protected void RefreshEzLayoutShear()
        {
            if (ezLayoutShear is float shear)
                ApplyEzLayoutShear(shear);
        }
    }

    public partial class AddPresetButton
    {
        internal void ApplyEzLayoutShear(float shearX) => ButtonContent.Shear = new Vector2(-shearX, 0);
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
