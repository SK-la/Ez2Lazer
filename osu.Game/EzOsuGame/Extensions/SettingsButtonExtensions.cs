// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.EzOsuGame.Extensions
{
    /// <summary>
    /// 支持显式换行文本的圆角按钮。
    /// </summary>
    public partial class EzTwoLineTextRoundedButton : RoundedButton
    {
        private readonly LocalisableString text;
        private readonly float fontSize;
        private readonly FillFlowContainer textFlow;

        private ILocalisedBindableString localisedText = null!;

        [Resolved]
        private LocalisationManager localisation { get; set; } = null!;

        public EzTwoLineTextRoundedButton(LocalisableString text, float fontSize = 14f)
        {
            this.text = text;
            this.fontSize = fontSize;

            Height = 38;

            Text = string.Empty;
            SpriteText.Hide();

            Content.Add(textFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 0),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            localisedText = localisation.GetLocalisedBindableString(text);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localisedText.BindValueChanged(value => updateText(value.NewValue), true);
        }

        private void updateText(string resolvedText)
        {
            string[] lines = string.IsNullOrEmpty(resolvedText)
                ? new[] { string.Empty }
                : resolvedText.Split('\n');

            float displayFontSize = lines.Length > 1 ? fontSize : fontSize + 3f;

            textFlow.Spacing = new Vector2(0, lines.Length > 1 ? 4 : 0);

            textFlow.Clear();

            foreach (string line in lines)
            {
                textFlow.Add(new OsuSpriteText
                {
                    Text = line,
                    Font = OsuFont.GetFont(size: displayFontSize, weight: FontWeight.Bold),
                    UseFullGlyphHeight = false,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }
        }
    }
}
