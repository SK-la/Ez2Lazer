// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    /// <summary>
    /// 用于显示列背景的组件，支持按键高亮和暗化效果。
    /// 背景虚化功能由 Stage 级别处理。
    /// </summary>
    public partial class EzColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly IBindable<Colour4> columnColourLocal = new Bindable<Colour4>();
        private readonly IBindable<double> hitPosition = new BindableDouble();
        private Color4 brightColour;
        private Color4 dimColour;

        private Sprite hitOverlay = null!;
        private Box separator = null!;

        private bool shouldDrawSeparator;

        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        public EzColumnBackground()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            var texture = textures.Get("EzResources/note/ColumnLight.png");

            hitOverlay = new Sprite
            {
                Name = "Hit Overlay",
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                // Blending = BlendingParameters.Additive,
                Alpha = 0,
                Texture = texture,
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

            // 计算 drawSeparator 结果（基于不变的列数和列索引）
            shouldDrawSeparator = drawSeparatorImpl(Column.Index, stageDefinition);

            applyAccent(Column.AccentColour.Value);

            hitPosition.BindTo(ezSkinInfo.HitPosition);
            hitPosition.BindValueChanged(_ => updateSeparator(), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bool hasSeparator = false;

            foreach (var child in Column.BackgroundContainer.Children)
            {
                if (child is Box b && b.Name == "Separator")
                {
                    hasSeparator = true;
                    break;
                }
            }

            if (!hasSeparator)
                Column.BackgroundContainer.Add(separator);

            if (!Column.BackgroundContainer.Children.Contains(hitOverlay))
                Column.BackgroundContainer.Add(hitOverlay);

            // Now bind to column colour (Column may not have been ready during load).
            columnColourLocal.BindTo(Column.EzColumnColourBindable);
            // apply current accent immediately and subscribe to future changes
            columnColourLocal.BindValueChanged(e => applyAccent(e.NewValue), true);
        }

        private void updateSeparator()
        {
            float h = DrawHeight - (float)hitPosition.Value;
            hitOverlay.Y = -(float)hitPosition.Value;
            hitOverlay.Height = h;
            separator.Height = h;
            separator.Alpha = shouldDrawSeparator ? 0.25f : 0;
        }

        protected virtual Color4 NoteColor
        {
            get
            {
                var c = Column.EzColumnColourBindable.Value;
                return new Color4(c.R, c.G, c.B, c.A);
            }
        }

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

        // 基于不变的列数和列索引预计算分隔符显示
        private bool drawSeparatorImpl(int columnIndex, StageDefinition stage) => stage.Columns switch
        {
            12 => columnIndex is 0 or 10,
            14 => columnIndex is 0 or 5 or 6 or 11,
            16 => columnIndex is 0 or 5 or 9 or 14,
            _ => false
        };

        private void applyAccent(Colour4 baseCol)
        {
            var baseColor4 = new Color4(baseCol.R, baseCol.G, baseCol.B, baseCol.A);
            var newColour = baseColor4.Darken(3);
            if (newColour.A != 0)
                newColour = newColour.Opacity(0.8f);
            hitOverlay.Colour = newColour;
            brightColour = baseColor4.Opacity(0.6f);
            dimColour = baseColor4.Opacity(0);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Unbind local bindables to avoid leaking subscriptions.
                columnColourLocal.UnbindBindings();
                hitPosition.UnbindBindings();
            }

            base.Dispose(isDisposing);
        }
    }
}
