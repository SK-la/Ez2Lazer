// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 配置控件工厂，根据配置元数据自动创建对应的 UI 控件。
    /// </summary>
    public static class ConfigControlFactory
    {
        /// <summary>
        /// 根据属性类型创建对应的配置控件。
        /// </summary>
        public static Drawable CreateControl(ConfigMetadata metadata, Action<object?> onValueChanged)
        {
            var container = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Padding = new MarginPadding { Bottom = 15 }
            };

            // 标题和描述
            container.Add(createHeader(metadata));

            // 根据类型创建控件
            Drawable control = metadata.PropertyType switch
            {
                Type t when t == typeof(float) => createFloatSlider(metadata, onValueChanged),
                Type t when t == typeof(int) => createIntSlider(metadata, onValueChanged),
                Type t when t == typeof(bool) => createCheckbox(metadata, onValueChanged),
                Type t when t == typeof(string) => createTextBox(metadata, onValueChanged),
                Type t when t == typeof(Color4) => createColorPicker(metadata, onValueChanged),
                _ => createUnsupportedControl(metadata)
            };

            container.Add(control);

            return container;
        }

        private static Container createHeader(ConfigMetadata metadata)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = metadata.DisplayName,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                        Colour = Color4.White,
                    },
                    new OsuSpriteText
                    {
                        Text = metadata.Description ?? string.Empty,
                        Font = OsuFont.Default.With(size: 11),
                        Colour = Color4.Gray,
                        Y = 18,
                    }
                }
            };
        }

        private static SliderBar<float> createFloatSlider(ConfigMetadata metadata, Action<object?> onValueChanged)
        {
            float min = metadata.Min is float fmin ? fmin : 0f;
            float max = metadata.Max is float fmax ? fmax : 1f;
            float value = metadata.DefaultValue is float fval ? fval : (min + max) / 2;

            var bindable = new BindableFloat(value) { MinValue = min, MaxValue = max };
            bindable.ValueChanged += e => onValueChanged(e.NewValue);

            return new RoundedSliderBar<float>
            {
                RelativeSizeAxes = Axes.X,
                Current = bindable,
            };
        }

        private static SliderBar<int> createIntSlider(ConfigMetadata metadata, Action<object?> onValueChanged)
        {
            int min = metadata.Min is int imin ? imin : 0;
            int max = metadata.Max is int imax ? imax : 100;
            int value = metadata.DefaultValue is int ival ? ival : (min + max) / 2;

            var bindable = new BindableInt(value) { MinValue = min, MaxValue = max };
            bindable.ValueChanged += e => onValueChanged(e.NewValue);

            return new RoundedSliderBar<int>
            {
                RelativeSizeAxes = Axes.X,
                Current = bindable,
            };
        }

        private static OsuCheckbox createCheckbox(ConfigMetadata metadata, Action<object?> onValueChanged)
        {
            bool value = metadata.DefaultValue is bool bval && bval;

            var bindable = new BindableBool(value);
            bindable.ValueChanged += e => onValueChanged(e.NewValue);

            return new OsuCheckbox
            {
                Current = bindable,
                LabelText = metadata.DisplayName,
            };
        }

        private static OsuTextBox createTextBox(ConfigMetadata metadata, Action<object?> onValueChanged)
        {
            string value = metadata.DefaultValue as string ?? string.Empty;

            var textbox = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                Text = value,
                PlaceholderText = "Enter value...",
            };

            textbox.OnCommit += (sender, isNew) =>
            {
                if (isNew)
                    onValueChanged(textbox.Text);
            };

            return textbox;
        }

        private static Container createColorPicker(ConfigMetadata metadata, Action<object?> onValueChanged)
        {
            // 简化的颜色选择器（实际项目中可能需要更复杂的实现）
            string hexValue = metadata.DefaultValue as string ?? "#FFFFFFFF";

            try
            {
                Color4 color = Colour4.FromHex(hexValue);

                var container = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = color,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = hexValue,
                            Font = OsuFont.Default.With(size: 12),
                            Colour = getContrastColor(color),
                            X = 10,
                        }
                    }
                };

                // TODO: 添加点击事件打开颜色选择对话框
                // 这里简化处理，实际需要集成颜色选择器组件

                return container;
            }
            catch
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    Child = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = "Invalid color format",
                        Colour = Color4.Red,
                    }
                };
            }
        }

        private static Color4 getContrastColor(Color4 background)
        {
            // 计算亮度，决定使用黑色还是白色文字
            float luminance = (0.299f * background.R + 0.587f * background.G + 0.114f * background.B) / 255f;
            return luminance > 0.5f ? Color4.Black : Color4.White;
        }

        private static Container createUnsupportedControl(ConfigMetadata metadata)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Child = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = $"Unsupported type: {metadata.PropertyType.Name}",
                    Colour = Color4.Orange,
                }
            };
        }
    }
}
