// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Rulesets.Mania.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    /// <summary>
    /// 用于显示列背景的组件，支持按键高亮和暗化效果。
    /// 背景虚化功能由 Stage 级别处理。
    /// </summary>
    public partial class EzColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private const string overlay_texture_base_path = "Column/ColumnLight";

        private Container lightContainer = null!;
        private Drawable light = null!;
        private Box? separator;

        private Bindable<Colour4> colourBindable = null!;
        private Bindable<double> hitPosition = null!;

        // private Color4 brightColour;
        // private Color4 dimColour;

        private bool hasSeparator;
        private float lightPosition;

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
        private void load(EzResourceStore resources)
        {
            // 计算 drawSeparator 结果（基于不变的列数和列索引）
            // TODO: 以后要支持自定义
            hasSeparator = stageDefinition.HasSeparator(column.Index);

            InternalChildren = new[]
            {
                lightContainer = new Container
                {
                    // Name = "mania-stage-light",
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.Both,
                    Child = light = resources.GetAnimation(overlay_texture_base_path)?.With(l =>
                    {
                        l.Anchor = Anchor.BottomCentre;
                        l.Origin = Anchor.BottomCentre;
                        l.RelativeSizeAxes = Axes.X;
                        l.Width = 1;
                        l.Alpha = 0;
                    }) ?? Empty(),
                }
            };

            hitPosition = ezConfig.GetBindable<double>(Ez2Setting.HitPosition);
            hitPosition.BindValueChanged(_ => updateSeparator(), true);
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

            // if (!column.BackgroundContainer.Children.Contains(light))
            //     column.BackgroundContainer.Add(light);

            Scheduler.AddOnce(updateSeparator);

            colourBindable = column.EzNoteColourBindable;
            colourBindable.BindValueChanged(v =>
            {
                var baseColour = v.NewValue;
                light.Colour = baseColour;

                // brightColour = baseColour.Opacity(1f);
                // dimColour = baseColour.Opacity(0);
                // hitOverlay.Colour = ColourInfo.GradientVertical(dimColour, brightColour);
            }, true);
        }

        private void updateSeparator()
        {
            lightPosition = (float)hitPosition.Value;
            float h = DrawHeight - lightPosition;
            // hitOverlay.Height = h;

            lightContainer.Padding = new MarginPadding { Bottom = lightPosition };
            lightContainer.Scale = Vector2.One;

            if (separator != null)
            {
                separator.Height = h;
                separator.Alpha = hasSeparator ? 0.25f : 0;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
            {
                light.FadeIn();
                light.ScaleTo(Vector2.One);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            const double animation_length = 250;

            if (e.Action == column.Action.Value)
            {
                light.FadeTo(0, animation_length);
                light.ScaleTo(new Vector2(1, 0), animation_length);
            }
        }
    }
}
