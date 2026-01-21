using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Screens
{
    public partial class EzSelectorColour : CompositeDrawable
    {
        private readonly FillFlowContainer buttonsContainer;
        private readonly Dictionary<string, Color4> colorMap = new Dictionary<string, Color4>();
        private readonly Dictionary<string, EzSkinColorButton> buttonsByName = new Dictionary<string, EzSkinColorButton>();
        private readonly Lazy<Dictionary<string, Color4>> defaultColors;
        private readonly Box backgroundBox;
        public Bindable<string> Current { get; }
        public float ButtonHeight { get; set; } = 30;
        private const float spacing_width = 5f;

        /// <summary>
        /// 创建一个颜色按钮选择器
        /// </summary>
        /// <param name="label">选择器标签</param>
        /// <param name="items">可选项目</param>
        /// <param name="colorMapping">项目到颜色的映射</param>
        public EzSelectorColour(string label, string[] items, Dictionary<string, Color4>? colorMapping = null)
        {
            Current = new Bindable<string>();
            // 优化：使用懒加载初始化默认颜色
            defaultColors = new Lazy<Dictionary<string, Color4>>(() => new Dictionary<string, Color4>
            {
                [EzConstants.COLUMN_TYPE_A] = Color4.White,
                [EzConstants.COLUMN_TYPE_B] = Color4.DodgerBlue,
                [EzConstants.COLUMN_TYPE_S] = Color4.IndianRed,
                [EzConstants.COLUMN_TYPE_E] = Color4.IndianRed,
                [EzConstants.COLUMN_TYPE_P] = Color4.LimeGreen
            });

            if (colorMapping != null)
                colorMap = new Dictionary<string, Color4>(colorMapping);

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 5;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Radius = 3f,
                Colour = Color4.Black.Opacity(0.2f),
                Offset = new Vector2(0, 1),
            };

            InternalChildren = new Drawable[]
            {
                backgroundBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.05f)
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 10, Right = 5, Bottom = 5 },
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = label,
                            Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                            Margin = new MarginPadding { Left = 5, Top = 5 }
                        },
                        buttonsContainer = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(spacing_width, 2),
                            Padding = new MarginPadding { Vertical = 2 }
                        }
                    }
                }
            };

            AddButtons(items);

            Current.BindValueChanged(e =>
            {
                if (e.OldValue != null && buttonsByName.TryGetValue(e.OldValue, out var oldButton))
                    oldButton.Selected = false;

                if (e.NewValue != null && buttonsByName.TryGetValue(e.NewValue, out var newButton))
                    newButton.Selected = true;
            });
        }

        // 添加悬浮事件处理
        protected override bool OnHover(HoverEvent e)
        {
            backgroundBox.FadeColour(Color4.White.Opacity(0.1f), 200, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            backgroundBox.FadeColour(Color4.Black.Opacity(0.05f), 200, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        public void AddButtons(IEnumerable<string> colorNames, Dictionary<string, Color4>? customColors = null)
        {
            foreach (string name in colorNames)
            {
                Color4 color = customColors?.TryGetValue(name, out Color4 customColor) == true
                    ? customColor
                    : getColorForName(name);

                var button = new EzSkinColorButton(name, color, ButtonHeight)
                {
                    Selected = Current.Value == name,
                    Action = () => Current.Value = name
                };

                buttonsByName[name] = button;
                buttonsContainer.Add(button);
            }

            updateAllButtonWidths();
        }

        private void updateAllButtonWidths()
        {
            int buttonCount = buttonsContainer.Children.Count;
            if (buttonCount <= 0) return;

            float width = 1f / buttonCount;

            foreach (var child in buttonsContainer.Children.OfType<EzSkinColorButton>())
            {
                child.Width = width - 0.03f;
            }
        }

        public void SetColorMapping(string name, Color4 color)
        {
            colorMap[name] = color;

            if (buttonsByName.TryGetValue(name, out var button))
                button.UpdateColor(color);
        }

        private Color4 getColorForName(string name)
        {
            if (colorMap.TryGetValue(name, out Color4 color))
                return color;

            return defaultColors.Value.TryGetValue(name, out color) ? color : Color4.White;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (var button in buttonsByName.Values)
                {
                    button.Action = null;
                }

                buttonsByName.Clear();
                colorMap.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
