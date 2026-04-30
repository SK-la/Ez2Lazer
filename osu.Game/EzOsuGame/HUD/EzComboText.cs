// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
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
        private readonly EzComboSpriteText textPart;
        public Bindable<EzEnumGameThemeName> FontName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);
        public Bindable<bool> UseLazerFont { get; } = new Bindable<bool>(false);

        public FillFlowContainer TextContainer { get; private set; }

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

        public EzComboText(Bindable<EzEnumGameThemeName>? externalFontName = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            if (externalFontName is not null)
                FontName.BindTo(externalFontName);

            textPart = new EzComboSpriteText(textLookup, FontName);

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

            float scale = getUniformHeightScale(textPart.Height);
            textPart.Scale = new Vector2(scale);

            FontName.BindValueChanged(e =>
            {
                textPart.FontName.Value = e.NewValue;
                // textPart.LoadAsync(); // **强制重新加载字体**
                scale = getUniformHeightScale(textPart.Height);
                textPart.Scale = new Vector2(scale);
                textPart.Invalidate(); // **确保 UI 立即刷新**
            }, true);
        }

        private float getUniformHeightScale(float textureHeight, float targetHeight = 25f)
            => textureHeight <= 0 ? 1 : targetHeight / textureHeight;

        private partial class EzComboSpriteText : EzSpriteText
        {
            public EzComboSpriteText(Func<char, string> getLookup, Bindable<EzEnumGameThemeName> fontName)
                : base(getLookup, fontName)
            {
            }

            protected override char[] GetPreloadSpecialChars()
                => new[] { 't', 'e', 'l' };

            protected override string[] GetPossiblePaths(string themeRoot, string lookup, char character)
            {
                switch (character)
                {
                    case 'e':
                    case 'l':
                        return new[]
                        {
                            Path.Combine(themeRoot, lookup)
                        };

                    case 't':
                        return new[]
                        {
                            Path.Combine(themeRoot, "combo", lookup)
                        };

                    default:
                        // 数字从 combo/number 目录读取
                        return new[]
                        {
                            Path.Combine(themeRoot, "combo", "number", lookup),
                        };
                }
            }
        }
    }
}
