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
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania;
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
        private Sprite hitOverlay = null!;
        private Box? separator;
        private static Texture? sharedTexture;

        private Bindable<Colour4> colourBindable = null!;
        private Bindable<double> hitPosition = null!;
        private Color4 brightColour;
        private Color4 dimColour;
        private bool hasSeparator;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        public EzColumnBackground()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            // 计算 drawSeparator 结果（基于不变的列数和列索引）
            hasSeparator = stageDefinition.HasSeparator(column.Index);
            sharedTexture ??= textures.Get("EzResources/note/ColumnLight");

            hitOverlay = new Sprite
            {
                Name = "Hit Overlay",
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                // Blending = BlendingParameters.Additive,
                Alpha = 0,
                Texture = sharedTexture,
            };

            hitPosition = ezConfig.GetBindable<double>(Ez2Setting.HitPosition);
            hitPosition.BindValueChanged(_ => updateSeparator());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (hasSeparator)
            {
                separator = new Box
                {
                    Name = "Separator",
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopCentre,
                    Width = 2,
                    Colour = Color4.White.Opacity(0.5f),
                    Alpha = 0,
                };

                column.BackgroundContainer.Add(separator);
            }

            if (!column.BackgroundContainer.Children.Contains(hitOverlay))
                column.BackgroundContainer.Add(hitOverlay);

            Scheduler.AddOnce(updateSeparator);

            colourBindable = column.EzNoteColourBindable;
            colourBindable.BindValueChanged(v =>
            {
                var baseColour = v.NewValue;
                brightColour = baseColour.Opacity(0.6f);
                dimColour = baseColour.Opacity(0);

                hitOverlay.Colour = ColourInfo.GradientVertical(dimColour, brightColour);
            }, true);
        }

        private void updateSeparator()
        {
            float h = DrawHeight - (float)hitPosition.Value;
            hitOverlay.Height = h;

            if (separator != null)
            {
                separator.Height = h;
                separator.Alpha = hasSeparator ? 0.25f : 0;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
                hitOverlay.FadeTo(1, 50, Easing.OutQuint).Then().FadeTo(0.5f, 250, Easing.OutQuint);
            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
                hitOverlay.FadeTo(0, 250, Easing.OutQuint);
        }
    }
}
