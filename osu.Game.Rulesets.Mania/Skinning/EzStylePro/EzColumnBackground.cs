// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        private const string default_column_set = "EzColumnLight";
        private const string builtin_column_light_path = "Column/ColumnLight";
        private static readonly EzColumnType[] type_fallback_order = { EzColumnType.S, EzColumnType.E, EzColumnType.P, EzColumnType.B, EzColumnType.A };

        private Container lightContainer = null!;
        private Drawable light = null!;
        private Box? separator;

        private EzResourceStore resources = null!;
        private Bindable<string> columnSet = null!;
        private Bindable<Colour4> colourBindable = null!;
        private Bindable<double> hitPosition = null!;
        private bool tintLight = true;

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
            this.resources = resources;

            // 计算 drawSeparator 结果（基于不变的列数和列索引）
            hasSeparator = stageDefinition.HasSeparator(column.Index);

            InternalChildren = new[]
            {
                lightContainer = new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.Both,
                    Child = light = Empty(),
                }
            };

            columnSet = ezConfig.GetBindable<string>(Ez2Setting.ColumnLightName);
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

            columnSet.BindValueChanged(_ => reloadLight(), true);
            column.EzNoteTypeBindable.BindValueChanged(_ =>
            {
                if (!tintLight)
                    reloadLight();
            });

            colourBindable.BindValueChanged(v =>
            {
                if (tintLight)
                    light.Colour = v.NewValue;

                // brightColour = baseColour.Opacity(1f);
                // dimColour = baseColour.Opacity(0);
                // hitOverlay.Colour = ColourInfo.GradientVertical(dimColour, brightColour);
            }, true);
        }

        private void reloadLight()
        {
            string setName = string.IsNullOrEmpty(columnSet.Value) ? default_column_set : columnSet.Value;
            Drawable? next = loadFromFolder(setName, out bool tint);

            if (next == null && isDefaultSet(setName))
            {
                next = resources.GetAnimation(builtin_column_light_path);
                tint = next != null;
            }

            tintLight = tint && next != null;
            lightContainer.Clear(disposeChildren: true);
            light = next ?? Empty();
            prepareLight(light);
            lightContainer.Add(light);
            light.Colour = tintLight ? colourBindable.Value : Colour4.White;
        }

        private Drawable? loadFromFolder(string setName, out bool tint)
        {
            int start = Array.IndexOf(type_fallback_order, column.EzNoteTypeBindable.Value);

            if (start < 0)
                start = type_fallback_order.Length - 1;

            string? tried = null;

            for (int i = start; i < type_fallback_order.Length; i++)
            {
                string name = colourName(type_fallback_order[i]);

                if (tried != null && string.Equals(name, tried, StringComparison.Ordinal))
                    continue;

                tried = name;
                Drawable? graphic = loadColour(setName, name);

                if (graphic == null)
                    continue;

                tint = false;
                return graphic;
            }

            Drawable? columnLight = resources.GetAnimation($"Column/{setName}/ColumnLight");

            if (columnLight != null)
            {
                tint = true;
                return columnLight;
            }

            tint = false;
            return null;
        }

        private Drawable? loadColour(string setName, string colour)
            => resources.GetAnimation(new EzAnimationRequest
            {
                Path = $"Column/{setName}/{colour}",
                Usage = EzTextureUsage.Large,
            });

        private static bool isDefaultSet(string setName)
            => string.Equals(setName, default_column_set, StringComparison.OrdinalIgnoreCase);

        private static string colourName(EzColumnType type) => type switch
        {
            EzColumnType.S or EzColumnType.E => "Red",
            EzColumnType.P => "Green",
            EzColumnType.B => "Blue",
            _ => "White",
        };

        private static void prepareLight(Drawable drawable)
        {
            drawable.Anchor = Anchor.BottomCentre;
            drawable.Origin = Anchor.BottomCentre;
            drawable.RelativeSizeAxes = Axes.X;
            drawable.Width = 1;
            drawable.Alpha = 0;
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
