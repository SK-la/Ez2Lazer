// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;

namespace osu.Game.Skinning.Components
{
    public partial class EzComboText : CompositeDrawable, IHasText
    {
        public readonly EzGetComboTexture TextPart;
        public Bindable<EzEnumGameThemeName> FontName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        public FillFlowContainer TextContainer { get; private set; }

        // public float DefaultWidth { get; set; } = 100; // 默认宽度

        public LocalisableString Text
        {
            get => TextPart.Text;
            set => TextPart.Text = value;
        }

        // public object Spacing { get; set; }

        public EzComboText(Bindable<EzEnumGameThemeName>? externalFontName = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            if (externalFontName is not null)
                FontName.BindTo(externalFontName);

            TextPart = new EzGetComboTexture(textLookup, FontName);

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
                        TextPart
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

                case 'c': return @"Title";

                case 'e': return @"Early";

                case 'l': return @"Late";

                case 'j': return @"judgement";

                default: return c.ToString();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            float scale = calculateScale(TextPart.Height);
            TextPart.Scale = new Vector2(scale);

            FontName.BindValueChanged(e =>
            {
                TextPart.FontName.Value = e.NewValue;
                // textPart.LoadAsync(); // **强制重新加载字体**
                scale = calculateScale(TextPart.Height);
                TextPart.Scale = new Vector2(scale);
                TextPart.Invalidate(); // **确保 UI 立即刷新**
            }, true);
        }

        private float calculateScale(float textureHeight, float targetHeight = 25f)
        {
            if (textureHeight <= 0)
                return 1;

            return targetHeight / textureHeight;
        }
    }
}
