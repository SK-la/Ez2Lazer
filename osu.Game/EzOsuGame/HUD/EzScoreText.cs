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
    public partial class EzScoreText : CompositeDrawable, IHasText
    {
        internal const float GLYPH_TARGET_HEIGHT = 25;

        private readonly EzScoreSpriteText textPart;

        public Bindable<string> ThemeName { get; } = new Bindable<string>(EzSelectorEnumList.DEFAULT_NAME);

        // public Bindable<bool> UseLazerFont { get; } = new Bindable<bool>();

        public FillFlowContainer TextContainer { get; private set; }

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

        public EzScoreText(Bindable<string>? externalThemeName = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            if (externalThemeName is not null)
                ThemeName = externalThemeName;

            textPart = new EzScoreSpriteText(textLookup, ThemeName);

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
                case '.': return @"dot";

                case '%': return @"percentage";

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
                textPart.Invalidate(); // **确保 UI 立即刷新**
            }, true);

            textPart.ThemeName.BindValueChanged(e =>
            {
                if (ThemeName.Value != e.NewValue)
                {
                    ThemeName.Value = e.NewValue;
                }
            });
        }

        private float getUniformHeightScale(float textureHeight, float targetHeight = 35f)
            => textureHeight <= 0 ? 1 : targetHeight / textureHeight;

        private partial class EzScoreSpriteText : EzSpriteText
        {
            public EzScoreSpriteText(Func<char, string> getLookup, Bindable<string> themeName)
                : base(getLookup, themeName)
            {
            }

            protected override char[] GetPreloadSpecialChars() => new[] { '.', '%' };

            protected override string[] GetPossiblePaths(string themeName, string lookup, char character)
            {
                string root = $"GameTheme/{themeName}/";

                return new[]
                {
                    $"{root}combo/number/{lookup}",
                    $"{root}number/score/{lookup}",
                    $"{root}number/{lookup}",
                };
            }
        }
    }
}
