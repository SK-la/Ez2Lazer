// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    /// <summary>
    /// 用于显示列背景的组件，支持按键高亮和暗化效果。
    /// 背景虚化功能由 Stage 级别处理。
    /// </summary>
    public partial class EzColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private Bindable<double> columnDim = new BindableDouble();
        private Bindable<double> hitPosition = new Bindable<double>();
        private Color4 brightColour;
        private Color4 dimColour;

        private Box dimOverlay = null!;
        private Box hitOverlay = null!;
        private Box separator = null!;

        private Bindable<Color4> accentColour = null!;

        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public EzColumnBackground()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            dimOverlay = new Box
            {
                Name = "Dim Overlay",
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
            };

            hitOverlay = new Box
            {
                Name = "Hit Overlay",
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Height = 0.5f,
                Blending = BlendingParameters.Additive,
                Alpha = 0
            };

            separator = new Box
            {
                Name = "Separator",
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopCentre,
                Width = 2,
                Colour = Color4.White.Opacity(0.5f),
                Alpha = 0,
            };

            accentColour = new Bindable<Color4>(ezSkinConfig.GetColumnColor(stageDefinition.Columns, Column.Index));
            accentColour.BindValueChanged(colour =>
            {
                var baseCol = colour.NewValue;
                var newColour = baseCol.Darken(3);
                if (newColour.A != 0)
                    newColour = newColour.Opacity(0.8f);
                hitOverlay.Colour = newColour;
                brightColour = baseCol.Opacity(0.6f);
                dimColour = baseCol.Opacity(0);
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Column.TopLevelContainer.Children.OfType<Box>().All(b => b.Name != "Separator"))
                Column.TopLevelContainer.Add(separator);

            if (!Column.BackgroundContainer.Children.Contains(dimOverlay))
                Column.BackgroundContainer.Add(dimOverlay);

            if (!Column.BackgroundContainer.Children.Contains(hitOverlay))
                Column.BackgroundContainer.Add(hitOverlay);

            hitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            hitPosition.BindValueChanged(_ => updateSeparator(), true);

            columnDim = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnDim);
            columnDim.BindValueChanged(_ => applyDim(), true);
        }

        private float dimTarget => (float)columnDim.Value;

        private void applyDim()
        {
            dimOverlay.Colour = Colour4.Black.Opacity(dimTarget);
        }

        private void updateSeparator()
        {
            separator.Height = DrawHeight - (float)hitPosition.Value;
            separator.Alpha = drawSeparator(Column.Index, stageDefinition) ? 0.25f : 0;
        }

        protected virtual Color4 NoteColor => ezSkinConfig.GetColumnColor(stageDefinition.Columns, Column.Index);

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == Column.Action.Value)
            {
                var noteColour = NoteColor;
                brightColour = noteColour.Opacity(0.9f);
                dimColour = noteColour.Opacity(0);
                hitOverlay.Colour = ColourInfo.GradientVertical(dimColour, brightColour);
                hitOverlay.FadeTo(1, 50, Easing.OutQuint).Then().FadeTo(0.5f, 250, Easing.OutQuint);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == Column.Action.Value)
                hitOverlay.FadeTo(0, 250, Easing.OutQuint);
        }

        //TODO: 这里的逻辑可以优化，避免重复计算
        private bool drawSeparator(int columnIndex, StageDefinition stage) => stage.Columns switch
        {
            12 => columnIndex is 0 or 10,
            14 => columnIndex is 0 or 5 or 6 or 11,
            16 => columnIndex is 0 or 5 or 9 or 14,
            _ => false
        };
    }
}
