// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Tests.Visual.Fonts
{
    public partial class TestSceneEmojiRendering : OsuTestScene
    {
        [Test]
        public void TestRenderBuiltInEmojiFont()
        {
            // a small curated set of common emoji characters to test rendering.
            // if you want to expand this, add more characters to this string.
            const string emojis = "😀😁😂🤣😅😊😍🤔🙌👍👎🎉🔥💯🙂😎❤️✨🎶";

            FillFlowContainer<Drawable> flow = null;
            OsuTextBox input = null;
            OsuSpriteText inputPreview = null;
            OsuSpriteText[] staticEmojiTexts = null;

            AddStep("show emoji using Noto-Emoji font", () =>
            {
                // Use StringInfo to split the string into text elements so surrogate-pair emoji are preserved.
                var indices = System.Globalization.StringInfo.ParseCombiningCharacters(emojis);

                staticEmojiTexts = indices.Select(i =>
                {
                    int nextIndex = i;
                    int current = i;
                    int idxInList = System.Array.IndexOf(indices, i);
                    if (idxInList + 1 < indices.Length)
                        nextIndex = indices[idxInList + 1];

                    string element = emojis.Substring(current, nextIndex - current);

                    return new OsuSpriteText
                    {
                        Text = element,
                        Font = new FontUsage("Noto-Emoji", 48),
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                    };
                }).ToArray();

                Child = flow = new FillFlowContainer<Drawable>
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(5),
                    Padding = new MarginPadding(10),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Static emoji glyphs:",
                            Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                        },
                    }.Concat(staticEmojiTexts).Concat(new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 2,
                            Colour = osuTK.Graphics.Color4.DimGray,
                        },
                        new OsuSpriteText
                        {
                            Text = "Interactive input test (paste/type emoji):",
                            Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                        },
                        input = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Text = emojis,
                        },
                        inputPreview = new OsuSpriteText
                        {
                            Font = new FontUsage("Noto-Emoji", 48),
                            Text = emojis,
                        },
                    }).ToArray()
                };

                input.Current.BindValueChanged(v => inputPreview.Text = v.NewValue, true);
            });

            // wait until at least one child has been measured/layouted (avoids race with first frame)
            AddUntilStep("wait for layout", () => flow != null && flow.Children.Count > 0 && flow.Children.Any(t => t.DrawWidth > 0));

            AddStep("validate widths", () =>
            {
                var zeros = staticEmojiTexts.Select((t, i) => (i, text: t.Text.ToString(), width: t.DrawWidth)).Where(x => x.width <= 0).ToArray();
                if (zeros.Length > 0)
                    NUnit.Framework.Assert.Fail($"emoji missing glyphs / zero width: {string.Join(", ", zeros.Select(z => $"#{z.i}='{z.text}'(w={z.width})"))}");
            });
        }
    }
}


