// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;
using osu.Game.Tests.Visual;

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

            FillFlowContainer<OsuSpriteText> flow = null;

            AddStep("show emoji using Noto-Emoji font", () =>
            {
                // Use StringInfo to split the string into text elements so surrogate-pair emoji are preserved.
                var indices = System.Globalization.StringInfo.ParseCombiningCharacters(emojis);

                Child = flow = new FillFlowContainer<OsuSpriteText>
                {
                    RelativeSizeAxes = osu.Framework.Graphics.Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(5),
                    Padding = new osu.Framework.Graphics.MarginPadding(10),
                    Children = indices.Select(i =>
                    {
                        int nextIndex = i;
                        int current = i;
                        // find the end of this text element
                        int idxInList = System.Array.IndexOf(indices, i);
                        if (idxInList + 1 < indices.Length)
                            nextIndex = indices[idxInList + 1];

                        string element = emojis.Substring(current, nextIndex - current);

                        return new OsuSpriteText
                        {
                            Text = element,
                            Font = new FontUsage("Noto-Emoji", 48),
                            Anchor = osu.Framework.Graphics.Anchor.TopLeft,
                            Origin = osu.Framework.Graphics.Anchor.TopLeft,
                        };
                    }).ToArray()
                };
            });

            // wait until at least one child has been measured/layouted (avoids race with first frame)
            AddUntilStep("wait for layout", () => flow != null && flow.Children.Count > 0 && flow.Children.Any(t => t.DrawWidth > 0));

            AddStep("validate widths", () =>
            {
                var zeros = flow.Children.Select((t, i) => (i, text: t.Text, width: t.DrawWidth)).Where(x => x.width <= 0).ToArray();
                if (zeros.Length > 0)
                    NUnit.Framework.Assert.Fail($"emoji missing glyphs / zero width: {string.Join(", ", zeros.Select(z => $"#{z.i}='{z.text}'(w={z.width})"))}");
            });
        }
    }
}


