// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Skinning.Components
{
    public partial class EzScoreText : CompositeDrawable, IHasText
    {
        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public readonly EzGetScoreTexture TextPart;
        public Bindable<EzEnumGameThemeName> FontName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        public FillFlowContainer TextContainer { get; private set; }

        // public float DefaultWidth { get; set; } = 100; // 默认宽度

        public LocalisableString Text
        {
            get => TextPart.Text;
            set => TextPart.Text = value;
        }

        // public object Spacing { get; set; }

        public EzScoreText()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            TextPart = new EzGetScoreTexture(textLookup, FontName);

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

                default: return c.ToString();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            FontName.BindTo(ezSkinConfig.GetBindable<EzEnumGameThemeName>(EzSkinSetting.GameThemeName));

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

        private float calculateScale(float textureHeight, float targetHeight = 35f)
        {
            if (textureHeight <= 0)
                return 1;

            return targetHeight / textureHeight;
        }
    }
}
