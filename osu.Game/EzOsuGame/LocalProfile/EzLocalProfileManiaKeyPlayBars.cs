// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Mania play-count bars grouped by key mode (bar portion of former <see cref="EzLocalProfileManiaOverview"/>).
    /// </summary>
    public partial class EzLocalProfileManiaKeyPlayBars : FillFlowContainer
    {
        public EzLocalProfileManiaKeyPlayBars(IReadOnlyList<EzLocalProfileManiaKeyStats> keyStats)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 6);

            var ordered = keyStats.OrderBy(k => k.KeyCount).ToList();

            if (ordered.Count == 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            int maxPlays = ordered.Max(k => k.ScoreCount);

            foreach (var key in ordered)
            {
                float ratio = maxPlays <= 0 ? 0 : (float)key.ScoreCount / maxPlays;
                Add(new KeyPlayBarRow($"{key.KeyCount}K", key.ScoreCount, ratio));
            }
        }

        private partial class KeyPlayBarRow : Container
        {
            public KeyPlayBarRow(string label, int plays, float ratio)
            {
                RelativeSizeAxes = Axes.X;
                Height = 20;
                Padding = new MarginPadding { Horizontal = 4 };

                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = label,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Width = 36,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 40, Right = 48 },
                        Child = new EzLocalProfileRoundedBar(ratio),
                    },
                    new CountText(plays),
                };
            }

            private partial class CountText : OsuSpriteText
            {
                public CountText(int plays)
                {
                    Anchor = Anchor.CentreRight;
                    Origin = Anchor.CentreRight;
                    Text = plays.ToString("N0");
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold);
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colours) => Colour = colours.Content2;
            }
        }
    }
}
