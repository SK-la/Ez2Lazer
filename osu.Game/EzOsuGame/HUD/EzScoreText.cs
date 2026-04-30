// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
{
    public partial class EzScoreText : CompositeDrawable, IHasText
    {
        private readonly EzScoreSpriteText textPart;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public Bindable<EzEnumGameThemeName> FontName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);
        public Bindable<bool> UseLazerFont { get; } = new Bindable<bool>();

        public FillFlowContainer TextContainer { get; private set; }

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

        public EzScoreText()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            textPart = new EzScoreSpriteText(textLookup, FontName);

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
            FontName.BindTo(ezSkinConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName));

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

        private float getUniformHeightScale(float textureHeight, float targetHeight = 35f)
            => textureHeight <= 0 ? 1 : targetHeight / textureHeight;

        private partial class EzScoreSpriteText : EzSpriteText
        {
            public EzScoreSpriteText(Func<char, string> getLookup, Bindable<EzEnumGameThemeName> fontName)
                : base(getLookup, fontName)
            {
            }

            protected override string[] GetPossiblePaths(string themeRoot, string lookup, char character)
            {
                return new[]
                {
                    $@"{themeRoot}number/score/{lookup}",
                    $@"{themeRoot}number/combo/{lookup}",
                    $@"{themeRoot}number/{lookup}",
                };
            }
        }
    }
}
