// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
{
    public partial class EzComboText : CompositeDrawable, IHasText
    {
        internal const float GLYPH_TARGET_HEIGHT = 25;

        private readonly EzComboSpriteText textPart;
        public Bindable<string> ThemeName { get; } = new Bindable<string>(EzSelectorEnumList.DEFAULT_NAME);

        // public Bindable<bool> UseLazerFont { get; } = new Bindable<bool>(false);

        public FillFlowContainer TextContainer { get; private set; }

        // 位移写在字形父级上，不会跟着字形缩放。源图像素先经过度量缩放，再乘适配缩放。
        public float SourcePixelScale => textPart.Scale.Y * EzSpriteText.GLYPH_METRIC_SCALE;

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

        public EzComboText(Bindable<string>? externalThemeName = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            if (externalThemeName is not null)
                ThemeName = externalThemeName;

            textPart = new EzComboSpriteText(textLookup, ThemeName);

            InternalChildren = new Drawable[]
            {
                TextContainer = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(2),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,

                    Children = new Drawable[]
                    {
                        textPart
                    }
                },
            };
        }

        private string textLookup(char c)
        {
            switch (c)
            {
                case 't': return @"Title";

                case 'e': return @"Early";

                case 'l': return @"Late";

                case 'j': return @"judgement";

                default: return c.ToString();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ThemeName.BindValueChanged(e =>
            {
                textPart.ThemeName.Value = e.NewValue;
                float scale = getUniformHeightScale(textPart.Height, GLYPH_TARGET_HEIGHT);
                textPart.Scale = new Vector2(scale);
                textPart.Invalidate(); // 确保 UI 立即刷新
            }, true);

            // （可选）textPart.ThemeName 变化时（来自全局配置），反向更新 SettingSource 的 ThemeName
            // 如果你不需要反向更新，可以注释掉这段代码
            textPart.ThemeName.BindValueChanged(e =>
            {
                if (ThemeName.Value != e.NewValue)
                {
                    ThemeName.Value = e.NewValue;
                }
            });
        }

        private float getUniformHeightScale(float textureHeight, float targetHeight = 25f)
            => textureHeight <= 0 ? 1 : targetHeight / textureHeight;

        private partial class EzComboSpriteText : EzSpriteText
        {
            public EzComboSpriteText(Func<char, string> getLookup, Bindable<string> themeName)
                : base(getLookup, themeName)
            {
            }

            protected override char[] GetPreloadSpecialChars()
                => new[] { 't', 'e', 'l' };

            protected override string[] GetPossiblePaths(string themeName, string lookup, char character)
            {
                string root = $"GameTheme/{themeName}";

                switch (character)
                {
                    case 'e':
                    case 'l':
                        return new[]
                        {
                            $"{root}/{lookup}"
                        };

                    case 't':
                        return new[]
                        {
                            $"{root}/combo/{lookup}"
                        };

                    default:
                        return new[]
                        {
                            $"{root}/combo/number/{lookup}",
                        };
                }
            }
        }
    }
}
